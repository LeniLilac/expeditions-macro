using System.Globalization;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.DetectorViewer.Models;

public sealed record PixelSample(
    int X,
    int Y,
    byte Red,
    byte Green,
    byte Blue,
    double Luminance,
    double Hue,
    double Saturation,
    double Value)
{
    public string Hex =>
        $"#{Red:X2}{Green:X2}{Blue:X2}";

    public string Summary =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"({X}, {Y})  {Hex}  RGB {Red}, {Green}, {Blue}  Luma {Luminance:0.0}  HSV {Hue:0}°, {Saturation:0}%, {Value:0}%");
}

public static class PixelInspector
{
    public static PixelSample? Sample(
        ImageFrame image,
        int x,
        int y)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Format != PixelFormat.Rgb24 ||
            x < 0 ||
            y < 0 ||
            x >= image.Width ||
            y >= image.Height)
        {
            return null;
        }
        int pixel =
            (y * image.Width + x) * 3;
        byte red = image.Pixels[pixel];
        byte green = image.Pixels[pixel + 1];
        byte blue = image.Pixels[pixel + 2];
        double r = red / 255d;
        double g = green / 255d;
        double b = blue / 255d;
        double maximum = Math.Max(r, Math.Max(g, b));
        double minimum = Math.Min(r, Math.Min(g, b));
        double delta = maximum - minimum;
        double hue =
            delta == 0
                ? 0
                : maximum == r
                    ? 60 * (((g - b) / delta) % 6)
                    : maximum == g
                        ? 60 * ((b - r) / delta + 2)
                        : 60 * ((r - g) / delta + 4);
        if (hue < 0)
        {
            hue += 360;
        }
        double saturation =
            maximum == 0
                ? 0
                : delta / maximum;
        double luminance =
            0.2126 * red +
            0.7152 * green +
            0.0722 * blue;
        return new PixelSample(
            x,
            y,
            red,
            green,
            blue,
            luminance,
            hue,
            saturation * 100,
            maximum * 100);
    }
}
