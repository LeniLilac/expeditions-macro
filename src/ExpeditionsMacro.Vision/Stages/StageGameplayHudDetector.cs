using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Diagnostics;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Vision.Stages;

public sealed record StageGameplayHudMatch(
    bool Visible,
    double Confidence,
    double HotbarSupport,
    double UnitManagerScore,
    double StageInfoScore);

public static class StageGameplayHudDetector
{
    private static readonly ScreenRegion HotbarRegion = new(235, 525, 350, 78);
    private const int HotbarColumns = 6;

    public static StageGameplayHudMatch Detect(ImageFrame image)
    {
        ValidateClient(image);
        double hotbar = HotbarSupport(image);
        double unitManager = ActionButtonDetector.Score(image, "gameplay_unit_manager");
        double stageInfo = ActionButtonDetector.Score(image, "gameplay_stage_info");
        double confidence = Math.Clamp(
            0.40 * hotbar +
            0.30 * unitManager +
            0.30 * stageInfo,
            0,
            1);
        bool visible =
            hotbar >= 0.50 &&
            unitManager >= 0.70 &&
            stageInfo >= 0.70;
        StageGameplayHudMatch match = new(
            visible,
            confidence,
            hotbar,
            unitManager,
            stageInfo);
        VisionTrace.Emit(
            "stage_gameplay_hud",
            visible ? "Visible" : "Hidden",
            confidence,
            new
            {
                HotbarSupport = hotbar,
                UnitManagerScore = unitManager,
                StageInfoScore = stageInfo,
            });
        return match;
    }

    private static double HotbarSupport(ImageFrame image)
    {
        int supported = 0;
        int baseWidth = HotbarRegion.Width / HotbarColumns;
        for (int column = 0; column < HotbarColumns; column++)
        {
            int left = HotbarRegion.X + column * baseWidth;
            int width = column == HotbarColumns - 1
                ? HotbarRegion.Right - left
                : baseWidth;
            if (SupportsUnitCard(image, new ScreenRegion(
                left,
                HotbarRegion.Y,
                width,
                HotbarRegion.Height)))
            {
                supported++;
            }
        }
        return (double)supported / HotbarColumns;
    }

    private static bool SupportsUnitCard(
        ImageFrame image,
        ScreenRegion region)
    {
        int dark = 0;
        int colorful = 0;
        int pixels = region.Width * region.Height;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                int maximum = Math.Max(red, Math.Max(green, blue));
                int minimum = Math.Min(red, Math.Min(green, blue));
                if (red + green + blue <= 210) dark++;
                if (maximum >= 90 && maximum - minimum >= 55) colorful++;
            }
        }

        return (double)dark / pixels >= 0.18 &&
            (double)colorful / pixels >= 0.08;
    }

    private static void ValidateClient(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != ChallengeScreenDetector.ClientWidth ||
            image.Height != ChallengeScreenDetector.ClientHeight)
        {
            throw new InvalidDataException(
                "Stage gameplay HUD detector input must be an RGB 808 by 611 client image.");
        }
    }
}
