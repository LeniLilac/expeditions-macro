using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Camera;

internal static class CameraCoarseAtlasEvidencePolicy
{
    // Fingerprints may authorize one bounded feedback group, never alignment
    // success. The structural floor excludes textureless lookalikes, while
    // remote isolation rejects a repeated map neighborhood.
    internal const double MinimumDenseFingerprint = 0.96;
    internal const double MinimumDenseFingerprintIsolation = 0.06;
    internal const double MinimumDenseStructuralEvidence = 0.20;

    public static bool IsReliable(
        CameraYawAtlasKind atlasKind,
        AtlasMatch match,
        double minimumRegisteredScore)
    {
        if (match.Score >= minimumRegisteredScore) return true;
        return atlasKind == CameraYawAtlasKind.DenseSweep &&
               match.Score >= MinimumDenseStructuralEvidence &&
               match.FingerprintScore >= MinimumDenseFingerprint &&
               match.FingerprintIsolation >=
               MinimumDenseFingerprintIsolation;
    }
}
