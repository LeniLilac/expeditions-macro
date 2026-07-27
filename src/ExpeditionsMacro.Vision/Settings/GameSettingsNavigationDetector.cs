using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Settings;

public readonly record struct GameSettingsNavigationActionMatch(
    bool Available,
    double Confidence,
    int ActionX,
    int ActionY);

public readonly record struct GameSettingsUiScaleInputMatch(
    bool Available,
    bool Focused,
    double Confidence,
    int ActionX,
    int ActionY);

public static class GameSettingsNavigationDetector
{
    private const int UiScaleActionX = 370;
    private const int UiScaleActionY = 222;
    private const int CompactLayoutOffsetY = -14;

    private static readonly ScreenRegion UiScaleInputBody =
        new(355, 214, 18, 16);
    private static readonly ScreenRegion UiScaleSlider =
        new(382, 217, 70, 10);
    private static readonly ScreenRegion UiScaleFocusRing =
        new(346, 203, 37, 38);

    public static GameSettingsPageMatch DetectSelectedPage(
        ImageFrame image)
    {
        GameSettingsPanelMatch panel =
            GameSettingsScreenDetector.DetectPanel(image);
        return DetectSelectedPage(image, panel);
    }

    public static GameSettingsNavigationActionMatch
        DetectPageAction(
        ImageFrame image,
        GameSettingsPage page)
    {
        GameSettingsPanelMatch panel =
            GameSettingsScreenDetector.DetectPanel(image);
        if (!panel.Visible ||
            !panel.Settled)
        {
            return default;
        }

        int index = Array.IndexOf(
            GameSettingsLayout.TabPages,
            page);
        if (index < 0)
        {
            return default;
        }

        GameSettingsPageMatch selected =
            DetectSelectedPage(image, panel);
        if (selected.Page == GameSettingsPage.None)
        {
            return default;
        }

        ScreenRegion target = ScaleRegion(
            GameSettingsLayout.TabRegions[index],
            panel.UiScale);
        return new GameSettingsNavigationActionMatch(
            true,
            Math.Min(
                panel.Confidence,
                selected.Confidence),
            target.X + target.Width / 2,
            target.Y + target.Height / 2);
    }

    public static GameSettingsUiScaleInputMatch
        DetectUiScaleInput(
        ImageFrame image)
    {
        GameSettingsPanelMatch panel =
            GameSettingsScreenDetector.DetectPanel(image);
        GameSettingsPageMatch page =
            DetectSelectedPage(image, panel);
        if (page.Page !=
            GameSettingsPage.Miscellaneous)
        {
            return default;
        }

        return new[]
            {
                DetectUiScaleInputAt(
                    image,
                    panel,
                    page,
                    offsetY: 0),
                DetectUiScaleInputAt(
                    image,
                    panel,
                    page,
                    CompactLayoutOffsetY),
            }
            .OrderByDescending(
                match => match.Confidence)
            .First();
    }

    private static GameSettingsPageMatch DetectSelectedPage(
        ImageFrame image,
        GameSettingsPanelMatch panel)
    {
        if (!panel.Visible ||
            !panel.Settled)
        {
            return new GameSettingsPageMatch(
                GameSettingsPage.None,
                0);
        }

        (double Score, GameSettingsPage Page)[] ranked =
            GameSettingsLayout.TabRegions
                .Select((region, index) =>
                    (
                        Score:
                        GameSettingsVisionMetrics.ColorFraction(
                            image,
                            ScaleRegion(
                                region,
                                panel.UiScale),
                            GameSettingsVisionMetrics.IsCyan),
                        Page:
                        GameSettingsLayout.TabPages[index]))
                .OrderByDescending(
                    candidate => candidate.Score)
                .ToArray();
        double gap =
            ranked[0].Score -
            ranked[1].Score;
        if (ranked[0].Score < 0.25 ||
            gap < 0.12)
        {
            return new GameSettingsPageMatch(
                GameSettingsPage.None,
                ranked[0].Score);
        }

        return new GameSettingsPageMatch(
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
                1));
    }

    private static GameSettingsUiScaleInputMatch
        DetectUiScaleInputAt(
        ImageFrame image,
        GameSettingsPanelMatch panel,
        GameSettingsPageMatch page,
        int offsetY)
    {
        double dark =
            GameSettingsVisionMetrics.ColorFraction(
                image,
                ScaleRegion(
                    Offset(
                        UiScaleInputBody,
                        offsetY),
                    panel.UiScale),
                GameSettingsVisionMetrics.IsDark);
        double slider =
            GameSettingsVisionMetrics.ColorFraction(
                image,
                ScaleRegion(
                    Offset(
                        UiScaleSlider,
                        offsetY),
                    panel.UiScale),
                IsNeutralControl);
        double focus =
            GameSettingsVisionMetrics.ColorFraction(
                image,
                ScaleRegion(
                    Offset(
                        UiScaleFocusRing,
                        offsetY),
                    panel.UiScale),
                GameSettingsVisionMetrics.IsCyan);
        bool available =
            dark >= 0.70 &&
            slider >= 0.25;
        double confidence = available
            ? Math.Min(
                page.Confidence,
                Math.Clamp(
                    0.60 +
                    0.18 *
                    GameSettingsVisionMetrics.Ramp(
                        dark,
                        0.70,
                        0.95) +
                    0.22 *
                    GameSettingsVisionMetrics.Ramp(
                        slider,
                        0.25,
                        0.50),
                    0,
                    1))
            : Math.Min(dark, slider);
        (int actionX, int actionY) =
            ScalePoint(
                UiScaleActionX,
                UiScaleActionY + offsetY,
                panel.UiScale);
        return new GameSettingsUiScaleInputMatch(
            available,
            available && focus >= 0.06,
            confidence,
            available ? actionX : 0,
            available ? actionY : 0);
    }

    private static ScreenRegion ScaleRegion(
        ScreenRegion region,
        double scale)
    {
        (int left, int top) = ScalePoint(
            region.X,
            region.Y,
            scale);
        (int right, int bottom) = ScalePoint(
            region.Right,
            region.Bottom,
            scale);
        return new ScreenRegion(
            left,
            top,
            Math.Max(1, right - left),
            Math.Max(1, bottom - top));
    }

    private static (int X, int Y) ScalePoint(
        int x,
        int y,
        double scale) =>
        (
            (int)Math.Round(
                GameSettingsLayout.PanelCenterX +
                (x -
                 GameSettingsLayout.PanelCenterX) *
                scale),
            (int)Math.Round(
                GameSettingsLayout.PanelCenterY +
                (y -
                 GameSettingsLayout.PanelCenterY) *
                scale));

    private static ScreenRegion Offset(
        ScreenRegion region,
        int offsetY) =>
        new(
            region.X,
            region.Y + offsetY,
            region.Width,
            region.Height);

    private static bool IsNeutralControl(
        byte red,
        byte green,
        byte blue)
    {
        int maximum =
            Math.Max(red, Math.Max(green, blue));
        int minimum =
            Math.Min(red, Math.Min(green, blue));
        return maximum - minimum <= 20 &&
            red + green + blue >= 120;
    }
}
