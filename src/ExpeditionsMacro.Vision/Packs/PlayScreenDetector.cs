using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class PlayScreenDetector
{
    private static readonly double[] TitleScales = [0.75, 0.80, 0.85, 0.90, 0.95, 1.00, 1.05, 1.10, 1.15, 1.20, 1.25, 1.30, 1.35];
    private static readonly ScreenRegion FooterRegion = new(608, 234, 176, 14);

    private readonly record struct PlayMatch(double Score, AdaptiveRegionMatch Title);

    public static double Score(ImageFrame titleReference, ScreenRegion titleRegion, ImageFrame image) =>
        Find(titleReference, titleRegion, image)?.Score ?? 0;

    public static (int X, int Y)? ActionFor(
        ImageFrame titleReference,
        ScreenRegion titleRegion,
        ImageFrame image,
        int configuredX,
        int configuredY)
    {
        if (Find(titleReference, titleRegion, image) is not PlayMatch match) return null;
        return match.Title.MapPoint(configuredX, configuredY);
    }

    private static PlayMatch? Find(ImageFrame titleReference, ScreenRegion titleRegion, ImageFrame image)
    {
        AdaptiveRegionMatch title = AdaptiveUiMatcher.Find(
            titleReference,
            image,
            titleRegion,
            horizontalRadius: 105,
            verticalRadius: 80,
            requestedScales: TitleScales);

        // The map name, artwork, reward icons, and avatar all vary by account and
        // game update. The outlined teal "Expedition" title is the invariant signal.
        // Require both its edge template and its distinctive color so unrelated text
        // cannot turn a weak template correlation into a Play-screen match.
        if (title.Correlation < 0.38 || title.Score < 0.48) return null;
        double tealFraction = TealFraction(image, title.MatchedRegion);
        if (tealFraction < 0.025) return null;

        // A title-shaped patch can occur in the detailed lobby scenery. The real
        // Expedition tile also has a wide, nearly black footer with two pieces of
        // neutral text (progress and "Click to view") at a fixed offset from the
        // title. This second signal excludes scenery without depending on the map
        // name, tile artwork, avatar, or reward icons.
        ScreenRegion footer = title.MapRegion(FooterRegion);
        if (!footer.FitsWithin(image.Width, image.Height)) return null;
        (double darkFraction, double neutralTextFraction) = FooterFractions(image, footer);
        if (darkFraction < 0.62 || neutralTextFraction < 0.012) return null;

        double quality = Math.Clamp(
            0.40 * Ramp(title.Correlation, 0.38, 0.82) +
            0.30 * Ramp(title.Score, 0.48, 0.86) +
            0.12 * Ramp(tealFraction, 0.025, 0.16) +
            0.10 * Ramp(darkFraction, 0.62, 0.90) +
            0.08 * Ramp(neutralTextFraction, 0.012, 0.06),
            0,
            1);
        return new PlayMatch(0.82 + 0.18 * quality, title);
    }

    private static (double Dark, double NeutralText) FooterFractions(ImageFrame image, ScreenRegion region)
    {
        if (image.Format != PixelFormat.Rgb24 || !region.FitsWithin(image.Width, image.Height)) return (0, 0);
        int dark = 0;
        int neutralText = 0;
        int total = checked(region.Width * region.Height);
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
                if (maximum < 80) dark++;
                if (minimum > 140 && maximum - minimum < 55) neutralText++;
            }
        }
        return ((double)dark / total, (double)neutralText / total);
    }

    private static double TealFraction(ImageFrame image, ScreenRegion region)
    {
        if (image.Format != PixelFormat.Rgb24 || !region.FitsWithin(image.Width, image.Height)) return 0;
        int teal = 0;
        int total = checked(region.Width * region.Height);
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                if (green >= 85 && blue >= 55 && green - red >= 18 && blue - red >= 8) teal++;
            }
        }
        return (double)teal / total;
    }

    private static double Ramp(double value, double minimum, double maximum) =>
        Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
}
