using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Vision.Challenges;

public enum ChallengeScreenState
{
    None,
    GameModeSelector,
    ChallengeList,
    ChallengeAvailable,
    PreviewReady,
    PostMatchPreview,
    Prestart,
    Victory,
    Defeat,
}

public sealed record ChallengeScreenMatch(
    ChallengeScreenState State,
    double Confidence,
    int? ActionX = null,
    int? ActionY = null);

public static class ChallengeScreenDetector
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;

    private static readonly ScreenRegion ChallengeTileRegion = new(360, 150, 250, 105);
    private static readonly ScreenRegion ChallengeTileTitleRegion = new(360, 150, 130, 55);
    private static readonly ScreenRegion StoryTileTitleRegion = new(360, 50, 130, 55);
    private static readonly ScreenRegion RaidTileTitleRegion = new(570, 50, 120, 55);
    private static readonly ScreenRegion ExpeditionTileTitleRegion = new(600, 150, 140, 55);
    private static readonly ScreenRegion PanelHeaderRegion = new(90, 120, 220, 80);
    private static readonly ScreenRegion PanelTopEdgeRegion = new(280, 146, 380, 14);
    private static readonly ScreenRegion PanelLeftRailRegion = new(125, 180, 150, 245);
    private static readonly ScreenRegion PanelRightEdgeRegion = new(655, 140, 35, 330);
    private static readonly ScreenRegion PanelBottomEdgeRegion = new(115, 440, 575, 30);
    private static readonly ScreenRegion VictoryRosterRegion = new(510, 180, 150, 275);

    private static readonly IReadOnlyDictionary<ChallengeType, (int X, int Y)> TypeActions =
        new Dictionary<ChallengeType, (int X, int Y)>
        {
            [ChallengeType.Trait] = (470, 244),
            [ChallengeType.Stat] = (470, 335),
            [ChallengeType.Sprite] = (470, 425),
        };

    public static ChallengeScreenMatch Detect(ImageFrame image)
    {
        IReadOnlyDictionary<ChallengeScreenState, double> scores = ScoreStates(image);
        ChallengeScreenState[] priority =
        [
            ChallengeScreenState.Defeat,
            ChallengeScreenState.Victory,
            ChallengeScreenState.Prestart,
            ChallengeScreenState.PreviewReady,
            ChallengeScreenState.PostMatchPreview,
            ChallengeScreenState.ChallengeAvailable,
            ChallengeScreenState.ChallengeList,
            ChallengeScreenState.GameModeSelector,
        ];
        foreach (ChallengeScreenState state in priority)
        {
            if (scores[state] < Threshold(state)) continue;
            (int X, int Y)? action = ActionFor(state, image);
            return new ChallengeScreenMatch(state, scores[state], action?.X, action?.Y);
        }
        return new ChallengeScreenMatch(ChallengeScreenState.None, 0);
    }

    public static IReadOnlyDictionary<ChallengeScreenState, double> ScoreStates(ImageFrame image)
    {
        ValidateClient(image);
        double panel = PanelScore(image);
        double selectStage = ActionButtonDetector.Score(image, "challenge_select_stage");
        double enterMatchmaking = ActionButtonDetector.Score(image, "challenge_enter_matchmaking");
        double previewStart = ActionButtonDetector.Score(image, "challenge_preview_start");
        double changeMode = ActionButtonDetector.Score(image, "challenge_change_mode");
        double victoryClose = ActionButtonDetector.Score(image, "challenge_victory_close");
        double defeat = TerminalScreenDetector.Score(image, "defeat");
        double prestart = RewardScreenDetector.HasHeader(image) ? 0 : StartDialogDetector.Score(image);
        double challengeList = ChallengeListScore(image, panel);
        double availability = Math.Max(selectStage, enterMatchmaking);
        double challengeAvailable = panel < 0.55 || availability == 0 ? 0 : Math.Clamp(0.48 * panel + 0.52 * availability, 0, 1);
        double preview = previewStart == 0 || changeMode == 0 ? 0 : Math.Clamp(0.55 * previewStart + 0.45 * changeMode, 0, 1);
        double postMatchPreview = previewStart > 0 || changeMode == 0 ? 0 : Math.Clamp(0.72 * changeMode + 0.28 * DarkPartyPanelScore(image), 0, 1);
        double victory = victoryClose == 0 ? 0 : Math.Clamp(0.40 * panel + 0.35 * victoryClose + 0.25 * VictoryRosterScore(image), 0, 1);

        return new Dictionary<ChallengeScreenState, double>
        {
            [ChallengeScreenState.None] = 0,
            [ChallengeScreenState.GameModeSelector] = GameModeSelectorScore(image),
            [ChallengeScreenState.ChallengeList] = challengeList,
            [ChallengeScreenState.ChallengeAvailable] = challengeAvailable,
            [ChallengeScreenState.PreviewReady] = preview,
            [ChallengeScreenState.PostMatchPreview] = postMatchPreview,
            [ChallengeScreenState.Prestart] = prestart,
            [ChallengeScreenState.Victory] = victory,
            [ChallengeScreenState.Defeat] = defeat,
        };
    }

    public static (int X, int Y) ActionForType(ChallengeType type) =>
        TypeActions.TryGetValue(type, out (int X, int Y) action)
            ? action
            : throw new ArgumentOutOfRangeException(nameof(type));

    public static (int X, int Y)? ActionFor(ChallengeScreenState state, ImageFrame image) => state switch
    {
        ChallengeScreenState.GameModeSelector => (480, 205),
        ChallengeScreenState.ChallengeAvailable => ChallengeAvailableAction(image),
        ChallengeScreenState.PreviewReady => ActionButtonDetector.ActionFor(image, "challenge_preview_start"),
        ChallengeScreenState.PostMatchPreview => ActionButtonDetector.ActionFor(image, "challenge_change_mode"),
        ChallengeScreenState.Prestart => StartDialogDetector.ActionFor(image),
        ChallengeScreenState.Victory => ActionButtonDetector.ActionFor(image, "challenge_victory_close"),
        ChallengeScreenState.Defeat => ActionButtonDetector.ActionFor(image, "defeat"),
        _ => null,
    };

    public static double Threshold(ChallengeScreenState state) => state switch
    {
        ChallengeScreenState.GameModeSelector => 0.76,
        ChallengeScreenState.ChallengeList => 0.74,
        ChallengeScreenState.ChallengeAvailable => 0.76,
        ChallengeScreenState.PreviewReady => 0.78,
        ChallengeScreenState.PostMatchPreview => 0.76,
        ChallengeScreenState.Prestart => 0.82,
        ChallengeScreenState.Victory => 0.74,
        ChallengeScreenState.Defeat => 0.75,
        _ => 1,
    };

    private static double GameModeSelectorScore(ImageFrame image)
    {
        double title = ColorFraction(image, ChallengeTileTitleRegion, IsChallengeYellow);
        double storyTitle = ColorFraction(image, StoryTileTitleRegion, IsCyan);
        double raidTitle = ColorFraction(image, RaidTileTitleRegion, IsRaidRed);
        double expeditionTitle = ColorFraction(image, ExpeditionTileTitleRegion, IsExpeditionGreen);
        double dark = ColorFraction(image, ChallengeTileRegion, IsDark);
        if (title < 0.018 || storyTitle < 0.008 || raidTitle < 0.006 || expeditionTitle < 0.006 || dark < 0.30) return 0;
        return Math.Clamp(
            0.72 +
            0.10 * Ramp(title, 0.018, 0.08) +
            0.05 * Ramp(storyTitle, 0.008, 0.04) +
            0.04 * Ramp(raidTitle, 0.006, 0.035) +
            0.04 * Ramp(expeditionTitle, 0.006, 0.035) +
            0.05 * Ramp(dark, 0.30, 0.75),
            0,
            1);
    }

    private static (int X, int Y)? ChallengeAvailableAction(ImageFrame image)
    {
        (int X, int Y)? selectStage = ActionButtonDetector.ActionFor(image, "challenge_select_stage");
        if (selectStage is not null) return selectStage;
        (int X, int Y)? matchmaking = ActionButtonDetector.ActionFor(image, "challenge_enter_matchmaking");
        if (matchmaking is null) return null;
        return (Math.Max(320, matchmaking.Value.X - 163), matchmaking.Value.Y);
    }

    private static double PanelScore(ImageFrame image)
    {
        double header = ColorFraction(image, PanelHeaderRegion, IsCyan);
        double top = BestHorizontalLineFraction(image, PanelTopEdgeRegion, IsCyan);
        double leftRailDark = ColorFraction(image, PanelLeftRailRegion, IsDark);
        double right = BestVerticalLineFraction(image, PanelRightEdgeRegion, IsCyan);
        double bottom = BestHorizontalLineFraction(image, PanelBottomEdgeRegion, IsCyan);
        if (header < 0.025 || top < 0.55 || leftRailDark < 0.52 || right < 0.35 || bottom < 0.30) return 0;
        return Math.Clamp(
            0.66 +
            0.08 * Ramp(header, 0.025, 0.12) +
            0.06 * Ramp(top, 0.55, 0.95) +
            0.04 * Ramp(leftRailDark, 0.52, 0.85) +
            0.09 * Ramp(right, 0.35, 0.90) +
            0.07 * Ramp(bottom, 0.30, 0.90),
            0,
            1);
    }

    private static double ChallengeListScore(ImageFrame image, double panelScore)
    {
        if (panelScore == 0) return 0;
        int supportedRows = 0;
        bool bottomRowSupported = false;
        foreach (int centerY in new[] { 244, 335, 425 })
        {
            ScreenRegion icon = new(348, centerY - 24, 46, 48);
            double cyan = ColorFraction(image, icon, IsCyan);
            bool supported = cyan is >= 0.028 and <= 0.20;
            if (supported) supportedRows++;
            if (centerY == 425) bottomRowSupported = supported;
        }
        // A blue reward overlay can saturate these same coordinates. Real selector
        // compass icons are sparse outlines, and the fixed third row is always present.
        if (supportedRows < 2 || !bottomRowSupported) return 0;
        return Math.Clamp(0.70 * panelScore + 0.30 * (supportedRows / 3d), 0, 1);
    }

    private static double DarkPartyPanelScore(ImageFrame image)
    {
        ScreenRegion partyPanel = new(410, 160, 355, 250);
        double dark = ColorFraction(image, partyPanel, IsDark);
        return dark < 0.45 ? 0 : 0.65 + 0.35 * Ramp(dark, 0.45, 0.85);
    }

    private static double VictoryRosterScore(ImageFrame image)
    {
        int supportedRows = 0;
        const int rowHeight = 50;
        for (int row = 0; row < 5; row++)
        {
            ScreenRegion region = new(VictoryRosterRegion.X, VictoryRosterRegion.Y + row * rowHeight, VictoryRosterRegion.Width, rowHeight);
            if (ColorFraction(image, region, IsRewardYellow) >= 0.008) supportedRows++;
        }
        return supportedRows < 3 ? 0 : 0.62 + 0.38 * ((supportedRows - 3) / 2d);
    }

    private static double ColorFraction(ImageFrame image, ScreenRegion region, Func<byte, byte, byte, bool> predicate)
    {
        if (!region.FitsWithin(image.Width, image.Height)) return 0;
        int matching = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (predicate(image.Pixels[pixel], image.Pixels[pixel + 1], image.Pixels[pixel + 2])) matching++;
            }
        }
        return (double)matching / (region.Width * region.Height);
    }

    private static double BestVerticalLineFraction(ImageFrame image, ScreenRegion region, Func<byte, byte, byte, bool> predicate)
    {
        double best = 0;
        for (int x = region.X; x < region.Right; x++)
        {
            int matching = 0;
            for (int y = region.Y; y < region.Bottom; y++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (predicate(image.Pixels[pixel], image.Pixels[pixel + 1], image.Pixels[pixel + 2])) matching++;
            }
            best = Math.Max(best, (double)matching / region.Height);
        }
        return best;
    }

    private static double BestHorizontalLineFraction(ImageFrame image, ScreenRegion region, Func<byte, byte, byte, bool> predicate)
    {
        double best = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            int matching = 0;
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (predicate(image.Pixels[pixel], image.Pixels[pixel + 1], image.Pixels[pixel + 2])) matching++;
            }
            best = Math.Max(best, (double)matching / region.Width);
        }
        return best;
    }

    private static bool IsCyan(byte red, byte green, byte blue) =>
        green >= 75 && blue >= 85 && green - red >= 20 && blue - red >= 28;

    private static bool IsChallengeYellow(byte red, byte green, byte blue) =>
        red >= 120 && green >= 85 && blue <= 100 && red - blue >= 45 && green - blue >= 25;

    private static bool IsRaidRed(byte red, byte green, byte blue) =>
        red >= 105 && red - green >= 35 && red - blue >= 25;

    private static bool IsExpeditionGreen(byte red, byte green, byte blue) =>
        green >= 75 && green - red >= 18 && green - blue >= 8;

    private static bool IsRewardYellow(byte red, byte green, byte blue) =>
        red >= 120 && green >= 80 && blue <= 95 && red - blue >= 45;

    private static bool IsDark(byte red, byte green, byte blue) =>
        red + green + blue <= 150;

    private static double Ramp(double value, double minimum, double maximum) =>
        Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static void ValidateClient(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || image.Width != ClientWidth || image.Height != ClientHeight)
        {
            throw new InvalidDataException($"Challenge detector input must be an RGB {ClientWidth} by {ClientHeight} client image.");
        }
    }
}
