using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Settings;

public enum RobloxSettingsButtonState
{
    None = 0,
    Closed = 1,
    Selected = 2,
}

public readonly record struct RobloxSettingsButtonMatch(
    RobloxSettingsButtonState State,
    double Confidence,
    int ActionX,
    int ActionY)
{
    public bool Available =>
        State != RobloxSettingsButtonState.None;
}

public static class RobloxSettingsButtonDetector
{
    public const int NoVoiceActionX = 232;
    public const int VoiceActionX = 276;
    public const int ActionY = 34;

    private static readonly int[] ActionXs =
    [
        NoVoiceActionX,
        VoiceActionX,
    ];

    public static RobloxSettingsButtonMatch Detect(
        ImageFrame image)
    {
        Validate(image);
        RobloxSettingsButtonMatch best = default;
        foreach (int actionX in ActionXs)
        {
            RobloxSettingsButtonMatch candidate =
                DetectAt(image, actionX);
            if (candidate.Confidence > best.Confidence)
            {
                best = candidate;
            }
        }

        VisionTrace.Emit(
            "roblox_settings_button",
            best.State.ToString(),
            best.Confidence,
            new
            {
                best.ActionX,
                best.ActionY,
            });
        return best;
    }

    private static RobloxSettingsButtonMatch DetectAt(
        ImageFrame image,
        int actionX)
    {
        int core = 0;
        int innerRing = 0;
        int middleRing = 0;
        int outerRing = 0;
        int outside = 0;
        for (int offsetY = -17;
             offsetY <= 17;
             offsetY++)
        {
            for (int offsetX = -17;
                 offsetX <= 17;
                 offsetX++)
            {
                int radiusSquared =
                    offsetX * offsetX +
                    offsetY * offsetY;
                if (radiusSquared >= 18 * 18 ||
                    !IsOpaqueNeutral(
                        image,
                        actionX + offsetX,
                        ActionY + offsetY))
                {
                    continue;
                }

                if (radiusSquared < 3 * 3)
                {
                    core++;
                }
                else if (radiusSquared < 6 * 6)
                {
                    innerRing++;
                }
                else if (radiusSquared < 9 * 9)
                {
                    middleRing++;
                }
                else if (radiusSquared < 12 * 12)
                {
                    outerRing++;
                }
                else
                {
                    outside++;
                }
            }
        }

        int glyphPixels =
            core +
            innerRing +
            middleRing +
            outerRing;
        bool outline =
            glyphPixels is >= 96 and <= 142 &&
            core is >= 5 and <= 11 &&
            innerRing is >= 16 and <= 28 &&
            middleRing is >= 36 and <= 58 &&
            outerRing is >= 30 and <= 50;
        bool selected =
            glyphPixels is >= 190 and <= 275 &&
            core is >= 3 and <= 12 &&
            innerRing is >= 45 and <= 75 &&
            middleRing is >= 95 and <= 140 &&
            outerRing is >= 40 and <= 75;
        if ((!outline && !selected) || outside > 6)
        {
            return default;
        }

        double confidence = Math.Clamp(
            0.76 +
            0.12 *
            GameSettingsVisionMetrics.Ramp(
                selected ? glyphPixels : 142 - glyphPixels,
                selected ? 190 : 0,
                selected ? 250 : 46) +
            0.12 *
            GameSettingsVisionMetrics.Ramp(
                6 - outside,
                0,
                6),
            0,
            1);
        return new RobloxSettingsButtonMatch(
            selected
                ? RobloxSettingsButtonState.Selected
                : RobloxSettingsButtonState.Closed,
            confidence,
            actionX,
            ActionY);
    }

    private static bool IsOpaqueNeutral(
        ImageFrame image,
        int x,
        int y)
    {
        int pixel = (y * image.Width + x) * 3;
        byte red = image.Pixels[pixel];
        byte green = image.Pixels[pixel + 1];
        byte blue = image.Pixels[pixel + 2];
        int minimum = Math.Min(red, Math.Min(green, blue));
        int maximum = Math.Max(red, Math.Max(green, blue));
        return minimum >= 165 &&
            maximum - minimum <= 55;
    }

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width !=
                GameSettingsScreenDetector.ClientWidth ||
            image.Height !=
                GameSettingsScreenDetector.ClientHeight)
        {
            throw new InvalidDataException(
                $"Settings button detector input must be an RGB {GameSettingsScreenDetector.ClientWidth} by {GameSettingsScreenDetector.ClientHeight} client image.");
        }
    }
}
