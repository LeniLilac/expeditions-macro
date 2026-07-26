using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Settings;

public static class GameSettingsScreenDetector
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;
    public const double CanonicalScaleTolerance = 0.015;
    public const int SettingsButtonX = 232;
    public const int SettingsButtonY = 34;
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
            ScreenRegion? interior =
                InteriorForScale(scale);
            if (interior is null)
            {
                continue;
            }

            double dark =
                GameSettingsVisionMetrics.ColorFraction(
                    image,
                    interior.Value,
                    GameSettingsVisionMetrics.IsDark);
            if (dark < 0.54)
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
                0.58 +
                0.24 *
                GameSettingsVisionMetrics.Ramp(
                    dark,
                    0.54,
                    0.90) +
                0.18 *
                GameSettingsVisionMetrics.Ramp(
                    close.Count,
                    70,
                    300),
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
            Math.Abs(panel.UiScale - 1) >
                CanonicalScaleTolerance)
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

        ScreenRegion sample =
            new(layout.X - 8, layout.Y - 8, 17, 17);
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

        return TraceToggle(
            new GameSettingToggleMatch(
                setting,
                state,
                confidence,
                layout.X,
                layout.Y));
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
                    GameSettingsVisionMetrics.IsScrollbarBlue);
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

    private static ScreenRegion? InteriorForScale(
        double scale)
    {
        int left = (int)Math.Round(
            GameSettingsLayout.PanelCenterX -
            271 * scale);
        int right = (int)Math.Round(
            GameSettingsLayout.PanelCenterX +
            271 * scale);
        int top = (int)Math.Round(
            GameSettingsLayout.PanelCenterY -
            154 * scale);
        int bottom = (int)Math.Round(
            GameSettingsLayout.PanelCenterY +
            154 * scale);
        ScreenRegion interior = new(
            Math.Clamp(
                left + (int)(120 * scale),
                0,
                ClientWidth - 1),
            Math.Clamp(
                top + (int)(38 * scale),
                0,
                ClientHeight - 1),
            Math.Clamp(
                right - left - (int)(140 * scale),
                1,
                ClientWidth),
            Math.Clamp(
                bottom - top - (int)(58 * scale),
                1,
                ClientHeight));
        return interior.FitsWithin(
            ClientWidth,
            ClientHeight)
            ? interior
            : null;
    }

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
