using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Tests;

public sealed class CameraModelCompatibilityTests
{
    [Fact]
    public async Task CameraModelV1_LoadExplainsHowToReplaceThePresetReference()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            string directory = Path.Combine(
                paths.CameraModels,
                "legacy-map-camera");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "manifest.json"),
                """
                {
                  "schema_version": 1,
                  "id": "legacy-map-camera",
                  "name": "Legacy map camera",
                  "region": {
                    "x": 120,
                    "y": 80,
                    "width": 300,
                    "height": 220
                  },
                  "client_width": 808,
                  "client_height": 611,
                  "baseline_score": 1.0,
                  "success_threshold": 0.75,
                  "coarse_step_pixels": 16,
                  "full_yaw_pixels": 640,
                  "settle_milliseconds": 200,
                  "created_at": "2026-01-01T00:00:00Z"
                }
                """);
            CameraModelRepository repository = new(paths);

            InvalidDataException error =
                await Assert.ThrowsAsync<InvalidDataException>(
                    () => repository.LoadAsync("legacy-map-camera"));

            Assert.Contains(
                "Legacy map camera",
                error.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "obsolete format schema 1",
                error.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "choose a current camera model",
                error.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "Save preset",
                error.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "Rebuilding the Macro plan alone",
                error.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "missing required properties",
                error.Message,
                StringComparison.Ordinal);
            Assert.Empty(await repository.ListAsync());
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }
}
