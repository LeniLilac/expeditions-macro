using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Camera;

internal static class CameraCoarseAtlasEvidencePolicy
{
    // Fingerprints may authorize one bounded feedback group, never alignment
    // success. The structural floor excludes textureless lookalikes, while
    // remote isolation rejects a repeated map neighborhood.
    internal const double MinimumDenseFingerprint = 0.94;
    internal const double MinimumDenseFingerprintIsolation = 0.06;
    internal const double MinimumDenseStructuralEvidence = 0.20;
    internal const double MinimumConstrainedStructuralEvidence = 0.15;

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

    public static bool IsReliableWithinKnownTransition(
        CameraYawAtlasKind atlasKind,
        AtlasMatch match,
        double minimumRegisteredScore)
    {
        if (match.Score >= minimumRegisteredScore) return true;
        // A verified prior position plus a bounded physical input supplies
        // the locality that global fingerprint isolation normally proves.
        // This may authorize another correction only; it never completes
        // alignment or model setup without direct goal verification.
        return atlasKind == CameraYawAtlasKind.DenseSweep &&
               match.Score >=
               MinimumConstrainedStructuralEvidence &&
               match.FingerprintScore >=
               MinimumDenseFingerprint;
    }
}
