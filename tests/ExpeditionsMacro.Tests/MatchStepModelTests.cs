using System.Text.Json;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class MatchStepModelTests
{
    [Fact]
    public void MatchSteps_ValidateAndRoundTripWithAdvancedSettings()
    {
        PlacementStep placement = Placement();
        PlacementStep reconfigure = placement with
        {
            Kind = MatchStepKind.ReconfigureUnit,
            PlacementId = string.Empty,
            TargetPlacementId =
                placement.PlacementId,
            X = 0,
            Y = 0,
            ChangeTargetingPriority = true,
            TargetingPriority =
                UnitTargetingPriority.Strongest,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Off,
            AutoUpgradeAction =
                MatchAutoUpgradeAction.Disable,
        };
        PlacementStep delay = new()
        {
            Kind = MatchStepKind.Delay,
            UnitKey = 1,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            DelayDurationMilliseconds = 250,
        };
        PlacementStep upgrade = placement with
        {
            Kind = MatchStepKind.UpgradeUnit,
            PlacementId = string.Empty,
            TargetPlacementId =
                placement.PlacementId,
            X = 0,
            Y = 0,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Off,
            UpgradeCount = 4,
        };
        PlacementModel model = Model(
            [placement, reconfigure, delay, upgrade]) with
        {
            AdvancedSettings = new PlacementAdvancedSettings
            {
                Enabled = true,
                PlacementBurstDurationMilliseconds = 25,
                VerifySelectedUnitPanelBeforeActions = false,
                VerifyPrestartBeforeManualPlayback = false,
                ManualPlaybackStartDelayMilliseconds = 900,
            },
        };

        model.Validate();
        string json = JsonSerializer.Serialize(
            model,
            JsonFileStore.Options);
        PlacementModel restored =
            JsonSerializer.Deserialize<PlacementModel>(
                json,
                JsonFileStore.Options)!;

        restored.Validate();
        Assert.Equal(
            [
                MatchStepKind.Placement,
                MatchStepKind.ReconfigureUnit,
                MatchStepKind.Delay,
                MatchStepKind.UpgradeUnit,
            ],
            restored.Steps.Select(step => step.Kind));
        Assert.True(restored.AdvancedSettings.Enabled);
        Assert.False(
            restored.AdvancedSettings
                .VerifySelectedUnitPanelBeforeActions);
        Assert.Equal(
            900,
            restored.AdvancedSettings
                .ManualPlaybackStartDelayMilliseconds);
    }

    [Fact]
    public void UnitAction_MustTargetAnEarlierPlacement()
    {
        PlacementStep reconfigure = Placement() with
        {
            Kind = MatchStepKind.ReconfigureUnit,
            PlacementId = string.Empty,
            TargetPlacementId = "missing-placement",
            X = 0,
            Y = 0,
            ChangeTargetingPriority = true,
            TargetingPriority =
                UnitTargetingPriority.Last,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Off,
        };
        PlacementModel model = Model([reconfigure]);

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                model.Validate);

        Assert.Contains(
            "must target a placed unit that appears earlier",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ManualPlaybackStartPolicy_DefaultsSafeAndRequiresExplicitAdvancedOptOut()
    {
        PlacementModel safe = Model([Placement()]) with
        {
            ManualInputRecordingId =
                "recording-12345678",
        };
        PlacementModel optedOut = safe with
        {
            AdvancedSettings =
                new PlacementAdvancedSettings
                {
                    Enabled = true,
                    VerifyPrestartBeforeManualPlayback =
                        false,
                },
        };

        Assert.True(
            ManualPlaybackStartPolicy.RequiresPrestart(
                safe));
        Assert.False(
            ManualPlaybackStartPolicy.RequiresPrestart(
                optedOut));
    }

    private static PlacementStep Placement() => new()
    {
        Kind = MatchStepKind.Placement,
        PlacementId = "placed-unit-2",
        UnitKey = 2,
        X = 220,
        Y = 280,
        DelayAfterMilliseconds = 0,
        AutoUpgradePriority =
            UnitAutoUpgradePriority.Priority1,
    };

    private static PlacementModel Model(
        IReadOnlyList<PlacementStep> steps) => new()
        {
            Id = "match-step-model",
            Name = "Match steps",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
            CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode = PlacementTargetMode.Story,
                MapNumber = 1,
                StoryRunKind = StoryRunKind.Act,
                ActNumber = 1,
            },
            Steps = steps,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
