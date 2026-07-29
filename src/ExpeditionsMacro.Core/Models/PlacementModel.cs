using System.Text.Json.Serialization;

namespace ExpeditionsMacro.Core.Models;

public sealed record PlacementStep
{
    public const int MaximumDelayDurationMilliseconds =
        3_600_000;

    public const int MaximumUpgradeCount = 100;

    public MatchStepKind Kind { get; init; }

    public string PlacementId { get; init; } =
        string.Empty;

    public string TargetPlacementId { get; init; } =
        string.Empty;

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

    public bool ChangeTargetingPriority { get; init; }

    public MatchAutoUpgradeAction AutoUpgradeAction
    {
        get;
        init;
    }

    public int DelayDurationMilliseconds { get; init; }

    public int UpgradeCount { get; init; }

    [JsonIgnore]
    public bool HasCoordinate =>
        Kind == MatchStepKind.Placement;

    [JsonIgnore]
    public bool HasPlacementReference =>
        Kind is MatchStepKind.ReconfigureUnit or
            MatchStepKind.UpgradeUnit;

    public void Validate(int clientWidth, int clientHeight)
    {
        if (DelayAfterMilliseconds < 0) throw new InvalidDataException("Placement delay cannot be negative.");
        if (DelayAfterStartMilliseconds < 0) throw new InvalidDataException("After-start placement delay cannot be negative.");
        if (!Enum.IsDefined(Kind))
        {
            throw new InvalidDataException(
                "Match step type is invalid.");
        }
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
        if (!Enum.IsDefined(AutoUpgradeAction))
        {
            throw new InvalidDataException(
                "Match-step Auto Upgrade action is invalid.");
        }

        if (HasCoordinate ||
            HasPlacementReference)
        {
            if (UnitKey is < 0 or > 9)
            {
                throw new InvalidDataException(
                    "Unit key must be 0 through 9.");
            }
        }
        if (HasCoordinate)
        {
            if (X < 0 || Y < 0 ||
                X >= clientWidth ||
                Y >= clientHeight)
            {
                throw new InvalidDataException(
                    "Match-step coordinate falls outside the Roblox client.");
            }
        }

        switch (Kind)
        {
            case MatchStepKind.Placement:
                RequirePlacementIdentity();
                RequireUnusedActionFields();
                break;
            case MatchStepKind.ReconfigureUnit:
                RequirePlacementReference();
                if (!ChangeTargetingPriority &&
                    AutoUpgradeAction ==
                        MatchAutoUpgradeAction.NoChange)
                {
                    throw new InvalidDataException(
                        "A reconfigure step must change targeting, Auto Upgrade, or both.");
                }
                if (DelayDurationMilliseconds != 0 ||
                    UpgradeCount != 0)
                {
                    throw new InvalidDataException(
                        "A reconfigure step contains settings for another action.");
                }
                break;
            case MatchStepKind.Delay:
                RequireNoPlacementIdentity();
                if (DelayDurationMilliseconds is < 1 or
                    > MaximumDelayDurationMilliseconds)
                {
                    throw new InvalidDataException(
                        $"Delay steps must wait 1 through {MaximumDelayDurationMilliseconds} ms.");
                }
                if (UpgradeCount != 0 ||
                    ChangeTargetingPriority ||
                    AutoUpgradeAction !=
                        MatchAutoUpgradeAction.NoChange)
                {
                    throw new InvalidDataException(
                        "A delay step contains settings for another action.");
                }
                break;
            case MatchStepKind.UpgradeUnit:
                RequirePlacementReference();
                if (UpgradeCount is < 1 or
                    > MaximumUpgradeCount)
                {
                    throw new InvalidDataException(
                        $"Upgrade steps must press Upgrade Unit 1 through {MaximumUpgradeCount} times.");
                }
                if (DelayDurationMilliseconds != 0 ||
                    ChangeTargetingPriority ||
                    AutoUpgradeAction !=
                        MatchAutoUpgradeAction.NoChange)
                {
                    throw new InvalidDataException(
                        "An upgrade step contains settings for another action.");
                }
                break;
            case MatchStepKind.StartGame:
                RequireNoPlacementIdentity();
                if (UnitKey != 0 ||
                    X != 0 ||
                    Y != 0 ||
                    DelayAfterMilliseconds != 0 ||
                    DelayAfterStartMilliseconds != 0 ||
                    DelayDurationMilliseconds != 0 ||
                    UpgradeCount != 0 ||
                    ChangeTargetingPriority ||
                    AutoUpgradeAction !=
                        MatchAutoUpgradeAction.NoChange)
                {
                    throw new InvalidDataException(
                        "The Start Game step cannot contain unit-action settings.");
                }
                break;
            default:
                throw new InvalidDataException(
                    "Match step type is invalid.");
        }
    }

    private void RequireUnusedActionFields()
    {
        if (DelayDurationMilliseconds != 0 ||
            UpgradeCount != 0 ||
            ChangeTargetingPriority ||
            AutoUpgradeAction !=
                MatchAutoUpgradeAction.NoChange)
        {
            throw new InvalidDataException(
                "A placement step contains settings for another action.");
        }
    }

    private void RequirePlacementIdentity()
    {
        if (!PlacementReferencePolicy.IsValidId(
                PlacementId) ||
            !string.IsNullOrEmpty(
                TargetPlacementId))
        {
            throw new InvalidDataException(
                "A placement step has an invalid internal identity.");
        }
    }

    private void RequirePlacementReference()
    {
        if (!string.IsNullOrEmpty(PlacementId) ||
            !PlacementReferencePolicy.IsValidId(
                TargetPlacementId) ||
            X != 0 ||
            Y != 0)
        {
            throw new InvalidDataException(
                "A unit action has an invalid placement reference.");
        }
    }

    private void RequireNoPlacementIdentity()
    {
        if (!string.IsNullOrEmpty(PlacementId) ||
            !string.IsNullOrEmpty(TargetPlacementId))
        {
            throw new InvalidDataException(
                "This match step cannot reference a placement.");
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

    public PlacementAdvancedSettings AdvancedSettings
    {
        get;
        init;
    } = new();

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
        if (AdvancedSettings is null)
        {
            throw new InvalidDataException(
                "Placement advanced settings are missing.");
        }
        AdvancedSettings.Validate();
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
        IReadOnlyList<PlacementStep> normalizedSteps =
            PlacementTimelinePolicy.NormalizeSteps(Steps);
        foreach (PlacementStep step in normalizedSteps)
        {
            step.Validate(ClientWidth, ClientHeight);
        }
        PlacementTimelinePolicy.ValidateStructure(
            normalizedSteps);
        if (CameraPreparationMode == CameraPreparationMode.FastNoAlign)
        {
            PlacementAuthoringRules.ValidateMinimumSpacing(
                normalizedSteps);
            PlacementAuthoringRules.ValidateBeforeStartSafety(
                normalizedSteps);
            PlacementAuthoringRules
                .ValidateMatchStepReferences(
                    normalizedSteps);
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
