using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Camera;

public sealed record CameraWorldReadinessResult(
    bool IsReady,
    double ReferenceTexture,
    double CurrentTexture,
    double RelativeTexture);

public static class CameraWorldReadiness
{
    private const double MinimumTexture = 0.08;
    private const double MinimumRelativeTexture = 0.20;

    public static CameraWorldReadinessResult Evaluate(
        ImageFrame reference,
        ImageFrame current)
    {
        Validate(reference, nameof(reference));
        Validate(current, nameof(current));
        if (reference.Width != current.Width || reference.Height != current.Height)
        {
            throw new ArgumentException(
                "Camera readiness frames must use the same dimensions.");
        }

        double referenceTexture = TextureScore(reference);
        double currentTexture = TextureScore(current);
        double relativeTexture = referenceTexture <= 0.001
            ? 0
            : currentTexture / referenceTexture;
        bool ready =
            currentTexture >= MinimumTexture &&
            relativeTexture >= MinimumRelativeTexture;
        return new CameraWorldReadinessResult(
            ready,
            referenceTexture,
            currentTexture,
            relativeTexture);
    }

    private static double TextureScore(ImageFrame gray)
    {
        double mean = gray.Pixels.Average(value => (double)value);
        double variance = gray.Pixels.Average(
            value => (value - mean) * (value - mean));
        double deviation = Math.Sqrt(variance);
        double gradientSum = 0;
        int gradientCount = 0;
        for (int y = 1; y < gray.Height; y++)
        {
            int row = y * gray.Width;
            int previousRow = row - gray.Width;
            for (int x = 1; x < gray.Width; x++)
            {
                gradientSum += Math.Abs(
                    gray.Pixels[row + x] -
                    gray.Pixels[row + x - 1]);
                gradientSum += Math.Abs(
                    gray.Pixels[row + x] -
                    gray.Pixels[previousRow + x]);
                gradientCount += 2;
            }
        }

        double gradient =
            gradientCount == 0 ? 0 : gradientSum / gradientCount;
        return Math.Clamp(
            0.55 * deviation / 42 +
            0.45 * gradient / 24,
            0,
            1);
    }

    private static void Validate(ImageFrame frame, string parameterName)
    {
        if (frame.Format != PixelFormat.Gray8 ||
            frame.Width <= 0 ||
            frame.Height <= 0)
        {
            throw new ArgumentException(
                "Camera readiness requires a non-empty grayscale image.",
                parameterName);
        }
    }
}
