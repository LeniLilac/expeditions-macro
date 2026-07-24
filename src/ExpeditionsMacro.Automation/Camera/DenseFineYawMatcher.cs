using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Camera;

internal static class DenseFineYawMatcher
{
    public static FineYawMatch FindBest(
        IReadOnlyList<FineYawReference> references,
        ImageFrame current)
    {
        if (references.Count == 0)
        {
            throw new ArgumentException(
                "At least one fine-yaw reference is required.",
                nameof(references));
        }

        FineYawReference best = references[0];
        double bestScore = double.NegativeInfinity;
        foreach (FineYawReference candidate in references)
        {
            double score = Correlation(
                candidate.Thumbnail,
                current);
            if (score <= bestScore) continue;
            best = candidate;
            bestScore = score;
        }
        return new FineYawMatch(best.Offset, bestScore);
    }

    private static double Correlation(
        ImageFrame reference,
        ImageFrame current)
    {
        if (reference.Format != PixelFormat.Gray8 ||
            current.Format != PixelFormat.Gray8 ||
            reference.Width != current.Width ||
            reference.Height != current.Height)
        {
            throw new ArgumentException(
                "Dense fine-yaw correlation requires equal grayscale images.");
        }

        double referenceMean = 0;
        double currentMean = 0;
        for (int index = 0;
             index < reference.Pixels.Length;
             index++)
        {
            referenceMean += reference.Pixels[index];
            currentMean += current.Pixels[index];
        }
        referenceMean /= reference.Pixels.Length;
        currentMean /= current.Pixels.Length;

        double covariance = 0;
        double referenceSquare = 0;
        double currentSquare = 0;
        for (int index = 0;
             index < reference.Pixels.Length;
             index++)
        {
            double first =
                reference.Pixels[index] - referenceMean;
            double second =
                current.Pixels[index] - currentMean;
            covariance += first * second;
            referenceSquare += first * first;
            currentSquare += second * second;
        }
        double denominator =
            Math.Sqrt(referenceSquare * currentSquare);
        if (denominator <= 1e-9) return 0;
        return Math.Clamp(
            (covariance / denominator + 1) / 2,
            0,
            1);
    }
}
