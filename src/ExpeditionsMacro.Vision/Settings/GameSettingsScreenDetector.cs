using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Settings;

public static class GameSettingsScreenDetector
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;
    public const double MinimumCanonicalUiScale = 0.98;
    public const double MaximumCanonicalUiScale = 1.02;
    public const int UnitsScrollbarTopY = 185;
    public const int UnitsScrollbarBottomY = 452;

    public static GameSettingsPanelMatch DetectPanel(
        ImageFrame image)
    {
        Validate(image);
        SettingsColorComponent[] closeCandidates =
            GameSettingsVisionMetrics.FindComponents(
                    image,
                    GameSettingsLayout.CloseSearch,
                    GameSettingsVisionMetrics.IsRed)
                .Where(component =>
                    component.Width is >= 12 and <= 32 &&
                    component.Height is >= 12 and <= 32 &&
                    component.Count >= 70 &&
                    IsPlausibleCloseX(component.CenterX))
                .OrderByDescending(component => component.Count)
                .ToArray();
        if (closeCandidates.Length == 0)
        {
            return TracePanel(
                new GameSettingsPanelMatch(
                    false,
                    false,
                    0,
                    0,
                    0,
                    0));
        }
        foreach (SettingsColorComponent close in
                 closeCandidates)
        {
            double closeX = close.CenterX;
            double closeY = close.CenterY;
            double scale =
                (closeX -
                 GameSettingsLayout.PanelCenterX) /
                GameSettingsLayout.BaseCloseOffsetX;
            GameSettingsTabRailMatch tabRail =
                GameSettingsTabRailDetector.Detect(
                    image,
                    scale);
            if (!tabRail.Available)
            {
                continue;
            }

            double expectedCloseY =
                GameSettingsLayout.PanelCenterY +
                GameSettingsLayout.BaseCloseOffsetY *
                scale;
            bool settled =
                Math.Abs(
                    closeY -
                    expectedCloseY) <= 8;
            double confidence = Math.Clamp(
                0.72 +
                0.10 *
                GameSettingsVisionMetrics.Ramp(
                    close.Count,
                    70,
                    300) +
                0.10 *
                GameSettingsVisionMetrics.Ramp(
                    tabRail.SelectedScore,
                    0.25,
                    0.70) +
                0.08 *
                GameSettingsVisionMetrics.Ramp(
                    tabRail.NeutralRows,
                    7,
                    8),
                0,
                1);
            return TracePanel(
                new GameSettingsPanelMatch(
                    confidence >= 0.72,
                    settled,
                    confidence,
                    scale,
                    (int)Math.Round(closeX),
                    (int)Math.Round(closeY)));
        }

        return TracePanel(
            new GameSettingsPanelMatch(
                false,
                false,
                0,
                0,
                0,
                0));
    }

    private static bool IsPlausibleCloseX(
        double closeX)
    {
        double scale =
            (closeX -
             GameSettingsLayout.PanelCenterX) /
            GameSettingsLayout.BaseCloseOffsetX;
        return scale is >= 0.72 and <= 1.28;
    }

    public static GameSettingsPageMatch DetectPage(
        ImageFrame image)
    {
        Validate(image);
        GameSettingsPanelMatch panel = DetectPanel(image);
        if (!panel.Visible ||
            !panel.Settled ||
            !IsCanonicalUiScale(panel.UiScale))
        {
            return TracePage(
                new GameSettingsPageMatch(
                    GameSettingsPage.None,
                    0));
        }

        (double Score, GameSettingsPage Page)[] ranked =
            GameSettingsLayout.TabRegions
                .Select((region, index) =>
                    (
                        GameSettingsVisionMetrics.ColorFraction(
                            image,
                            region,
                            GameSettingsVisionMetrics.IsCyan),
                        GameSettingsLayout.TabPages[index]))
                .OrderByDescending(candidate => candidate.Item1)
                .ToArray();
        double gap = ranked[0].Score - ranked[1].Score;
        GameSettingsPageMatch match =
            ranked[0].Score >= 0.25 && gap >= 0.12
                ? new GameSettingsPageMatch(
                    ranked[0].Page,
                    Math.Clamp(
                        0.65 +
                        0.25 *
                        GameSettingsVisionMetrics.Ramp(
                            ranked[0].Score,
                            0.25,
                            0.70) +
                        0.10 *
                        GameSettingsVisionMetrics.Ramp(
                            gap,
                            0.12,
                            0.40),
                        0,
                        1))
                : new GameSettingsPageMatch(
                    GameSettingsPage.None,
                    ranked[0].Score);
        return TracePage(match);
    }

    public static bool IsCanonicalUiScale(
        double uiScale) =>
        double.IsFinite(uiScale) &&
        uiScale >=
            MinimumCanonicalUiScale - 1e-9 &&
        uiScale <=
            MaximumCanonicalUiScale + 1e-9;

    public static GameSettingToggleMatch DetectToggle(
        ImageFrame image,
        RequiredGameSetting setting)
    {
        Validate(image);
        GameSettingLayoutEntry layout = LayoutFor(setting);
        GameSettingsPageMatch page = DetectPage(image);
        if (page.Page != layout.Page ||
            layout.Page == GameSettingsPage.Units &&
            !HasRequiredUnitsScroll(image, layout))
        {
            return TraceToggle(
                new GameSettingToggleMatch(
                    setting,
                    GameSettingToggleState.Unknown,
                    0,
                    layout.X,
                    layout.Y));
        }

        GameSettingToggleMatch[] candidates =
            CandidateRows(layout)
                .Select(y =>
                    DetectToggleAt(
                        image,
                        setting,
                        layout.X,
                        y))
                .OrderByDescending(
                    candidate => candidate.Confidence)
                .ToArray();
        return TraceToggle(candidates[0]);
    }

    private static IEnumerable<int> CandidateRows(
        GameSettingLayoutEntry layout)
    {
        yield return layout.Y;
        if (layout.AlternateY is int alternate &&
            alternate != layout.Y)
        {
            yield return alternate;
        }
    }

    private static GameSettingToggleMatch DetectToggleAt(
        ImageFrame image,
        RequiredGameSetting setting,
        int x,
        int y)
    {
        return Enumerable.Range(-2, 5)
            .Select(offsetY =>
                MeasureToggleAt(
                    image,
                    setting,
                    x,
                    y + offsetY,
                    y))
            .OrderByDescending(
                candidate => candidate.Confidence)
            .First();
    }

    private static GameSettingToggleMatch MeasureToggleAt(
        ImageFrame image,
        RequiredGameSetting setting,
        int x,
        int sampleY,
        int actionY)
    {
        ScreenRegion sample =
            new(x - 8, sampleY - 8, 17, 17);
        double enabled =
            GameSettingsVisionMetrics.ColorFraction(
                image,
                sample,
                GameSettingsVisionMetrics.IsGreen);
        double disabled =
            GameSettingsVisionMetrics.ColorFraction(
                image,
                sample,
                GameSettingsVisionMetrics.IsRed);
        GameSettingToggleState state;
        double confidence;
        if (enabled >= 0.50 &&
            enabled - disabled >= 0.30)
        {
            state = GameSettingToggleState.Enabled;
            confidence = Math.Clamp(
                0.72 +
                0.28 *
                GameSettingsVisionMetrics.Ramp(
                    enabled,
                    0.50,
                    0.92),
                0,
                1);
        }
        else if (disabled >= 0.33 &&
                 disabled - enabled >= 0.25)
        {
            state = GameSettingToggleState.Disabled;
            confidence = Math.Clamp(
                0.72 +
                0.28 *
                GameSettingsVisionMetrics.Ramp(
                    disabled,
                    0.33,
                    0.60),
                0,
                1);
        }
        else
        {
            state = GameSettingToggleState.Unknown;
            confidence = Math.Max(enabled, disabled);
        }

        return new GameSettingToggleMatch(
            setting,
            state,
            confidence,
            x,
            actionY);
    }

    public static GameSettingsScrollbarThumb?
        FindUnitsScrollbarThumb(ImageFrame image)
    {
        Validate(image);
        if (DetectPage(image).Page !=
            GameSettingsPage.Units)
        {
            return null;
        }

        List<(int X, int StartY, int EndY)> runs = [];
        for (int x = 665; x <= 671; x++)
        {
            (int StartY, int EndY) run =
                GameSettingsVisionMetrics.LongestVerticalRun(
                    image,
                    x,
                    180,
                    455,
                    GameSettingsVisionMetrics.IsScrollbarBlue,
                    maximumGap: 1);
            if (run.EndY - run.StartY + 1 >= 120)
            {
                runs.Add((x, run.StartY, run.EndY));
            }
        }

        if (runs.Count < 2) return null;
        return new GameSettingsScrollbarThumb(
            (int)Math.Round(runs.Average(run => run.X)),
            (int)Math.Round(runs.Average(run => run.StartY)),
            (int)Math.Round(runs.Average(run => run.EndY)));
    }

    public static (int X, int Y) PageAction(
        GameSettingsPage page)
    {
        int index = Array.IndexOf(
            GameSettingsLayout.TabPages,
            page);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                page,
                "The settings page has no tab action.");
        }

        ScreenRegion region =
            GameSettingsLayout.TabRegions[index];
        return (
            region.X + region.Width / 2,
            region.Y + region.Height / 2);
    }

    public static bool RequiresUnitsBottom(
        RequiredGameSetting setting) =>
        LayoutFor(setting).UnitsBottom;

    public static GameSettingsPage PageFor(
        RequiredGameSetting setting) =>
        LayoutFor(setting).Page;

    private static bool HasRequiredUnitsScroll(
        ImageFrame image,
        GameSettingLayoutEntry layout)
    {
        GameSettingsScrollbarThumb? thumb =
            FindUnitsScrollbarThumb(image);
        return thumb is not null &&
            (layout.UnitsBottom
                ? thumb.Value.IsAtBottom
                : thumb.Value.IsAtTop);
    }

    private static GameSettingLayoutEntry LayoutFor(
        RequiredGameSetting setting) =>
        GameSettingsLayout.Settings.TryGetValue(
            setting,
            out GameSettingLayoutEntry layout)
            ? layout
            : throw new ArgumentOutOfRangeException(
                nameof(setting),
                setting,
                "The required game setting has no visual layout.");

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != ClientWidth ||
            image.Height != ClientHeight)
        {
            throw new InvalidDataException(
                $"Game settings detector input must be an RGB {ClientWidth} by {ClientHeight} client image.");
        }
    }

    private static GameSettingsPanelMatch TracePanel(
        GameSettingsPanelMatch match)
    {
        VisionTrace.Emit(
            "game_settings_panel",
            match.Visible
                ? match.Settled ? "Settled" : "Moving"
                : "None",
            match.Confidence,
            new
            {
                match.UiScale,
                match.CloseX,
                match.CloseY,
            });
        return match;
    }

    private static GameSettingsPageMatch TracePage(
        GameSettingsPageMatch match)
    {
        VisionTrace.Emit(
            "game_settings_page",
            match.Page.ToString(),
            match.Confidence);
        return match;
    }

    private static GameSettingToggleMatch TraceToggle(
        GameSettingToggleMatch match)
    {
        VisionTrace.Emit(
            "game_setting_toggle",
            match.State.ToString(),
            match.Confidence,
            new
            {
                Setting = match.Setting.ToString(),
                match.ActionX,
                match.ActionY,
            });
        return match;
    }
}
