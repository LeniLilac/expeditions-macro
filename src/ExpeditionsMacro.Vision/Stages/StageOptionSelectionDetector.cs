using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Stages;

public sealed record StoryOptionSelectionMatch(
    StoryRunKind? RunKind,
    int ActNumber,
    bool? HardMode,
    double Confidence,
    int? ActionX = null,
    int? ActionY = null)
{
    public bool Matches(
        StoryRunKind expectedKind,
        int expectedActNumber,
        bool? expectedHardMode)
    {
        if (RunKind != expectedKind)
        {
            return false;
        }
        if (expectedKind == StoryRunKind.Act &&
            ActNumber != expectedActNumber)
        {
            return false;
        }
        return expectedHardMode is null ||
            HardMode == expectedHardMode;
    }
}

public sealed record RaidOptionSelectionMatch(
    RaidAct? Act,
    double Confidence,
    int? ActionX = null,
    int? ActionY = null)
{
    public bool Matches(RaidAct expectedAct) =>
        Act == expectedAct;
}

public static class StageOptionSelectionDetector
{
    private const int RailSearchLeft = 100;
    private const int RailSearchRight = 205;
    private const int RailSearchTop = 150;
    private const int RailSearchBottom = 470;
    private const int MinimumRailAccentPixels = 240;
    private const int RailBandRadius = 1;
    private const int RowHalfHeight = 12;
    private const int RowLeftOffset = 4;
    private const int RowRightOffset = 46;

    private static readonly int[] StoryRowCenters =
        [201, 240, 279, 318, 357, 398, 437];
    private static readonly int[] RaidRowCenters =
        [229, 318, 407];

    public static StoryOptionSelectionMatch DetectStory(
        ImageFrame image)
    {
        ValidateClient(image);
        StageScreenMatch detail =
            StageScreenDetector.Detect(image);
        if (detail.State != StageScreenState.StoryDetail ||
            FindRailLeft(image) is not int railLeft)
        {
            return TraceStory(
                new StoryOptionSelectionMatch(
                    null,
                    0,
                    null,
                    0));
        }

        double[] rowScores = StoryRowCenters
            .Select(center => BestRowScore(
                image,
                railLeft,
                center,
                IsCyan))
            .ToArray();
        int selectedRow = SelectDistinctRow(
            rowScores,
            minimumScore: 0.50,
            minimumSeparation: 0.25);
        if (selectedRow < 0)
        {
            return TraceStory(
                new StoryOptionSelectionMatch(
                    null,
                    0,
                    null,
                    rowScores.Max()));
        }

        StoryRunKind runKind =
            selectedRow < 5
                ? StoryRunKind.Act
                : selectedRow == 5
                    ? StoryRunKind.Infinite
                    : StoryRunKind.Mastery;
        int actNumber =
            runKind == StoryRunKind.Act
                ? selectedRow + 1
                : 0;
        bool? hardMode =
            runKind == StoryRunKind.Act
                ? DetectDifficulty(image, railLeft)
                : null;
        return TraceStory(
            new StoryOptionSelectionMatch(
                runKind,
                actNumber,
                hardMode,
                Math.Min(
                    detail.Confidence,
                    rowScores[selectedRow]),
                detail.ActionX,
                detail.ActionY));
    }

    public static RaidOptionSelectionMatch DetectRaid(
        ImageFrame image)
    {
        ValidateClient(image);
        StageScreenMatch detail =
            StageScreenDetector.Detect(image);
        if (detail.State != StageScreenState.RaidDetail ||
            FindRailLeft(image) is not int railLeft)
        {
            return TraceRaid(
                new RaidOptionSelectionMatch(
                    null,
                    0));
        }

        double[] rowScores = RaidRowCenters
            .Select(center => BestRowScore(
                image,
                railLeft,
                center,
                IsRaidRed))
            .ToArray();
        int selectedRow = SelectDistinctRow(
            rowScores,
            minimumScore: 0.65,
            minimumSeparation: 0.35);
        if (selectedRow < 0)
        {
            return TraceRaid(
                new RaidOptionSelectionMatch(
                    null,
                    rowScores.Max()));
        }

        return TraceRaid(
            new RaidOptionSelectionMatch(
                (RaidAct)(selectedRow + 1),
                Math.Min(
                    detail.Confidence,
                    rowScores[selectedRow]),
                detail.ActionX,
                detail.ActionY));
    }

    private static int? FindRailLeft(ImageFrame image)
    {
        // Keep the one-pixel rail structural while allowing its opaque coverage
        // to phase across immediately adjacent raster columns.
        int bestX = -1;
        int bestSupport = 0;
        int bestCenterCount = 0;
        for (int x = RailSearchLeft + RailBandRadius;
             x < RailSearchRight - RailBandRadius;
             x++)
        {
            int support = 0;
            int centerCount = 0;
            for (int y = RailSearchTop;
                 y < RailSearchBottom;
                 y++)
            {
                bool rowSupported = false;
                for (int offset = -RailBandRadius;
                     offset <= RailBandRadius;
                     offset++)
                {
                    ReadPixel(
                        image,
                        x + offset,
                        y,
                        out byte red,
                        out byte green,
                        out byte blue);
                    if (!IsStrongAccent(red, green, blue))
                    {
                        continue;
                    }
                    rowSupported = true;
                    if (offset == 0)
                    {
                        centerCount++;
                    }
                }
                if (rowSupported) support++;
            }
            if (support < bestSupport ||
                support == bestSupport &&
                centerCount <= bestCenterCount)
            {
                continue;
            }
            bestSupport = support;
            bestCenterCount = centerCount;
            bestX = x;
        }
        return bestSupport >= MinimumRailAccentPixels
            ? bestX
            : null;
    }

    private static double BestRowScore(
        ImageFrame image,
        int railLeft,
        int expectedCenter,
        Func<byte, byte, byte, bool> predicate)
    {
        double best = 0;
        for (int offset = -8; offset <= 8; offset++)
        {
            int top =
                expectedCenter +
                offset -
                RowHalfHeight;
            int matching = 0;
            int total = 0;
            for (int y = top;
                 y < top + RowHalfHeight * 2;
                 y++)
            {
                for (int x = railLeft + RowLeftOffset;
                     x < railLeft + RowRightOffset;
                     x++)
                {
                    ReadPixel(
                        image,
                        x,
                        y,
                        out byte red,
                        out byte green,
                        out byte blue);
                    if (predicate(red, green, blue))
                    {
                        matching++;
                    }
                    total++;
                }
            }
            best = Math.Max(
                best,
                (double)matching / total);
        }
        return best;
    }

    private static int SelectDistinctRow(
        IReadOnlyList<double> scores,
        double minimumScore,
        double minimumSeparation)
    {
        int selected = -1;
        double best = 0;
        double second = 0;
        for (int index = 0;
             index < scores.Count;
             index++)
        {
            double score = scores[index];
            if (score > best)
            {
                second = best;
                best = score;
                selected = index;
            }
            else
            {
                second = Math.Max(second, score);
            }
        }
        return best >= minimumScore &&
            best - second >= minimumSeparation
                ? selected
                : -1;
    }

    private static bool? DetectDifficulty(
        ImageFrame image,
        int railLeft)
    {
        int left = railLeft + 90;
        int right = railLeft + 220;
        const int top = 200;
        const int bottom = 225;
        double green = ColorFraction(
            image,
            left,
            top,
            right,
            bottom,
            IsStoryGreen);
        double red = ColorFraction(
            image,
            left,
            top,
            right,
            bottom,
            IsRaidRed);
        if (green >= 0.08 &&
            green - red >= 0.05)
        {
            return false;
        }
        if (red >= 0.035 &&
            red - green >= 0.025)
        {
            return true;
        }
        return null;
    }

    private static double ColorFraction(
        ImageFrame image,
        int left,
        int top,
        int right,
        int bottom,
        Func<byte, byte, byte, bool> predicate)
    {
        int matching = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                ReadPixel(
                    image,
                    x,
                    y,
                    out byte red,
                    out byte green,
                    out byte blue);
                if (predicate(red, green, blue))
                {
                    matching++;
                }
            }
        }
        return (double)matching /
            ((right - left) * (bottom - top));
    }

    private static void ReadPixel(
        ImageFrame image,
        int x,
        int y,
        out byte red,
        out byte green,
        out byte blue)
    {
        int offset =
            (y * image.Width + x) * 3;
        red = image.Pixels[offset];
        green = image.Pixels[offset + 1];
        blue = image.Pixels[offset + 2];
    }

    private static bool IsStrongAccent(
        byte red,
        byte green,
        byte blue)
    {
        int maximum =
            Math.Max(red, Math.Max(green, blue));
        int minimum =
            Math.Min(red, Math.Min(green, blue));
        return maximum >= 75 &&
            maximum - minimum >= 45;
    }

    private static bool IsCyan(
        byte red,
        byte green,
        byte blue) =>
        green >= 75 &&
        blue >= 85 &&
        green - red >= 20 &&
        blue - red >= 28;

    private static bool IsStoryGreen(
        byte red,
        byte green,
        byte blue) =>
        green >= 75 &&
        green - red >= 28 &&
        green - blue >= 20;

    private static bool IsRaidRed(
        byte red,
        byte green,
        byte blue) =>
        red >= 105 &&
        red - green >= 35 &&
        red - blue >= 25;

    private static void ValidateClient(
        ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != ChallengeScreenDetector.ClientWidth ||
            image.Height != ChallengeScreenDetector.ClientHeight)
        {
            throw new InvalidDataException(
                "Stage option detector input must be an RGB 808 by 611 client image.");
        }
    }

    private static StoryOptionSelectionMatch TraceStory(
        StoryOptionSelectionMatch match)
    {
        VisionTrace.Emit(
            "stage_story_option",
            match.RunKind?.ToString() ?? "None",
            match.Confidence,
            new
            {
                match.ActNumber,
                match.HardMode,
                match.ActionX,
                match.ActionY,
            });
        return match;
    }

    private static RaidOptionSelectionMatch TraceRaid(
        RaidOptionSelectionMatch match)
    {
        VisionTrace.Emit(
            "stage_raid_option",
            match.Act?.ToString() ?? "None",
            match.Confidence,
            new
            {
                match.ActionX,
                match.ActionY,
            });
        return match;
    }
}
