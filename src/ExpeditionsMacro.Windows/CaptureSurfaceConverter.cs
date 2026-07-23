using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Windows;

internal static class CaptureSurfaceConverter
{
    public static ImageFrame ConvertScRgbRgba16ToRgb(
        byte[] rgba16,
        int surfaceWidth,
        int surfaceHeight,
        ScreenRegion crop)
    {
        if (rgba16.Length != checked(surfaceWidth * surfaceHeight * 8))
        {
            throw new ArgumentException("The FP16 RGBA buffer has an unexpected length.", nameof(rgba16));
        }
        if (crop.X < 0 || crop.Y < 0 || crop.Right > surfaceWidth || crop.Bottom > surfaceHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(crop), "The client crop must fit inside the captured surface.");
        }

        byte[] rgb = new byte[checked(crop.Width * crop.Height * 3)];
        int target = 0;
        for (int y = crop.Y; y < crop.Bottom; y++)
        {
            int source = checked((y * surfaceWidth + crop.X) * 8);
            for (int x = 0; x < crop.Width; x++, source += 8, target += 3)
            {
                float red = ReadFiniteHalf(rgba16, source);
                float green = ReadFiniteHalf(rgba16, source + 2);
                float blue = ReadFiniteHalf(rgba16, source + 4);
                ToneMapScRgb(ref red, ref green, ref blue);
                rgb[target] = LinearToSrgbByte(red);
                rgb[target + 1] = LinearToSrgbByte(green);
                rgb[target + 2] = LinearToSrgbByte(blue);
            }
        }
        return new ImageFrame(crop.Width, crop.Height, PixelFormat.Rgb24, rgb, takeOwnership: true);
    }

    private static float ReadFiniteHalf(byte[] source, int offset)
    {
        ushort bits = (ushort)(source[offset] | source[offset + 1] << 8);
        float value = (float)BitConverter.UInt16BitsToHalf(bits);
        return float.IsFinite(value) ? Math.Max(0f, value) : 0f;
    }

    private static void ToneMapScRgb(ref float red, ref float green, ref float blue)
    {
        // Windows Graphics Capture exposes HDR/WCG window pixels as linear scRGB,
        // where 1.0 is SDR reference white (80 nits) and HDR highlights can exceed 1.0.
        // Keep ordinary SDR pixels on the standard transfer curve and only
        // compress values that enter the highlight shoulder. This avoids changing the
        // detector input on SDR systems while preventing Auto HDR highlights from clipping.
        const float shoulderStart = 0.8f;
        const float scRgbAt1000Nits = 12.5f;
        const float shoulderScale = 2f;

        float luminance = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
        if (luminance > shoulderStart)
        {
            float capped = Math.Min(luminance, scRgbAt1000Nits);
            float numerator = 1f - MathF.Exp(-(capped - shoulderStart) / shoulderScale);
            float denominator = 1f - MathF.Exp(-(scRgbAt1000Nits - shoulderStart) / shoulderScale);
            float mapped = shoulderStart + (1f - shoulderStart) * numerator / denominator;
            float scale = mapped / luminance;
            red *= scale;
            green *= scale;
            blue *= scale;
        }

        float maximum = Math.Max(red, Math.Max(green, blue));
        if (maximum > 1f)
        {
            float scale = 1f / maximum;
            red *= scale;
            green *= scale;
            blue *= scale;
        }
    }

    private static byte LinearToSrgbByte(float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        float encoded = clamped <= 0.0031308f
            ? 12.92f * clamped
            : 1.055f * MathF.Pow(clamped, 1f / 2.4f) - 0.055f;
        return (byte)Math.Clamp((int)MathF.Round(encoded * 255f), 0, 255);
    }
}
