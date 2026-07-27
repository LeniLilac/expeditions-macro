using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class CameraPreparationExecutionPolicyTests
{
    [Fact]
    public void FastNoAlign_IsTheOnlyExecutablePreparationMode()
    {
        Assert.True(
            CameraPreparationExecutionPolicy
                .IsSupportedForExecution(
                    CameraPreparationMode.FastNoAlign));
        Assert.False(
            CameraPreparationExecutionPolicy
                .IsSupportedForExecution(
                    CameraPreparationMode.CameraModel));

        CameraPreparationExecutionPolicy
            .ValidateForExecution(
                CameraPreparationMode.FastNoAlign);
    }

    [Fact]
    public void CameraModel_StopsExecutionWithActionableGuidance()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => CameraPreparationExecutionPolicy
                    .ValidateForExecution(
                        CameraPreparationMode.CameraModel,
                        "The selected preset"));

        Assert.Equal(
            "The selected preset uses the retired Camera Model workflow. " +
            "Open Placement Setup and choose or create a Fast no align setup before running the macro.",
            error.Message);
    }

    [Fact]
    public async Task
        LegacyCameraModelPreset_RemainsLoadableEditableAndUnchanged()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            string path = Path.Combine(
                paths.Presets,
                "legacy-camera.json");
            await File.WriteAllTextAsync(
                path,
                """
                {
                  "schema_version": 1,
                  "id": "legacy-camera",
                  "name": "Legacy Camera Preset",
                  "map_number": 1,
                  "difficulty": 2,
                  "camera_preparation_mode": "camera_model",
                  "camera_model_id": "camera-one",
                  "placement_model_id": "placement-one"
                }
                """);
            PresetRepository repository = new(paths);

            ExpeditionPreset listed =
                Assert.Single(await repository.ListAsync());
            Assert.Equal(
                CameraPreparationMode.CameraModel,
                listed.CameraPreparationMode);
            Assert.Equal(
                "camera-one",
                listed.CameraModelId);

            ExpeditionPreset edited = listed with
            {
                Name = "Renamed Legacy Camera Preset",
            };
            await repository.SaveAsync(edited);
            ExpeditionPreset loaded =
                Assert.IsType<ExpeditionPreset>(
                    await repository.LoadAsync(
                        edited.Id));

            Assert.Equal(
                "Renamed Legacy Camera Preset",
                loaded.Name);
            Assert.Equal(
                CameraPreparationMode.CameraModel,
                loaded.CameraPreparationMode);
            Assert.Equal(
                "camera-one",
                loaded.CameraModelId);
            Assert.Throws<InvalidDataException>(
                () => CameraPreparationExecutionPolicy
                    .ValidateForExecution(
                        loaded.CameraPreparationMode,
                        "The selected preset"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public void MacroPlanWithLegacyPresetTask_RemainsValidAndVisible()
    {
        MacroPlan plan = new()
        {
            Id = "legacy-plan",
            Name = "Legacy Plan",
            Tasks =
            [
                new MacroTaskDefinition
                {
                    Id = "legacy-camera-task",
                    Kind = MacroTaskKind.Expedition,
                    PresetId = "legacy-camera",
                    Name = "Legacy Camera Task",
                },
            ],
        };

        plan.Validate();

        MacroTaskDefinition task =
            Assert.Single(plan.Tasks);
        Assert.Equal(
            "legacy-camera",
            task.PresetId);
        Assert.Equal(
            "Legacy Camera Task",
            task.Name);
    }
}
