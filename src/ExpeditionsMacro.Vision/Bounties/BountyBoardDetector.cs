using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Bounties;

public enum BountyBoardState
{
    None,
    EventCatalog,
    Board,
    RerollConfirmation,
    RewardOverlay,
}

public readonly record struct BountyBoardMatch(
    BountyBoardState State,
    double Confidence,
    IReadOnlyList<BountyCardAction> Actions,
    IReadOnlyList<BountyNumberMatch> Numbers,
    bool NoGold);

public static class BountyBoardDetector
{
    private static readonly ScreenRegion EventTab =
        new(10, 208, 168, 52);
    private static readonly ScreenRegion BoardHeader =
        new(205, 20, 125, 60);
    private static readonly ScreenRegion BoardTopRail =
        new(195, 210, 605, 20);
    private static readonly ScreenRegion RewardTitle =
        new(315, 230, 185, 45);
    private static readonly ScreenRegion RewardBackdrop =
        new(200, 150, 408, 300);
    private static readonly ScreenRegion Modal =
        new(275, 255, 270, 110);

    public static BountyBoardMatch Detect(
        ImageFrame image)
    {
        Validate(image);
        double noGold =
            BountyNoGoldRecognizer.Score(image);
        double reward = RewardScore(image);
        if (reward >= 0.78)
        {
            return Trace(
                new BountyBoardMatch(
                    BountyBoardState.RewardOverlay,
                    reward,
                    [],
                    [],
                    noGold > 0));
        }

        (int X, int Y)? confirm =
            BountyBoardActionDetector
                .ConfirmationAction(image);
        double modal = ConfirmationScore(
            image,
            confirm is not null);
        if (modal >= 0.78)
        {
            return Trace(
                new BountyBoardMatch(
                    BountyBoardState.RerollConfirmation,
                    modal,
                    [],
                    BountyNumberRecognizer
                        .Detect(image),
                    noGold > 0));
        }

        double board = BoardScore(image);
        if (board >= 0.76)
        {
            return Trace(
                new BountyBoardMatch(
                    BountyBoardState.Board,
                    board,
                    BountyBoardActionDetector
                        .Find(image),
                    BountyNumberRecognizer
                        .Detect(image),
                    noGold > 0));
        }

        double entry = EventTabScore(image);
        return Trace(
            entry >= 0.76
                ? new BountyBoardMatch(
                    BountyBoardState.EventCatalog,
                    entry,
                    [],
                    [],
                    noGold > 0)
                : new BountyBoardMatch(
                    BountyBoardState.None,
                    Math.Max(
                        board,
                        entry),
                    [],
                    [],
                    noGold > 0));
    }

    public static (int X, int Y)
        LobbyEventAction => (50, 410);

    public static (int X, int Y)
        BountyBoardEventAction => (92, 234);

    public static (int X, int Y)
        RerollCancelAction => (462, 336);

    public static (int X, int Y)?
        RerollConfirmAction(
        ImageFrame image) =>
        BountyBoardActionDetector
            .ConfirmationAction(image);

    public static (int X, int Y)
        RewardDismissAction => (404, 386);

    public static (int X, int Y)
        BoardBackAction => (55, 588);

    private static double BoardScore(
        ImageFrame image)
    {
        double headerGold =
            ColorFraction(
                image,
                BoardHeader,
                IsGold);
        double headerBronze =
            ColorFraction(
                image,
                BoardHeader,
                IsBronze);
        double railBronze =
            ColorFraction(
                image,
                BoardTopRail,
                IsBronze);
        double tabBronze =
            ColorFraction(
                image,
                EventTab,
                IsBronze);
        double tabNeutral =
            ColorFraction(
                image,
                EventTab,
                IsNeutral);
        if (headerGold < 0.025 ||
            headerBronze < 0.15 ||
            railBronze < 0.14 ||
            tabBronze < 0.10 ||
            tabNeutral < 0.025)
        {
            return 0;
        }
        return Math.Clamp(
            0.76 +
            0.06 * Ramp(
                headerGold,
                0.025,
                0.08) +
            0.06 * Ramp(
                headerBronze,
                0.15,
                0.42) +
            0.06 * Ramp(
                railBronze,
                0.14,
                0.42) +
            0.06 * Ramp(
                tabBronze,
                0.10,
                0.22),
            0,
            1);
    }

    private static double EventTabScore(
        ImageFrame image)
    {
        double bronze =
            ColorFraction(
                image,
                EventTab,
                IsBronze);
        double neutral =
            ColorFraction(
                image,
                EventTab,
                IsNeutral);
        double dark =
            ColorFraction(
                image,
                EventTab,
                IsDark);
        if (bronze < 0.24 ||
            neutral < 0.025 ||
            dark < 0.30)
        {
            return 0;
        }
        return Math.Clamp(
            0.76 +
            0.10 * Ramp(
                bronze,
                0.24,
                0.55) +
            0.08 * Ramp(
                neutral,
                0.025,
                0.10) +
            0.06 * Ramp(
                dark,
                0.30,
                0.70),
            0,
            1);
    }

    private static double ConfirmationScore(
        ImageFrame image,
        bool liveAction)
    {
        double dark =
            ColorFraction(
                image,
                Modal,
                IsDark);
        double neutral =
            ColorFraction(
                image,
                Modal,
                IsNeutral);
        if (!liveAction ||
            dark < 0.50 ||
            neutral < 0.015)
        {
            return 0;
        }
        return Math.Clamp(
            0.78 +
            0.12 * Ramp(
                dark,
                0.50,
                0.78) +
            0.10 * Ramp(
                neutral,
                0.015,
                0.075),
            0,
            1);
    }

    private static double RewardScore(
        ImageFrame image)
    {
        double gold =
            ColorFraction(
                image,
                RewardTitle,
                IsGold);
        double dark =
            ColorFraction(
                image,
                RewardBackdrop,
                IsDark);
        double neutral =
            ColorFraction(
                image,
                new ScreenRegion(
                    325,
                    340,
                    160,
                    35),
                IsNeutral);
        if (gold < 0.055 ||
            dark < 0.80 ||
            neutral < 0.006)
        {
            return 0;
        }
        return Math.Clamp(
            0.78 +
            0.10 * Ramp(
                gold,
                0.055,
                0.14) +
            0.06 * Ramp(
                dark,
                0.80,
                0.98) +
            0.06 * Ramp(
                neutral,
                0.006,
                0.04),
            0,
            1);
    }

    private static double ColorFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        int matches = 0;
        for (int y = region.Y;
             y < region.Bottom;
             y++)
        {
            for (int x = region.X;
                 x < region.Right;
                 x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                if (predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    matches++;
                }
            }
        }
        return (double)matches /
            (region.Width * region.Height);
    }

    private static bool IsGold(
        byte red,
        byte green,
        byte blue) =>
        red > 150 &&
        green > 75 &&
        green < 205 &&
        blue < 90;

    private static bool IsBronze(
        byte red,
        byte green,
        byte blue) =>
        red > 70 &&
        green > 35 &&
        red > green * 1.25 &&
        green > blue * 1.15;

    private static bool IsNeutral(
        byte red,
        byte green,
        byte blue)
    {
        int maximum = Math.Max(
            red,
            Math.Max(green, blue));
        int minimum = Math.Min(
            red,
            Math.Min(green, blue));
        return minimum > 140 &&
            maximum - minimum < 60;
    }

    private static bool IsDark(
        byte red,
        byte green,
        byte blue) =>
        (red + green + blue) / 3 < 55;

    private static double Ramp(
        double value,
        double low,
        double high) =>
        Math.Clamp(
            (value - low) /
            Math.Max(0.0001, high - low),
            0,
            1);

    private static BountyBoardMatch Trace(
        BountyBoardMatch match)
    {
        VisionTrace.Emit(
            "bounty_board",
            match.State.ToString(),
            match.Confidence,
            new
            {
                match.NoGold,
                Actions = match.Actions.Select(
                    action => new
                    {
                        action.Kind,
                        action.X,
                        action.Y,
                    }),
                Numbers = match.Numbers.Select(
                    number => new
                    {
                        number.Number,
                        number.Confidence,
                        number.CenterX,
                        number.CenterY,
                    }),
            });
        return match;
    }

    private static void Validate(
        ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new ArgumentException(
                "Bounty Board detection requires an 808 by 611 RGB client capture.",
                nameof(image));
        }
    }
}
