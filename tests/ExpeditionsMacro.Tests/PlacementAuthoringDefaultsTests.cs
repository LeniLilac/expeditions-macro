using System.Text.Json;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementAuthoringDefaultsTests
{
    [Fact]
    public void PlacementDefaults_RoundTripAsSetupAuthoringState()
    {
        PlacementModel current = Placement() with
        {
            DefaultTargetingPriority =
                UnitTargetingPriority.Boss,
            DefaultAutoUpgradePriority =
                UnitAutoUpgradePriority.Priority5,
        };

        PlacementModel restored =
            JsonSerializer.Deserialize<PlacementModel>(
                JsonSerializer.Serialize(
                    current,
                    JsonFileStore.Options),
                JsonFileStore.Options) ??
            throw new InvalidDataException(
                "Could not deserialize placement.");

        Assert.Equal(
            UnitTargetingPriority.Boss,
            restored.DefaultTargetingPriority);
        Assert.Equal(
            UnitAutoUpgradePriority.Priority5,
            restored.DefaultAutoUpgradePriority);
        restored.Validate();
    }

    [Fact]
    public void PlacementModel_RejectsInvalidAuthoringDefaults()
    {
        PlacementModel invalid = Placement() with
        {
            DefaultAutoUpgradePriority =
                (UnitAutoUpgradePriority)7,
        };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                invalid.Validate);

        Assert.Contains(
            "Default Auto Upgrade priority",
            error.Message,
            StringComparison.Ordinal);
    }

    private static PlacementModel Placement() =>
        new()
        {
            Id = $"placement-{Guid.NewGuid():N}",
            Name = "Placement",
            ClientWidth = 808,
            ClientHeight = 611,
            Steps =
            [
                new PlacementStep
                {
                    UnitKey = 1,
                    X = 340,
                    Y = 300,
                    DelayAfterMilliseconds = 900,
                    Phase = PlacementPhase.BeforeStart,
                },
            ],
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = 2,
            },
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
