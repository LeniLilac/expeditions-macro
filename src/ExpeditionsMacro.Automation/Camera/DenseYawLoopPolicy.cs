namespace ExpeditionsMacro.Automation.Camera;

internal static class DenseYawLoopPolicy
{
    public const double FingerprintThreshold = 0.94;
    public const double FineMatchThreshold = 0.96;

    public static bool IsFineNeighborhoodReturn(
        double fingerprintScore,
        double fineMatchScore) =>
        fingerprintScore >= FingerprintThreshold &&
        fineMatchScore >= FineMatchThreshold;

    public static bool IsReturn(
        double fingerprintScore,
        double directScore,
        double fineMatchScore,
        double exactReturnThreshold) =>
        directScore >= exactReturnThreshold ||
        IsFineNeighborhoodReturn(
            fingerprintScore,
            fineMatchScore);
}
