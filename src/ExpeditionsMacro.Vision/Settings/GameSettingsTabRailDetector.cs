using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Settings;

internal readonly record struct GameSettingsTabRailMatch(
    bool Available,
    int NeutralRows,
    double SelectedScore);

internal static class GameSettingsTabRailDetector
{
    public static GameSettingsTabRailMatch Detect(
        ImageFrame image,
        double scale)
    {
        ScreenRegion[] regions =
            GameSettingsLayout.TabRegions
                .Select(region =>
                    ScaleRegion(region, scale))
                .ToArray();
        int neutralRows = regions.Count(region =>
            GameSettingsVisionMetrics.ColorFraction(
                image,
                region,
                GameSettingsVisionMetrics
                    .IsNeutralTabSurface) >= 0.55);
        double[] selectedScores = regions
            .Select(region =>
                GameSettingsVisionMetrics.ColorFraction(
                    image,
                    region,
                    GameSettingsVisionMetrics.IsCyan))
            .OrderByDescending(score => score)
            .ToArray();
        double selectedScore = selectedScores[0];
        double selectedGap =
            selectedScore - selectedScores[1];
        return new GameSettingsTabRailMatch(
            neutralRows >= 7 &&
            selectedScore >= 0.25 &&
            selectedGap >= 0.12,
            neutralRows,
            selectedScore);
    }

    private static ScreenRegion ScaleRegion(
        ScreenRegion region,
        double scale)
    {
        (int left, int top) =
            ScalePoint(region.X, region.Y, scale);
        (int right, int bottom) =
            ScalePoint(
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
}
