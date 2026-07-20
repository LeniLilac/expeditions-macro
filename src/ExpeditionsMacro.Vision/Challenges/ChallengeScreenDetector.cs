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
    ChallengeListUnavailable,
    ChallengeAvailable,
    ChallengeCooldown,
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

    private static readonly ScreenRegion ChallengeTileTitleRegion = new(360, 150, 130, 55);
    private static readonly ScreenRegion StoryTileTitleRegion = new(360, 50, 130, 55);
    private static readonly ScreenRegion RaidTileTitleRegion = new(570, 50, 120, 55);
    private static readonly ScreenRegion ExpeditionTileTitleRegion = new(600, 150, 140, 55);
    private static readonly ScreenRegion GameModeRowDividerRegion = new(355, 143, 440, 20);
    private static readonly ScreenRegion GameModeColumnDividerRegion = new(562, 45, 24, 215);
    private static readonly ScreenRegion CompactPanelHeaderRegion = new(90, 120, 220, 80);
    private static readonly ScreenRegion CompactPanelTopEdgeRegion = new(280, 146, 380, 14);
    private static readonly ScreenRegion CompactPanelLeftRailRegion = new(125, 180, 150, 245);
    private static readonly ScreenRegion CompactPanelRightEdgeRegion = new(655, 140, 35, 330);
    private static readonly ScreenRegion CompactPanelBottomEdgeRegion = new(115, 440, 575, 30);
    private static readonly ScreenRegion LargePanelHeaderRegion = new(70, 110, 250, 100);
    private static readonly ScreenRegion LargePanelTopEdgeRegion = new(230, 120, 500, 45);
    private static readonly ScreenRegion LargePanelLeftRailRegion = new(105, 170, 170, 275);
    private static readonly ScreenRegion LargePanelRightEdgeRegion = new(630, 120, 105, 370);
    private static readonly ScreenRegion LargePanelBottomEdgeRegion = new(90, 425, 645, 65);
    private static readonly ScreenRegion ChallengeListFirstSeparatorRegion = new(265, 282, 435, 15);
    private static readonly ScreenRegion ChallengeListSecondSeparatorRegion = new(265, 373, 435, 15);
    private static readonly ScreenRegion RegularChallengeTabRegion = new(135, 180, 135, 75);
    private static readonly ScreenRegion VictoryRosterRegion = new(510, 180, 150, 275);

    private static readonly IReadOnlyDictionary<ChallengeType, (int X, int Y)> TypeActions =
        new Dictionary<ChallengeType, (int X, int Y)>
        {
            // Click the stable map thumbnail at the left of each row. Reward icons
            // open item tooltips and are especially dense on the Sprite row.
            [ChallengeType.Trait] = (320, 244),
            [ChallengeType.Stat] = (320, 335),
            [ChallengeType.Sprite] = (320, 425),
        };

    public static ChallengeScreenMatch Detect(ImageFrame image)
    {
        IReadOnlyDictionary<ChallengeScreenState, double> scores = ScoreStates(image);
        ChallengeScreenState[] priority =
        [
            ChallengeScreenState.Defeat,
            ChallengeScreenState.ChallengeAvailable,
            ChallengeScreenState.ChallengeCooldown,
            ChallengeScreenState.ChallengeListUnavailable,
            ChallengeScreenState.Victory,
            ChallengeScreenState.Prestart,
            ChallengeScreenState.PreviewReady,
            ChallengeScreenState.PostMatchPreview,
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
        selectStage = Math.Max(selectStage, ActionButtonDetector.Score(image, "challenge_select_stage_wide"));
        double enterMatchmaking = ActionButtonDetector.Score(image, "challenge_enter_matchmaking");
        double previewStart = ActionButtonDetector.Score(image, "challenge_preview_start");
        double changeMode = ActionButtonDetector.Score(image, "challenge_change_mode");
        double partyStart = ActionButtonDetector.Score(image, "challenge_party_start");
        double partyDisband = ActionButtonDetector.Score(image, "challenge_party_disband");
        double victoryParty = ActionButtonDetector.Score(image, "challenge_victory_party");
        double victoryClose = ActionButtonDetector.Score(image, "challenge_victory_close");
        double defeat = TerminalScreenDetector.Score(image, "defeat");
        double prestart = RewardScreenDetector.HasHeader(image) ? 0 : StartDialogDetector.Score(image);
        double challengeList = ChallengeListScore(image, panel);
        double challengeListUnavailable = ChallengeListUnavailableScore(image, panel);
        double availability = Math.Max(selectStage, enterMatchmaking);
        double challengeAvailable = panel < 0.55 || availability == 0 ? 0 : Math.Clamp(0.48 * panel + 0.52 * availability, 0, 1);
        double cooldownFooter = CooldownFooterScore(image);
        double challengeCooldown = panel < 0.55 || availability > 0 || cooldownFooter == 0
            ? 0
            : Math.Clamp(0.52 * panel + 0.48 * cooldownFooter, 0, 1);
        double navigationPreview = previewStart == 0 || changeMode == 0
            ? 0
            : Math.Clamp(0.55 * previewStart + 0.45 * changeMode, 0, 1);
        double privatePartyPreview = partyStart == 0 || partyDisband == 0
            ? 0
            : Math.Clamp(0.55 * partyStart + 0.45 * partyDisband, 0, 1);
        double preview = Math.Max(navigationPreview, privatePartyPreview);
        double postMatchPreview = previewStart > 0 || changeMode == 0 ? 0 : Math.Clamp(0.72 * changeMode + 0.28 * DarkPartyPanelScore(image), 0, 1);
        double victory = victoryClose == 0 || victoryParty == 0
            ? 0
            : Math.Clamp(0.30 * panel + 0.25 * victoryClose + 0.30 * victoryParty + 0.15 * VictoryRosterScore(image), 0, 1);

        return new Dictionary<ChallengeScreenState, double>
        {
            [ChallengeScreenState.None] = 0,
            [ChallengeScreenState.GameModeSelector] = GameModeSelectorScore(image),
            [ChallengeScreenState.ChallengeList] = challengeList,
            [ChallengeScreenState.ChallengeListUnavailable] = challengeListUnavailable,
            [ChallengeScreenState.ChallengeAvailable] = challengeAvailable,
            [ChallengeScreenState.ChallengeCooldown] = challengeCooldown,
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

    public static (int X, int Y) TerminalCloseAction(ImageFrame image) =>
        ActionButtonDetector.ActionFor(image, "challenge_victory_close") ?? (657, 163);

    public static (int X, int Y) DefeatRetryAction(ImageFrame image) =>
        ActionButtonDetector.ActionFor(image, "defeat") ?? (225, 438);

    public static (int X, int Y) OpenPlayAction() => (147, 584);

    public static (int X, int Y)? ActionFor(ChallengeScreenState state, ImageFrame image) => state switch
    {
        ChallengeScreenState.GameModeSelector => (480, 205),
        ChallengeScreenState.ChallengeAvailable => ChallengeAvailableAction(image),
        ChallengeScreenState.ChallengeCooldown => (308, 437),
        ChallengeScreenState.PreviewReady => PreviewReadyAction(image),
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
        ChallengeScreenState.ChallengeListUnavailable => 0.80,
        ChallengeScreenState.ChallengeAvailable => 0.76,
        ChallengeScreenState.ChallengeCooldown => 0.76,
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
        double rowDivider = BestHorizontalLineFraction(image, GameModeRowDividerRegion, IsDark);
        double columnDivider = BestVerticalLineFraction(image, GameModeColumnDividerRegion, IsDark);
        if (title < 0.018 ||
            storyTitle < 0.008 ||
            raidTitle < 0.006 ||
            expeditionTitle < 0.006 ||
            rowDivider < 0.80 ||
            columnDivider < 0.48)
        {
            return 0;
        }
        return Math.Clamp(
            0.72 +
            0.08 * Ramp(title, 0.018, 0.08) +
            0.04 * Ramp(storyTitle, 0.008, 0.04) +
            0.04 * Ramp(raidTitle, 0.006, 0.035) +
            0.04 * Ramp(expeditionTitle, 0.006, 0.035) +
            0.04 * Ramp(rowDivider, 0.80, 0.99) +
            0.04 * Ramp(columnDivider, 0.48, 0.85),
            0,
            1);
    }

    private static (int X, int Y)? ChallengeAvailableAction(ImageFrame image)
    {
        (int X, int Y)? selectStage = ActionButtonDetector.ActionFor(image, "challenge_select_stage");
        selectStage ??= ActionButtonDetector.ActionFor(image, "challenge_select_stage_wide");
        if (selectStage is not null) return selectStage;
        (int X, int Y)? matchmaking = ActionButtonDetector.ActionFor(image, "challenge_enter_matchmaking");
        if (matchmaking is null) return null;
        return (Math.Max(320, matchmaking.Value.X - 163), matchmaking.Value.Y);
    }

    private static (int X, int Y)? PreviewReadyAction(ImageFrame image) =>
        ActionButtonDetector.Score(image, "challenge_party_disband") > 0
            ? ActionButtonDetector.ActionFor(image, "challenge_party_start")
            : ActionButtonDetector.ActionFor(image, "challenge_preview_start");

    private static double PanelScore(ImageFrame image) => PanelScore(image, IsCyan);

    private static double PanelScore(ImageFrame image, Func<byte, byte, byte, bool> predicate) => Math.Max(
        PanelScore(
            image,
            CompactPanelHeaderRegion,
            CompactPanelTopEdgeRegion,
            CompactPanelLeftRailRegion,
            CompactPanelRightEdgeRegion,
            CompactPanelBottomEdgeRegion,
            predicate),
        PanelScore(
            image,
            LargePanelHeaderRegion,
            LargePanelTopEdgeRegion,
            LargePanelLeftRailRegion,
            LargePanelRightEdgeRegion,
            LargePanelBottomEdgeRegion,
            predicate));

    private static double PanelScore(
        ImageFrame image,
        ScreenRegion headerRegion,
        ScreenRegion topEdgeRegion,
        ScreenRegion leftRailRegion,
        ScreenRegion rightEdgeRegion,
        ScreenRegion bottomEdgeRegion,
        Func<byte, byte, byte, bool> predicate)
    {
        double header = ColorFraction(image, headerRegion, predicate);
        double top = BestHorizontalLineFraction(image, topEdgeRegion, predicate);
        double leftRailDark = ColorFraction(image, leftRailRegion, IsDark);
        double right = BestVerticalLineFraction(image, rightEdgeRegion, predicate);
        double bottom = BestHorizontalLineFraction(image, bottomEdgeRegion, predicate);
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
        // Hovering one selector row can dim the other compass emblems from cyan to
        // gray. The two long separators remain stable across both observed panel
        // scales and distinguish the three-row selector from a Challenge detail view.
        double separatorEvidence = ChallengeListSeparatorEvidence(image);

        // A blue reward overlay can saturate the old emblem coordinates. Preserve
        // sparse cyan emblems as independent evidence, but never require their color.
        double emblemEvidence = supportedRows >= 2 && bottomRowSupported ? supportedRows / 3d : 0;
        double rowEvidence = Math.Max(separatorEvidence, emblemEvidence);
        if (rowEvidence == 0) return 0;
        return Math.Clamp(0.70 * panelScore + 0.30 * rowEvidence, 0, 1);
    }

    private static double ChallengeListUnavailableScore(ImageFrame image, double activePanelScore)
    {
        if (activePanelScore > 0) return 0;
        double neutralPanel = PanelScore(image, IsNeutralGray);
        double separatorEvidence = ChallengeListSeparatorEvidence(image);
        double regularNeutral = ColorFraction(image, RegularChallengeTabRegion, IsNeutralGray);
        if (neutralPanel == 0 || separatorEvidence == 0 || regularNeutral < 0.25) return 0;
        return Math.Clamp(
            0.65 * neutralPanel +
            0.25 * separatorEvidence +
            0.10 * Ramp(regularNeutral, 0.25, 0.60),
            0,
            1);
    }

    private static double ChallengeListSeparatorEvidence(ImageFrame image)
    {
        double first = BestHorizontalLineFraction(image, ChallengeListFirstSeparatorRegion, IsNeutralGray);
        double second = BestHorizontalLineFraction(image, ChallengeListSecondSeparatorRegion, IsNeutralGray);
        return first >= 0.65 && second >= 0.65 ? (first + second) / 2 : 0;
    }

    private static double DarkPartyPanelScore(ImageFrame image)
    {
        ScreenRegion partyPanel = new(410, 160, 355, 250);
        double dark = ColorFraction(image, partyPanel, IsDark);
        return dark < 0.45 ? 0 : 0.65 + 0.35 * Ramp(dark, 0.45, 0.85);
    }

    private static double CooldownFooterScore(ImageFrame image)
    {
        ScreenRegion footer = new(345, 418, 325, 38);
        double neutral = ColorFraction(image, footer, IsNeutralGray);
        return neutral < 0.45 ? 0 : 0.72 + 0.28 * Ramp(neutral, 0.45, 0.68);
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

    private static bool IsNeutralGray(byte red, byte green, byte blue)
    {
        int maximum = Math.Max(red, Math.Max(green, blue));
        int minimum = Math.Min(red, Math.Min(green, blue));
        return maximum - minimum <= 28 && maximum is >= 35 and <= 190;
    }

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
