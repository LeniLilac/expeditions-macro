using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

internal readonly record struct DenseYawPeriodSample(
    TimeSpan Elapsed,
    ImageFrame Thumbnail,
    CameraYawAtlasIndex.CameraYawFingerprint Fingerprint);

internal readonly record struct DenseYawPeriodDecision(
    TimeSpan TurnElapsed,
    bool FoldedRepeatedTurn,
    double MedianAgreement,
    double LowerQuartileAgreement,
    double MedianStructureAgreement,
    double LowerQuartileStructureAgreement,
    double GoalProximity,
    int PairCount);

internal static class DenseYawPeriodPolicy
{
    private const int MaximumComparisonPairs = 18;
    private const int MinimumComparisonPairs = 10;
    private const double MinimumDetectedRepeatedTurnSeconds = 4.5;
    private const double MinimumMedianAgreement = 0.96;
    private const double MinimumLowerQuartileAgreement = 0.93;
    private const double MinimumMedianStructureAgreement = 0.33;
    private const double MinimumLowerQuartileStructureAgreement = 0.25;
    private const double MinimumGoalProximity = 0.78;

    public static DenseYawPeriodDecision ReduceRepeatedTurn(
        IReadOnlyList<DenseYawPeriodSample> samples,
        ImageFrame goalThumbnail,
        TimeSpan detectedTurn)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(goalThumbnail);
        if (detectedTurn <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(detectedTurn));
        }
        if (detectedTurn.TotalSeconds <
                MinimumDetectedRepeatedTurnSeconds ||
            samples.Count < MinimumComparisonPairs * 2)
        {
            return Unchanged(detectedTurn);
        }

        TimeSpan approximatePeriod =
            TimeSpan.FromTicks(detectedTurn.Ticks / 2);
        TimeSpan comparisonWindow = TimeSpan.FromMilliseconds(
            Math.Clamp(
                approximatePeriod.TotalMilliseconds * 0.08,
                160,
                350));
        DenseYawPeriodSample[] firstTurn = SelectEvenly(
            samples
                .Where(sample =>
                    sample.Elapsed >=
                        TimeSpan.FromTicks(
                            approximatePeriod.Ticks / 12) &&
                    sample.Elapsed <=
                        TimeSpan.FromTicks(
                            approximatePeriod.Ticks * 11 / 12))
                .ToArray(),
            MaximumComparisonPairs);
        CameraYawAtlasIndex.CameraYawFingerprint goalFingerprint =
            CameraYawAtlasIndex.CameraYawFingerprint.Create(
                goalThumbnail);
        List<(
            double Agreement,
            double Structure,
            TimeSpan Period)> matches = [];
        foreach (DenseYawPeriodSample first in firstTurn)
        {
            TimeSpan minimum =
                first.Elapsed + approximatePeriod - comparisonWindow;
            TimeSpan maximum =
                first.Elapsed + approximatePeriod + comparisonWindow;
            (DenseYawPeriodSample Sample, double Agreement,
                double Structure)[] candidates = samples
                .Where(candidate =>
                    candidate.Elapsed >= minimum &&
                    candidate.Elapsed <= maximum &&
                    candidate.Elapsed <= detectedTurn)
                .Select(candidate => (
                    Sample: candidate,
                    Agreement:
                        first.Fingerprint.Similarity(
                            candidate.Fingerprint),
                    Structure:
                        CameraRegisteredScorer.Score(
                            first.Thumbnail,
                            candidate.Thumbnail).Score))
                .OrderByDescending(candidate => candidate.Structure)
                .ThenByDescending(candidate => candidate.Agreement)
                .ToArray();
            if (candidates.Length == 0) continue;
            (DenseYawPeriodSample Sample, double Agreement,
                double Structure) best = candidates[0];
            matches.Add((
                best.Agreement,
                best.Structure,
                best.Sample.Elapsed - first.Elapsed));
        }
        if (matches.Count < MinimumComparisonPairs)
        {
            return Unchanged(detectedTurn);
        }

        double[] agreements = matches
            .Select(match => match.Agreement)
            .Order()
            .ToArray();
        double medianAgreement = Median(agreements);
        double lowerQuartileAgreement =
            agreements[(int)Math.Floor((agreements.Length - 1) * 0.25)];
        double[] structures = matches
            .Select(match => match.Structure)
            .Order()
            .ToArray();
        double medianStructureAgreement = Median(structures);
        double lowerQuartileStructureAgreement =
            structures[(int)Math.Floor((structures.Length - 1) * 0.25)];
        TimeSpan measuredPeriod = TimeSpan.FromTicks(
            (long)Math.Round(
                Median(
                    matches
                        .Select(match =>
                            (double)match.Period.Ticks)
                        .Order()
                        .ToArray())));
        double goalProximity = samples
            .Where(sample =>
                Math.Abs(
                    (sample.Elapsed - measuredPeriod)
                        .TotalMilliseconds) <=
                comparisonWindow.TotalMilliseconds)
            .Select(sample =>
                goalFingerprint.Similarity(sample.Fingerprint))
            .DefaultIfEmpty(0)
            .Max();
        bool repeated =
            medianAgreement >= MinimumMedianAgreement &&
            lowerQuartileAgreement >=
                MinimumLowerQuartileAgreement &&
            medianStructureAgreement >=
                MinimumMedianStructureAgreement &&
            lowerQuartileStructureAgreement >=
                MinimumLowerQuartileStructureAgreement &&
            goalProximity >= MinimumGoalProximity;
        return repeated
            ? new DenseYawPeriodDecision(
                measuredPeriod,
                FoldedRepeatedTurn: true,
                medianAgreement,
                lowerQuartileAgreement,
                medianStructureAgreement,
                lowerQuartileStructureAgreement,
                goalProximity,
                matches.Count)
            : new DenseYawPeriodDecision(
                detectedTurn,
                FoldedRepeatedTurn: false,
                medianAgreement,
                lowerQuartileAgreement,
                medianStructureAgreement,
                lowerQuartileStructureAgreement,
                goalProximity,
                matches.Count);
    }

    private static DenseYawPeriodDecision Unchanged(
        TimeSpan detectedTurn) =>
        new(
            detectedTurn,
            FoldedRepeatedTurn: false,
            MedianAgreement: 0,
            LowerQuartileAgreement: 0,
            MedianStructureAgreement: 0,
            LowerQuartileStructureAgreement: 0,
            GoalProximity: 0,
            PairCount: 0);

    private static DenseYawPeriodSample[] SelectEvenly(
        IReadOnlyList<DenseYawPeriodSample> samples,
        int maximum)
    {
        if (samples.Count <= maximum)
        {
            return samples.ToArray();
        }
        DenseYawPeriodSample[] selected =
            new DenseYawPeriodSample[maximum];
        for (int index = 0; index < maximum; index++)
        {
            int source = (int)Math.Round(
                index * (samples.Count - 1d) /
                (maximum - 1d));
            selected[index] = samples[source];
        }
        return selected;
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        int middle = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }
}
