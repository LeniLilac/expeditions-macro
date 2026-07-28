using System.Text.Json.Serialization;

namespace ExpeditionsMacro.Core.Models;

public sealed record PlacementStep
{
    public required int UnitKey { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public required int DelayAfterMilliseconds { get; init; }

    public PlacementPhase Phase { get; init; }

    public int DelayAfterStartMilliseconds { get; init; }

    public UnitTargetingPriority TargetingPriority { get; init; }

    [JsonPropertyName("auto_upgrade")]
    [JsonConverter(
        typeof(UnitAutoUpgradePriorityJsonConverter))]
    public UnitAutoUpgradePriority AutoUpgradePriority
    {
        get;
        init;
    } = UnitAutoUpgradePriority.Off;

    public void Validate(int clientWidth, int clientHeight)
    {
        if (UnitKey is < 0 or > 9) throw new InvalidDataException("Unit key must be 0 through 9.");
        if (X < 0 || Y < 0 || X >= clientWidth || Y >= clientHeight) throw new InvalidDataException("Placement coordinate falls outside the Roblox client.");
        if (DelayAfterMilliseconds < 0) throw new InvalidDataException("Placement delay cannot be negative.");
        if (DelayAfterStartMilliseconds < 0) throw new InvalidDataException("After-start placement delay cannot be negative.");
        if (!Enum.IsDefined(Phase)) throw new InvalidDataException("Placement phase is invalid.");
        if (!Enum.IsDefined(TargetingPriority))
        {
            throw new InvalidDataException(
                "Unit targeting priority is invalid.");
        }
        if (!Enum.IsDefined(AutoUpgradePriority))
        {
            throw new InvalidDataException(
                "Auto Upgrade priority is invalid.");
        }
    }
}

public sealed record PlacementCapture(
    int UnitKey,
    int X,
    int Y,
    int SelectedAtMilliseconds,
    int ClickedAtMilliseconds);

public sealed record PlacementModel
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumImpossibilityThresholdMinutes = 180;
    public const int DefaultPlacementAttempts = 1;
    public const int MaximumPlacementAttempts = 8;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required int ClientWidth { get; init; }

    public required int ClientHeight { get; init; }

    public required IReadOnlyList<PlacementStep> Steps { get; init; }

    public CameraPreparationMode CameraPreparationMode { get; init; }

    public PlacementTarget? Target { get; init; }

    public int TeamSlot { get; init; }

    public int PlacementIntervalMilliseconds { get; init; } =
        PlacementAuthoringRules
            .DefaultStepDelayMilliseconds;

    public int DefaultAfterStartDelayMilliseconds { get; init; } =
        PlacementAuthoringRules
            .DefaultAfterStartDelayMilliseconds;

    public int PlacementAttempts { get; init; } =
        DefaultPlacementAttempts;

    public string? ManualInputRecordingId { get; init; }

    public int ImpossibilityThresholdMinutes { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported placement model format.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Placement model identity is missing.");
        if (ClientWidth <= 0 || ClientHeight <= 0) throw new InvalidDataException("Placement model client size is invalid.");
        bool usesManualRecording =
            !string.IsNullOrWhiteSpace(
                ManualInputRecordingId);
        if (Steps.Count == 0 &&
            !usesManualRecording)
        {
            throw new InvalidDataException(
                "Placement model has no steps or manual recording.");
        }
        if (!Enum.IsDefined(CameraPreparationMode)) throw new InvalidDataException("Placement model camera preparation is invalid.");
        if (TeamSlot is < 0 or > 8) throw new InvalidDataException("Team must be Don't change or Team 1 through 8.");
        if (PlacementIntervalMilliseconds < 0)
        {
            throw new InvalidDataException(
                "Placement interval cannot be negative.");
        }
        if (DefaultAfterStartDelayMilliseconds < 0)
        {
            throw new InvalidDataException(
                "Default After Start delay cannot be negative.");
        }
        if (PlacementAttempts is < 1 or
            > MaximumPlacementAttempts)
        {
            throw new InvalidDataException(
                $"Placement attempts must be 1 through {MaximumPlacementAttempts}.");
        }
        if (ImpossibilityThresholdMinutes is < 0 or
            > MaximumImpossibilityThresholdMinutes)
        {
            throw new InvalidDataException(
                $"Impossibility threshold must be 0 through {MaximumImpossibilityThresholdMinutes} minutes.");
        }
        if (usesManualRecording)
        {
            if (CameraPreparationMode !=
                CameraPreparationMode.FastNoAlign)
            {
                throw new InvalidDataException(
                    "Manual recordings require a Fast no align placement setup.");
            }
            try
            {
                ManualInputRecording.ValidateId(
                    ManualInputRecordingId!);
            }
            catch (ArgumentException error)
            {
                throw new InvalidDataException(
                    "Manual recording id is invalid.",
                    error);
            }
        }
        if (CameraPreparationMode == CameraPreparationMode.FastNoAlign)
        {
            if (Target is null) throw new InvalidDataException("Choose the map and act for this Fast no align placement model.");
            Target.Validate();
        }
        foreach (PlacementStep step in Steps) step.Validate(ClientWidth, ClientHeight);
        if (CameraPreparationMode == CameraPreparationMode.FastNoAlign)
        {
            PlacementAuthoringRules.ValidateMinimumSpacing(Steps);
            PlacementAuthoringRules.ValidateBeforeStartSafety(Steps);
        }
    }

    public void ValidateCompatibility(
        CameraPreparationMode expectedMode,
        PlacementTarget expectedTarget)
    {
        Validate();
        expectedTarget.Validate();
        if (CameraPreparationMode != expectedMode)
        {
            throw new InvalidDataException(
                expectedMode == CameraPreparationMode.FastNoAlign
                    ? "Choose a Fast no align placement model for this preset."
                    : "Camera Model placements are retired. Replace this preset with a current Placement Setup.");
        }
        if (expectedMode == CameraPreparationMode.FastNoAlign &&
            (Target is null ||
             !PlacementSetupCatalog.Covers(
                 Target,
                 expectedTarget)))
        {
            throw new InvalidDataException(
                "The selected placement model was made for a different map or act.");
        }
    }

    public bool IsCompatibleWith(
        CameraPreparationMode expectedMode,
        PlacementTarget expectedTarget) =>
        CameraPreparationMode == expectedMode &&
        (expectedMode == CameraPreparationMode.CameraModel ||
         Target is not null &&
         PlacementSetupCatalog.Covers(
             Target,
             expectedTarget));

    public static IReadOnlyList<PlacementStep> FromCaptures(
        IReadOnlyList<PlacementCapture> captures,
        int defaultDelayMilliseconds,
        bool useRecordedDelays)
    {
        if (defaultDelayMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(defaultDelayMilliseconds));
        List<PlacementStep> steps = new(captures.Count);
        for (int index = 0; index < captures.Count; index++)
        {
            PlacementCapture capture = captures[index];
            int delay = defaultDelayMilliseconds;
            if (useRecordedDelays && index + 1 < captures.Count)
            {
                delay = Math.Max(0, captures[index + 1].SelectedAtMilliseconds - capture.ClickedAtMilliseconds);
            }

            steps.Add(new PlacementStep
            {
                UnitKey = capture.UnitKey,
                X = capture.X,
                Y = capture.Y,
                DelayAfterMilliseconds = delay,
                AutoUpgradePriority =
                    PlacementAuthoringRules
                        .DefaultAutoUpgradePriority,
            });
        }

        return steps;
    }
}
