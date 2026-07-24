using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Tests;

public sealed class CameraCoarseAtlasEvidencePolicyTests
{
    private const double ReportedMinimumRegisteredScore = 0.5921;

    [Fact]
    public void HybridMatch_LowRegistrationWithUniqueNeighborhood_IsReliable()
    {
        ImageFrame local = FingerprintFrame(seed: 0, phase: 0);
        ImageFrame[] atlas =
        [
            local,
            FingerprintFrame(seed: 0, phase: 2),
            FingerprintFrame(seed: 0, phase: 4),
            FingerprintFrame(seed: 1, phase: 0),
            FingerprintFrame(seed: 2, phase: 0),
            FingerprintFrame(seed: 3, phase: 0),
            FingerprintFrame(seed: 4, phase: 0),
            FingerprintFrame(seed: 5, phase: 0),
            local,
        ];

        CameraYawAtlasMatch visionMatch =
            CameraYawAtlasIndex.For(atlas).FindBest(
                FingerprintFrame(seed: 0, phase: 1));
        AtlasMatch match = new(
            visionMatch.Index,
            visionMatch.Score,
            visionMatch.FingerprintScore,
            visionMatch.FingerprintIsolation);

        Assert.InRange(match.Score, 0.20, 0.59);
        Assert.True(
            match.FingerprintScore >=
            CameraCoarseAtlasEvidencePolicy.MinimumDenseFingerprint);
        Assert.True(
            match.FingerprintIsolation >=
            CameraCoarseAtlasEvidencePolicy
                .MinimumDenseFingerprintIsolation);
        Assert.True(
            CameraCoarseAtlasEvidencePolicy.IsReliable(
                CameraYawAtlasKind.DenseSweep,
                match,
                ReportedMinimumRegisteredScore));
    }

    [Theory]
    [InlineData(43, 0.3901, 0.9905, 0.1574)]
    [InlineData(48, 0.3742, 0.9937, 0.0806)]
    [InlineData(54, 0.3483, 0.9918, 0.0704)]
    [InlineData(59, 0.6951, 0.9995, 0.1187)]
    [InlineData(65, 0.2363, 0.9702, 0.0836)]
    public void ReportedDenseAlignmentEvidence_RemainsEligibleForFeedback(
        int index,
        double registered,
        double fingerprint,
        double isolation)
    {
        AtlasMatch match = new(
            index,
            registered,
            fingerprint,
            isolation);

        bool reliable = CameraCoarseAtlasEvidencePolicy.IsReliable(
            CameraYawAtlasKind.DenseSweep,
            match,
            ReportedMinimumRegisteredScore);

        Assert.True(reliable);
    }

    [Theory]
    [InlineData(CameraYawAtlasKind.DenseSweep, 0.10, 0.99, 0.15)]
    [InlineData(CameraYawAtlasKind.DenseSweep, 0.39, 0.99, 0.02)]
    [InlineData(CameraYawAtlasKind.DenseSweep, 0.39, 0.95, 0.16)]
    [InlineData(CameraYawAtlasKind.PulseSteps, 0.39, 0.99, 0.16)]
    public void AmbiguousEvidence_DoesNotEarnCoarseInput(
        CameraYawAtlasKind kind,
        double registered,
        double fingerprint,
        double isolation)
    {
        AtlasMatch match = new(
            43,
            registered,
            fingerprint,
            isolation);

        bool reliable = CameraCoarseAtlasEvidencePolicy.IsReliable(
            kind,
            match,
            ReportedMinimumRegisteredScore);

        Assert.False(reliable);
    }

    [Fact]
    public void ExistingRegisteredEvidence_RemainsSufficient()
    {
        AtlasMatch match = new(
            12,
            ReportedMinimumRegisteredScore,
            FingerprintScore: 0,
            FingerprintIsolation: 0);

        bool reliable = CameraCoarseAtlasEvidencePolicy.IsReliable(
            CameraYawAtlasKind.PulseSteps,
            match,
            ReportedMinimumRegisteredScore);

        Assert.True(reliable);
    }

    private static ImageFrame FingerprintFrame(
        int seed,
        int phase)
    {
        const int width = 160;
        const int height = 100;
        byte[] pixels = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            int cellY = y / 20;
            for (int x = 0; x < width; x++)
            {
                int cellX = x / 16;
                int cell = cellY * 10 + cellX;
                int baseValue =
                    48 + (int)(Mix(unchecked(
                        (uint)(cell + seed * 101))) % 144);
                uint hash = Mix(unchecked((uint)(
                    x * 73856093 ^
                    y * 19349663 ^
                    cell * 83492791 ^
                    phase * 1640531513)));
                int texture =
                    (hash & 1) == 0 ? -28 : 28;
                pixels[y * width + x] = (byte)Math.Clamp(
                    baseValue + texture,
                    0,
                    255);
            }
        }
        return new ImageFrame(
            width,
            height,
            PixelFormat.Gray8,
            pixels,
            takeOwnership: true);
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }
}
