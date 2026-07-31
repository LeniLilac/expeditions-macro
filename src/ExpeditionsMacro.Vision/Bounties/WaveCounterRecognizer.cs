using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Bounties;

public readonly record struct WaveCounterMatch(
    int Wave,
    double Confidence,
    int Distance,
    int Margin);

public static class WaveCounterRecognizer
{
    private const int Width = 16;
    private const int Height = 11;
    private const int BytesPerTemplate = 22;
    private const int MaximumDistance = 25;
    private const int MinimumMargin = 3;
    private const int SearchRadius = 1;
    private const int SearchDiameter =
        2 * SearchRadius + 1;
    private const int ObservationsPerAnchor =
        SearchDiameter * SearchDiameter;
    private const int ObservationCount =
        ObservationsPerAnchor;
    private const double MinimumCoverage = 0.70;
    private const double MinimumDice = 0.72;

    private static readonly byte[] Templates =
        DecodeTemplates(
            "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");

    private static readonly int[] TemplatePixels =
        CountTemplatePixels(Templates);

    public static WaveCounterMatch? Detect(
        ImageFrame image)
    {
        Validate(image);
        WaveCounterOwnerMatch? owner =
            WaveCounterOwnerDetector
                .Detect(image);
        if (owner is null)
        {
            return null;
        }
        ScreenRegion counterRegion =
            owner.Value.CounterRegion;
        Span<byte> observations =
            stackalloc byte[
                BytesPerTemplate * ObservationCount];
        observations.Clear();
        Span<int> observedPixels =
            stackalloc int[ObservationCount];
        int observation = 0;
        for (int offsetY = -SearchRadius;
             offsetY <= SearchRadius;
             offsetY++)
        {
            for (int offsetX = -SearchRadius;
                 offsetX <= SearchRadius;
                 offsetX++)
            {
                Span<byte> observed =
                    observations.Slice(
                        observation *
                            BytesPerTemplate,
                        BytesPerTemplate);
                for (int y = 0;
                     y < Height;
                     y++)
                {
                    for (int x = 0;
                         x < Width;
                         x++)
                    {
                        int pixel =
                            ((counterRegion.Y +
                              offsetY + y) *
                                 image.Width +
                             counterRegion.X +
                             offsetX + x) * 3;
                        int luminance =
                            (299 *
                                 image.Pixels[pixel] +
                             587 *
                                 image.Pixels[
                                     pixel + 1] +
                             114 *
                                 image.Pixels[
                                     pixel + 2]) /
                            1000;
                        if (luminance <= 140)
                        {
                            continue;
                        }
                        int bit =
                            y * Width + x;
                        observed[bit / 8] |=
                            (byte)(
                                1 <<
                                (bit % 8));
                        observedPixels[
                            observation]++;
                    }
                }
                observation++;
            }
        }

        Candidate? best = null;
        Candidate? second = null;
        for (int wave = 0; wave <= 100; wave++)
        {
            Candidate? waveBest = null;
            int templateOffset =
                wave * BytesPerTemplate;
            for (int index = 0;
                 index < ObservationCount;
                 index++)
            {
                ReadOnlySpan<byte> observed =
                    observations.Slice(
                        index * BytesPerTemplate,
                        BytesPerTemplate);
                int intersection = 0;
                for (int templateByte = 0;
                     templateByte <
                     BytesPerTemplate;
                     templateByte++)
                {
                    intersection +=
                        System.Numerics
                            .BitOperations
                            .PopCount(
                                (uint)(
                                    observed[
                                        templateByte] &
                                    Templates[
                                        templateOffset +
                                        templateByte]));
                }
                int distance =
                    observedPixels[index] +
                    TemplatePixels[wave] -
                    2 * intersection;
                double coverage =
                    intersection /
                    (double)TemplatePixels[wave];
                double dice =
                    2d * intersection /
                    Math.Max(
                        1,
                        observedPixels[index] +
                        TemplatePixels[wave]);
                int offsetX =
                    index %
                        ObservationsPerAnchor %
                        SearchDiameter -
                    SearchRadius;
                int offsetY =
                    index %
                        ObservationsPerAnchor /
                        SearchDiameter -
                    SearchRadius;
                Candidate candidate = new(
                    wave,
                    distance,
                    coverage,
                    dice,
                    observedPixels[index],
                    counterRegion.X,
                    counterRegion.Y,
                    offsetX,
                    offsetY);
                if (waveBest is null ||
                    IsBetter(
                        candidate,
                        waveBest.Value))
                {
                    waveBest = candidate;
                }
            }
            Candidate waveCandidate =
                waveBest!.Value;
            if (best is null ||
                IsBetter(
                    waveCandidate,
                    best.Value))
            {
                second = best;
                best = waveCandidate;
            }
            else if (second is null ||
                     IsBetter(
                         waveCandidate,
                         second.Value))
            {
                second = waveCandidate;
            }
        }

        Candidate winner = best!.Value;
        int margin =
            second!.Value.Distance -
            winner.Distance;
        // Absolute distance alone accepts the sparse wave-1 template on an empty counter.
        WaveCounterMatch? match =
            winner.Distance <= MaximumDistance &&
            margin >= MinimumMargin &&
            winner.Coverage >= MinimumCoverage &&
            winner.Dice >= MinimumDice
                ? new WaveCounterMatch(
                    winner.Wave,
                    winner.Dice,
                    winner.Distance,
                    margin)
                : null;
        VisionTrace.Emit(
            "bounty_wave_counter",
            match?.Wave.ToString() ?? "none",
            match?.Confidence ?? 0,
            new
            {
                BestWave = winner.Wave,
                BestDistance = winner.Distance,
                margin,
                winner.Coverage,
                winner.Dice,
                winner.ObservedPixels,
                winner.CounterX,
                winner.CounterY,
                winner.OffsetX,
                winner.OffsetY,
            });
        return match;
    }

    private static bool IsBetter(
        Candidate candidate,
        Candidate current) =>
        candidate.Distance <
            current.Distance ||
        candidate.Distance ==
            current.Distance &&
        (candidate.Dice >
            current.Dice ||
         candidate.Dice ==
            current.Dice &&
         Math.Abs(candidate.OffsetX) +
            Math.Abs(candidate.OffsetY) <
         Math.Abs(current.OffsetX) +
            Math.Abs(current.OffsetY));

    private static int[] CountTemplatePixels(
        IReadOnlyList<byte> templates)
    {
        int[] counts = new int[101];
        for (int wave = 0;
             wave < counts.Length;
             wave++)
        {
            int offset =
                wave * BytesPerTemplate;
            for (int index = 0;
                 index < BytesPerTemplate;
                 index++)
            {
                counts[wave] +=
                    System.Numerics.BitOperations
                        .PopCount(
                            (uint)templates[
                                offset + index]);
            }
        }
        return counts;
    }

    private static byte[] DecodeTemplates(
        string payload)
    {
        string normalized =
            payload.TrimEnd('=') + "=";
        byte[] templates =
            Convert.FromBase64String(
                normalized);
        int expected =
            BytesPerTemplate * 101;
        return templates.Length == expected
            ? templates
            : throw new InvalidDataException(
                $"The embedded wave-counter payload contains {templates.Length} bytes; expected {expected}.");
    }

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new ArgumentException(
                "Wave counter detection requires an 808 by 611 RGB client capture.",
                nameof(image));
        }
    }

    private readonly record struct Candidate(
        int Wave,
        int Distance,
        double Coverage,
        double Dice,
        int ObservedPixels,
        int CounterX,
        int CounterY,
        int OffsetX,
        int OffsetY);

}
