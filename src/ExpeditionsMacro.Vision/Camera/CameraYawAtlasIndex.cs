using System.Runtime.CompilerServices;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Camera;

public readonly record struct CameraYawAtlasMatch(
    int Index,
    double Score,
    double FingerprintScore,
    double FingerprintIsolation);

public sealed class CameraYawAtlasIndex
{
    private const int GridColumns = 10;
    private const int GridRows = 5;
    private const int CandidateSeeds = 8;
    private const int BoundedCandidateSeeds = 2;
    private const int FingerprintNeighborhoodRadius = 2;
    private static readonly ConditionalWeakTable<
        IReadOnlyList<ImageFrame>,
        CameraYawAtlasIndex> Cache = new();

    private readonly IReadOnlyList<ImageFrame> _atlas;
    private readonly CameraYawFingerprint[] _fingerprints;
    private readonly int _uniqueCount;

    private CameraYawAtlasIndex(
        IReadOnlyList<ImageFrame> atlas,
        int uniqueCount)
    {
        _atlas = atlas;
        _uniqueCount = uniqueCount;
        _fingerprints = atlas
            .Take(uniqueCount)
            .Select(CameraYawFingerprint.Create)
            .ToArray();
    }

    public static CameraYawAtlasIndex For(
        IReadOnlyList<ImageFrame> atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        if (atlas.Count < 2)
        {
            throw new ArgumentException(
                "A yaw atlas must contain a turn and its closing frame.",
                nameof(atlas));
        }
        return Cache.GetValue(
            atlas,
            frames => new CameraYawAtlasIndex(
                frames,
                frames.Count - 1));
    }

    public CameraYawAtlasMatch FindBest(ImageFrame current)
    {
        return FindBest(
            current,
            Enumerable.Range(0, _uniqueCount),
            CandidateSeeds);
    }

    public CameraYawAtlasMatch FindBestWithin(
        ImageFrame current,
        int minimumIndex,
        int maximumIndex)
    {
        if (minimumIndex < 0 ||
            maximumIndex < minimumIndex ||
            maximumIndex >= _uniqueCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumIndex),
                "The bounded yaw-atlas range is invalid.");
        }
        return FindBest(
            current,
            Enumerable.Range(
                minimumIndex,
                maximumIndex - minimumIndex + 1),
            BoundedCandidateSeeds);
    }

    private CameraYawAtlasMatch FindBest(
        ImageFrame current,
        IEnumerable<int> allowedIndices,
        int candidateSeeds)
    {
        CameraYawFingerprint currentFingerprint =
            CameraYawFingerprint.Create(current);
        int[] allowed = allowedIndices.Distinct().ToArray();
        if (allowed.Length == 0)
        {
            throw new ArgumentException(
                "At least one yaw-atlas candidate is required.",
                nameof(allowedIndices));
        }
        HashSet<int> allowedSet = allowed.ToHashSet();
        (double Score, int Index)[] fingerprintScores = allowed
            .Select(index =>
                (_fingerprints[index].Similarity(currentFingerprint),
                    index))
            .ToArray();
        (double Score, int Index)[] ranked = fingerprintScores
            .OrderByDescending(item => item.Item1)
            .Take(Math.Min(candidateSeeds, allowed.Length))
            .Select(item => (item.Item1, item.Index))
            .ToArray();

        HashSet<int> candidates = [];
        foreach ((_, int index) in ranked)
        {
            for (int neighbor = -1; neighbor <= 1; neighbor++)
            {
                int candidate = Normalize(index + neighbor);
                if (allowedSet.Contains(candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        CameraYawAtlasMatch best = new(
            ranked[0].Index,
            double.NegativeInfinity,
            ranked[0].Score,
            0);
        foreach (int index in candidates)
        {
            double score =
                CameraRegisteredScorer.Score(_atlas[index], current).Score;
            if (score <= best.Score) continue;
            best = new(
                index,
                score,
                _fingerprints[index].Similarity(currentFingerprint),
                0);
        }
        double[] remoteScores = fingerprintScores
            .Where(item =>
                CircularDistance(item.Index, best.Index) >
                FingerprintNeighborhoodRadius)
            .Select(item => item.Score)
            .ToArray();
        if (remoteScores.Length == 0) return best;
        double isolation = Math.Max(
            0,
            best.FingerprintScore - remoteScores.Max());
        return best with { FingerprintIsolation = isolation };
    }

    private int Normalize(int index) =>
        ((index % _uniqueCount) + _uniqueCount) % _uniqueCount;

    private int CircularDistance(int left, int right)
    {
        int distance = Math.Abs(left - right);
        return Math.Min(distance, _uniqueCount - distance);
    }

    public sealed class CameraYawFingerprint
    {
        private readonly double[] _values;
        private readonly double _norm;

        private CameraYawFingerprint(double[] values)
        {
            _values = values;
            _norm = Math.Sqrt(values.Sum(value => value * value));
        }

        public static CameraYawFingerprint Create(ImageFrame frame)
        {
            if (frame.Format != PixelFormat.Gray8)
            {
                throw new ArgumentException(
                    "Yaw fingerprints require grayscale frames.",
                    nameof(frame));
            }

            long globalSum = 0;
            long globalSquareSum = 0;
            foreach (byte value in frame.Pixels)
            {
                globalSum += value;
                globalSquareSum += value * value;
            }
            double globalMean =
                (double)globalSum / frame.Pixels.Length;
            double globalVariance = Math.Max(
                0,
                (double)globalSquareSum / frame.Pixels.Length -
                globalMean * globalMean);
            double globalDeviation =
                Math.Max(8, Math.Sqrt(globalVariance));
            double[] values =
                new double[GridColumns * GridRows * 4];
            int feature = 0;
            for (int row = 0; row < GridRows; row++)
            {
                int y0 = row * frame.Height / GridRows;
                int y1 = (row + 1) * frame.Height / GridRows;
                for (int column = 0; column < GridColumns; column++)
                {
                    int x0 = column * frame.Width / GridColumns;
                    int x1 =
                        (column + 1) * frame.Width / GridColumns;
                    (double mean, double deviation, double horizontal,
                        double vertical) = CellFeatures(
                            frame,
                            x0,
                            y0,
                            x1,
                            y1);
                    values[feature++] =
                        (mean - globalMean) / globalDeviation;
                    values[feature++] = deviation / 64d;
                    values[feature++] = horizontal / 32d;
                    values[feature++] = vertical / 32d;
                }
            }
            return new CameraYawFingerprint(values);
        }

        public double Similarity(CameraYawFingerprint other)
        {
            if (_norm < 1e-9 || other._norm < 1e-9) return 0;
            double dot = 0;
            for (int index = 0; index < _values.Length; index++)
            {
                dot += _values[index] * other._values[index];
            }
            double cosine = dot / (_norm * other._norm);
            return Math.Clamp((cosine + 1) / 2, 0, 1);
        }

        private static (
            double Mean,
            double Deviation,
            double Horizontal,
            double Vertical) CellFeatures(
                ImageFrame frame,
                int x0,
                int y0,
                int x1,
                int y1)
        {
            long sum = 0;
            long squareSum = 0;
            long horizontal = 0;
            long vertical = 0;
            int count = Math.Max(1, (x1 - x0) * (y1 - y0));
            for (int y = y0; y < y1; y++)
            {
                int rowOffset = y * frame.Width;
                for (int x = x0; x < x1; x++)
                {
                    int index = rowOffset + x;
                    byte value = frame.Pixels[index];
                    sum += value;
                    squareSum += value * value;
                    if (x > x0)
                    {
                        horizontal += Math.Abs(
                            value - frame.Pixels[index - 1]);
                    }
                    if (y > y0)
                    {
                        vertical += Math.Abs(
                            value - frame.Pixels[index - frame.Width]);
                    }
                }
            }

            double mean = (double)sum / count;
            double variance =
                Math.Max(0, (double)squareSum / count - mean * mean);
            return (
                mean,
                Math.Sqrt(variance),
                (double)horizontal / count,
                (double)vertical / count);
        }
    }
}
