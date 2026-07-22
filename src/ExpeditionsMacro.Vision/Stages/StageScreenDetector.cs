using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Diagnostics;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Vision.Stages;

public enum StageMode
{
    Story,
    Raid,
}

public enum StageScreenState
{
    None,
    GameModeSelector,
    StorySelector,
    RaidSelector,
    StoryDetail,
    RaidDetail,
    PreviewReady,
    Prestart,
    Victory,
    Defeat,
    PostMatchPreview,
}

public sealed record StageScreenMatch(StageScreenState State, double Confidence, int? ActionX = null, int? ActionY = null);

public static class StageScreenDetector
{
    private static readonly ScreenRegion StorySelectorHeader = new(15, 45, 205, 65);
    private static readonly ScreenRegion StorySelectorOcean = new(0, 285, 808, 270);
    private static readonly ScreenRegion RaidSelectorSky = new(0, 40, 808, 250);
    private static readonly ScreenRegion RaidSelectorOcean = new(0, 290, 808, 270);
    private static readonly ScreenRegion DetailHeader = new(80, 105, 270, 100);
    private static readonly ScreenRegion DetailTopEdge = new(105, 125, 600, 65);
    private static readonly ScreenRegion DetailPanel = new(120, 135, 575, 340);
    private static readonly ScreenRegion StageVictoryHeader = new(65, 105, 280, 105);
    private static readonly ScreenRegion StageVictoryTopEdge = new(100, 120, 610, 70);

    public static StageScreenMatch Detect(ImageFrame image)
    {
        ValidateClient(image);
        ChallengeScreenMatch shared = ChallengeScreenDetector.Detect(image);
        if (shared.State == ChallengeScreenState.Defeat)
        {
            return Trace(new StageScreenMatch(StageScreenState.Defeat, shared.Confidence, shared.ActionX, shared.ActionY));
        }

        double stageVictory = StageVictoryScore(image);
        if (stageVictory >= 0.78)
        {
            (int X, int Y)? close = ActionButtonDetector.ActionFor(image, "challenge_victory_close");
            return Trace(new StageScreenMatch(StageScreenState.Victory, stageVictory, close?.X, close?.Y));
        }

        StageScreenMatch? terminal = shared.State switch
        {
            ChallengeScreenState.Victory => new(StageScreenState.Victory, shared.Confidence, shared.ActionX, shared.ActionY),
            ChallengeScreenState.Prestart => new(StageScreenState.Prestart, shared.Confidence, shared.ActionX, shared.ActionY),
            ChallengeScreenState.PreviewReady => new(StageScreenState.PreviewReady, shared.Confidence, shared.ActionX, shared.ActionY),
            ChallengeScreenState.PostMatchPreview or ChallengeScreenState.PostMatchHud => new(StageScreenState.PostMatchPreview, shared.Confidence, shared.ActionX, shared.ActionY),
            ChallengeScreenState.GameModeSelector => new(StageScreenState.GameModeSelector, shared.Confidence),
            _ => null,
        };
        if (terminal is not null) return Trace(terminal);

        double stagePartyStart = ActionButtonDetector.Score(image, "stage_party_start");
        double stagePartyChangeMap = ActionButtonDetector.Score(image, "stage_party_change_map");
        double stagePartyDisband = ActionButtonDetector.Score(image, "stage_party_disband");
        if (stagePartyStart > 0 && stagePartyChangeMap > 0 && stagePartyDisband > 0)
        {
            (int X, int Y)? action = ActionButtonDetector.ActionFor(image, "stage_party_start");
            double confidence = Math.Clamp(
                0.40 * stagePartyStart + 0.30 * stagePartyChangeMap + 0.30 * stagePartyDisband,
                0,
                1);
            return Trace(new StageScreenMatch(StageScreenState.PreviewReady, confidence, action?.X, action?.Y));
        }

        double narrowSelectStage = ActionButtonDetector.Score(image, "stage_select_stage");
        double wideSelectStage = ActionButtonDetector.Score(image, "stage_select_stage_wide");
        double selectStage = Math.Max(narrowSelectStage, wideSelectStage);
        double matchmaking = ActionButtonDetector.Score(image, "stage_enter_matchmaking");
        double cyanDetail = DetailPanelScore(image, IsCyan);
        double greenDetail = DetailPanelScore(image, IsStoryGreen);
        double purpleDetail = DetailPanelScore(image, IsStoryPurple);
        double redDetail = DetailPanelScore(image, IsRaidRed);
        double storyDetail = Math.Max(cyanDetail, Math.Max(greenDetail, purpleDetail));
        double storyActionSupport = Math.Max(matchmaking, wideSelectStage);
        if (selectStage >= 0.65 && storyActionSupport >= 0.65 && storyDetail >= 0.68)
        {
            (int X, int Y)? action = SelectStageAction(image);
            return Trace(new StageScreenMatch(StageScreenState.StoryDetail, Math.Clamp(0.35 * selectStage + 0.25 * storyActionSupport + 0.40 * storyDetail, 0, 1), action?.X, action?.Y));
        }
        if (narrowSelectStage >= 0.65 && matchmaking >= 0.65 && redDetail >= 0.68)
        {
            (int X, int Y)? action = SelectStageAction(image);
            return Trace(new StageScreenMatch(StageScreenState.RaidDetail, Math.Clamp(0.35 * selectStage + 0.25 * matchmaking + 0.40 * redDetail, 0, 1), action?.X, action?.Y));
        }

        double raidSelector = RaidSelectorScore(image);
        if (raidSelector >= 0.72) return Trace(new StageScreenMatch(StageScreenState.RaidSelector, raidSelector, 145, 350));

        double storySelector = StorySelectorScore(image);
        return Trace(storySelector >= 0.72
            ? new StageScreenMatch(StageScreenState.StorySelector, storySelector)
            : new StageScreenMatch(StageScreenState.None, Math.Max(Math.Max(storySelector, raidSelector), Math.Max(storyDetail, redDetail))));
    }

    public static (int X, int Y) ModeTileAction(StageMode mode) => mode switch
    {
        // The reward icons on the right side of the Story card consume clicks to
        // show item tooltips. Use the stable map-copy area on the left instead.
        StageMode.Story => (420, 105),
        StageMode.Raid => (680, 105),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static (int X, int Y) StoryMapAction(ChallengeMapId map) => map switch
    {
        ChallengeMapId.SchoolGrounds => (145, 350),
        ChallengeMapId.FlowerForest => (390, 350),
        ChallengeMapId.RoseKingdom => (650, 350),
        ChallengeMapId.FairyKingForest => (400, 350),
        ChallengeMapId.KingsTomb => (680, 350),
        _ => throw new ArgumentOutOfRangeException(nameof(map)),
    };

    public static bool StoryMapRequiresLaterScroll(ChallengeMapId map) => map is ChallengeMapId.FairyKingForest or ChallengeMapId.KingsTomb;

    public static (int X, int Y) RaidMapAction => (145, 350);

    public static (int X, int Y) StoryRunAction(StoryRunKind kind, int actNumber) => kind switch
    {
        StoryRunKind.Act when actNumber is >= 1 and <= 5 => (173, 201 + (actNumber - 1) * 39),
        StoryRunKind.Infinite => (173, 398),
        StoryRunKind.Mastery => (173, 437),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static (int X, int Y) StoryDifficultyAction(bool hardMode) => hardMode ? (254, 252) : (216, 252);

    public static (int X, int Y) RaidActAction(RaidAct act) => act switch
    {
        RaidAct.Act1 => (173, 229),
        RaidAct.Act2 => (173, 318),
        RaidAct.Act3 => (173, 407),
        _ => throw new ArgumentOutOfRangeException(nameof(act)),
    };

    public static (int X, int Y)? SelectStageAction(ImageFrame image) =>
        ActionButtonDetector.ActionFor(image, "stage_select_stage_wide")
        ?? ActionButtonDetector.ActionFor(image, "stage_select_stage")
        ?? (256, 449);

    public static (int X, int Y) PreviewStartAction(ImageFrame image) =>
        ActionButtonDetector.ActionFor(image, "stage_party_start")
        ?? ActionButtonDetector.ActionFor(image, "challenge_party_start")
        ?? ActionButtonDetector.ActionFor(image, "challenge_preview_start")
        ?? (480, 418);

    private static double StorySelectorScore(ImageFrame image)
    {
        double header = ColorFraction(image, StorySelectorHeader, IsCyan);
        double oceanBlue = ColorFraction(image, StorySelectorOcean, IsStoryOcean);
        double bottomLine = BestHorizontalLineFraction(image, new ScreenRegion(0, 540, 808, 30), IsDark);
        if (header < 0.045 || oceanBlue < 0.20 || bottomLine < 0.45) return 0;
        return Math.Clamp(0.68 + 0.12 * Ramp(header, 0.045, 0.20) + 0.12 * Ramp(oceanBlue, 0.20, 0.75) + 0.08 * Ramp(bottomLine, 0.45, 0.95), 0, 1);
    }

    private static double RaidSelectorScore(ImageFrame image)
    {
        double header = ColorFraction(image, StorySelectorHeader, IsRaidRed);
        double darkRedSky = ColorFraction(image, RaidSelectorSky, IsRaidSky);
        double oceanBlue = ColorFraction(image, RaidSelectorOcean, IsStoryOcean);
        double bottomLine = BestHorizontalLineFraction(image, new ScreenRegion(0, 540, 808, 30), IsDark);
        if (header < 0.030 || darkRedSky < 0.20 || oceanBlue < 0.18 || bottomLine < 0.45) return 0;
        return Math.Clamp(
            0.68 +
            0.10 * Ramp(header, 0.030, 0.16) +
            0.08 * Ramp(darkRedSky, 0.20, 0.78) +
            0.08 * Ramp(oceanBlue, 0.18, 0.72) +
            0.06 * Ramp(bottomLine, 0.45, 0.95),
            0,
            1);
    }

    private static double DetailPanelScore(ImageFrame image, Func<byte, byte, byte, bool> accent)
    {
        double header = ColorFraction(image, DetailHeader, accent);
        double top = BestHorizontalLineFraction(image, DetailTopEdge, accent);
        double dark = ColorFraction(image, DetailPanel, IsDark);
        double close = ActionButtonDetector.Score(image, "challenge_victory_close");
        if (header < 0.018 || top < 0.45 || dark < 0.38 || close == 0) return 0;
        return Math.Clamp(
            0.58 +
            0.12 * Ramp(header, 0.018, 0.12) +
            0.12 * Ramp(top, 0.45, 0.95) +
            0.08 * Ramp(dark, 0.38, 0.78) +
            0.10 * close,
            0,
            1);
    }

    private static double StageVictoryScore(ImageFrame image)
    {
        double header = ColorFraction(image, StageVictoryHeader, IsCyan);
        double top = BestHorizontalLineFraction(image, StageVictoryTopEdge, IsCyan);
        double close = ActionButtonDetector.Score(image, "challenge_victory_close");
        double repeatStage = ActionButtonDetector.Score(image, "victory");
        if (header < 0.018 || top < 0.45 || close == 0 || repeatStage == 0) return 0;
        return Math.Clamp(
            0.40 * Ramp(header, 0.018, 0.11) +
            0.20 * Ramp(top, 0.45, 0.95) +
            0.20 * close +
            0.20 * repeatStage,
            0,
            1);
    }

    private static double ColorFraction(ImageFrame image, ScreenRegion region, Func<byte, byte, byte, bool> predicate)
    {
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

    private static bool IsCyan(byte red, byte green, byte blue) => green >= 75 && blue >= 85 && green - red >= 20 && blue - red >= 28;
    private static bool IsStoryGreen(byte red, byte green, byte blue) => green >= 75 && green - red >= 28 && green - blue >= 20;
    private static bool IsStoryPurple(byte red, byte green, byte blue) => red >= 65 && blue >= 85 && red - green >= 20 && blue - green >= 28;
    private static bool IsRaidRed(byte red, byte green, byte blue) => red >= 105 && red - green >= 35 && red - blue >= 25;
    private static bool IsRaidSky(byte red, byte green, byte blue) => red >= 35 && red <= 115 && red - green >= 24 && red - blue >= 18;
    private static bool IsStoryOcean(byte red, byte green, byte blue) => blue >= 105 && blue - red >= 35 && blue - green >= 5;
    private static bool IsDark(byte red, byte green, byte blue) => red + green + blue <= 180;
    private static double Ramp(double value, double minimum, double maximum) => Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static void ValidateClient(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || image.Width != ChallengeScreenDetector.ClientWidth || image.Height != ChallengeScreenDetector.ClientHeight)
        {
            throw new InvalidDataException("Stage detector input must be an RGB 808 by 611 client image.");
        }
    }

    private static StageScreenMatch Trace(StageScreenMatch match)
    {
        VisionTrace.Emit("stage_screen", match.State.ToString(), match.Confidence, new { match.ActionX, match.ActionY });
        return match;
    }
}
