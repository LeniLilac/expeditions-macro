using System.Text.Json.Serialization;

namespace ExpeditionsMacro.Core.Models;

public enum MacroTaskKind
{
    Challenge,
    Expedition,
    Story,
    Raid,
    Event,
    Utility,
    Bounty,
}

public sealed record MacroTaskDefinition
{
    public required string Id { get; init; }
    public required MacroTaskKind Kind { get; init; }
    public string PresetId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Priority { get; init; } = 1;

    // Public beta plans could disable a task. Keep accepting that JSON field,
    // but discard it so every loaded task is active and future saves omit it.
    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyEnabled
    {
        get => null;
        init
        {
        }
    }

    public int TargetVictories { get; init; } = 1;
    public int TargetRuntimeMinutes { get; init; } = 180;
    public bool CompleteOnRuntimeDefeat { get; init; }
    public PlacementTarget? PlacementTarget { get; init; }
    public int Difficulty { get; init; } = 1;
    public bool HardMode { get; init; }
    public int DefeatRetries { get; init; }
    public bool RunTraitChallenge { get; init; } = true;
    public bool RunStatChallenge { get; init; } = true;
    public bool RunSpriteChallenge { get; init; } = true;
    public bool ExtractAtCheckpoint { get; init; } = true;
    public int BossesBeforeExtract { get; init; } = 1;
    public ResourceRefuelTarget RefuelTarget { get; init; } =
        ResourceRefuelTarget.GoldMine;
    public int RefuelIntervalMinutes { get; init; } = 60;
    public int BountyParkedNonViableLimit { get; init; } = 2;

    public bool IsRecurring =>
        Kind is MacroTaskKind.Challenge or
            MacroTaskKind.Utility or
            MacroTaskKind.Bounty;
    public bool UsesPlacementSetup =>
        Kind is not MacroTaskKind.Utility and
            not MacroTaskKind.Bounty &&
        string.IsNullOrWhiteSpace(PresetId);

    public void Validate()
    {
        ValidateId(Id, "task");
        if (!Enum.IsDefined(Kind)) throw new InvalidDataException("Task type is invalid.");
        if (Priority is < 1 or > 9999) throw new InvalidDataException("Task priority must be 1 through 9999.");
        if (TargetVictories is < 1 or > 100000) throw new InvalidDataException("Victory target must be 1 through 100000.");
        if (CompleteOnRuntimeDefeat && Kind != MacroTaskKind.Story) throw new InvalidDataException("Only an Infinite Story task can use a runtime target.");
        if (TargetRuntimeMinutes is < 1 or > 10080) throw new InvalidDataException("Runtime target must be 1 minute through 7 days.");
        if (DefeatRetries is < 0 or > 20) throw new InvalidDataException("Defeat retries must be 0 through 20.");
        if (Difficulty is < 1 or > 3) throw new InvalidDataException("Difficulty must be 1 through 3.");
        if (BossesBeforeExtract is < 0 or > 99) throw new InvalidDataException("Boss nodes before extraction must be 0 through 99.");
        if (Kind == MacroTaskKind.Utility)
        {
            if (!string.IsNullOrWhiteSpace(PresetId) ||
                PlacementTarget is not null)
            {
                throw new InvalidDataException(
                    "Utility tasks do not use presets or placement routes.");
            }
            if (RefuelTarget == ResourceRefuelTarget.None ||
                (RefuelTarget & ~ResourceRefuelTarget.Both) != 0)
            {
                throw new InvalidDataException(
                    "Choose Gold Mine refuel, Resource Drill refuel, or both.");
            }
            if (RefuelIntervalMinutes is < 1 or > 10080)
            {
                throw new InvalidDataException(
                    "Utility interval must be 1 minute through 7 days.");
            }
            return;
        }
        if (Kind == MacroTaskKind.Bounty)
        {
            if (!string.IsNullOrWhiteSpace(PresetId) ||
                PlacementTarget is not null)
            {
                throw new InvalidDataException(
                    "Bounty tasks use the required Placement Setup routes automatically.");
            }
            if (BountyParkedNonViableLimit is < 0 or > 4)
            {
                throw new InvalidDataException(
                    "Parked non-viable Bounties must be 0 through 4.");
            }
            return;
        }
        if (!UsesPlacementSetup)
        {
            ValidateId(PresetId, "preset");
            return;
        }

        if (Kind == MacroTaskKind.Challenge)
        {
            if (PlacementTarget is not null)
            {
                throw new InvalidDataException(
                    "Challenge rotation chooses its map automatically.");
            }
            if (!RunTraitChallenge &&
                !RunStatChallenge &&
                !RunSpriteChallenge)
            {
                throw new InvalidDataException(
                    "Select at least one Challenge type.");
            }
            return;
        }

        if (PlacementTarget is null)
        {
            throw new InvalidDataException(
                "Choose a map and act for this task.");
        }
        PlacementTarget.Validate();
        PlacementTargetMode expected = Kind switch
        {
            MacroTaskKind.Expedition =>
                PlacementTargetMode.Expedition,
            MacroTaskKind.Story =>
                PlacementTargetMode.Story,
            MacroTaskKind.Raid =>
                PlacementTargetMode.Raid,
            MacroTaskKind.Event =>
                PlacementTargetMode.Event,
            _ => throw new InvalidDataException(
                "The task route is invalid."),
        };
        if (PlacementTarget.Mode != expected)
        {
            throw new InvalidDataException(
                "The selected placement route does not match the task mode.");
        }
        if (PlacementTarget.Mode == PlacementTargetMode.Expedition &&
            PlacementTarget.MapNumber ==
            PlacementSetupCatalog.SharedExpeditionMapNumber)
        {
            throw new InvalidDataException(
                "Choose a specific Expedition map for this task.");
        }
        if (PlacementSetupCatalog.IsSharedStoryTarget(
                PlacementTarget))
        {
            throw new InvalidDataException(
                "Choose a specific Story run for this task.");
        }
    }

    private static void ValidateId(string id, string label)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new InvalidDataException($"The {label} id is invalid.");
    }
}

public sealed record MacroTaskProgress
{
    public required string TaskId { get; init; }
    public int Victories { get; init; }
    public int Defeats { get; init; }
    public long RuntimeSeconds { get; init; }
    public int TargetVictoryBaseline { get; init; }
    public long TargetRuntimeBaselineSeconds { get; init; }
    public bool Completed { get; init; }
    public DateTimeOffset? LastAttemptAt { get; init; }
    public DateTimeOffset? LastCompletedAt { get; init; }
    public DateTimeOffset? NextEligibleAtUtc { get; init; }
}

public sealed record MacroPlan
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<MacroTaskDefinition> Tasks { get; init; }
    public IReadOnlyList<MacroTaskProgress> Progress { get; init; } = [];
    public IReadOnlyList<MacroPlanLoopDefinition> Loops { get; init; } = [];
    public IReadOnlyList<MacroPlanLoopProgress> LoopStates { get; init; } = [];

    // Beta 29 wrote one loop and one progress object. Keep those fields
    // readable so existing local plans and share codes migrate in place.
    public MacroPlanLoopDefinition? Loop { get; init; }
    public MacroPlanLoopProgress LoopProgress { get; init; } = new();
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool UsesPlacementSetupWorkflow =>
        Tasks.All(task =>
            task.Kind is MacroTaskKind.Utility or
                MacroTaskKind.Bounty ||
            task.UsesPlacementSetup);

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported macro plan format.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Macro plan identity is missing.");
        if (Tasks.Count == 0) throw new InvalidDataException("Add at least one task to the macro plan.");
        foreach (MacroTaskDefinition task in Tasks) task.Validate();
        if (Tasks.Select(task => task.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Tasks.Count)
        {
            throw new InvalidDataException("Every macro task must have a unique id.");
        }
        if (Loop is not null && Loops.Count != 0)
        {
            throw new InvalidDataException(
                "A plan cannot mix legacy and current loop definitions.");
        }
        if (!LoopProgress.IsEmpty && Loop is null)
        {
            throw new InvalidDataException(
                "Legacy loop progress requires its saved loop.");
        }
        if (LoopStates.Count != 0 && Loops.Count == 0)
        {
            throw new InvalidDataException(
                "Loop progress requires current loop definitions.");
        }

        IReadOnlyList<MacroPlanLoopDefinition> loops =
            EffectiveLoops();
        MacroPlanLoopDefinition.ValidateAll(loops, Tasks);
        HashSet<string> signatures = loops
            .Select(loop => loop.ConfigurationSignature)
            .ToHashSet(StringComparer.Ordinal);
        if (signatures.Count != loops.Count)
        {
            throw new InvalidDataException(
                "Every loop must have a unique configuration.");
        }
        IReadOnlyList<MacroPlanLoopProgress> states =
            EffectiveLoopStates();
        if (states
                .Select(state => state.ConfigurationSignature)
                .Distinct(StringComparer.Ordinal)
                .Count() != states.Count)
        {
            throw new InvalidDataException(
                "Every loop may have only one progress record.");
        }
        foreach (MacroPlanLoopProgress state in states)
        {
            state.Validate();
            if (!signatures.Contains(
                    state.ConfigurationSignature))
            {
                throw new InvalidDataException(
                    "Loop progress refers to a loop that is no longer in the plan.");
            }
        }
        string[] taskIds = Tasks.Select(task => task.Id).ToArray();
        if (Progress.Select(value => value.TaskId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Progress.Count)
        {
            throw new InvalidDataException("Every macro task may have only one progress record.");
        }
        foreach (MacroTaskProgress value in Progress)
        {
            if (!taskIds.Contains(value.TaskId, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Macro progress refers to a task that is no longer in the plan.");
            }
            if (value.Victories < 0 ||
                value.Defeats < 0 ||
                value.RuntimeSeconds < 0 ||
                value.TargetVictoryBaseline < 0 ||
                value.TargetVictoryBaseline > value.Victories ||
                value.TargetRuntimeBaselineSeconds < 0 ||
                value.TargetRuntimeBaselineSeconds >
                    value.RuntimeSeconds)
            {
                throw new InvalidDataException("Macro task progress cannot be negative.");
            }
        }
    }

    public MacroTaskProgress ProgressFor(string taskId) =>
        Progress.FirstOrDefault(value => string.Equals(value.TaskId, taskId, StringComparison.OrdinalIgnoreCase))
        ?? new MacroTaskProgress { TaskId = taskId };

    public IReadOnlyList<MacroPlanLoopDefinition>
        EffectiveLoops() =>
        Loops.Count != 0
            ? Loops
            : Loop is null
                ? []
                : [Loop];

    public IReadOnlyList<MacroPlanLoopProgress>
        EffectiveLoopStates() =>
        LoopStates.Count != 0
            ? LoopStates
            : Loop is null ||
                LoopProgress.IsEmpty
                ? []
                : [LoopProgress];

    public MacroPlanLoopProgress LoopStateFor(
        MacroPlanLoopDefinition loop) =>
        EffectiveLoopStates()
            .FirstOrDefault(state =>
                string.Equals(
                    state.ConfigurationSignature,
                    loop.ConfigurationSignature,
                    StringComparison.Ordinal)) ??
        new MacroPlanLoopProgress
        {
            ConfigurationSignature =
                loop.ConfigurationSignature,
            Phase = MacroPlanLoopPhase.Loop,
        };

    public MacroPlan ResetProgress() => this with
    {
        Progress = Tasks.Select(task => new MacroTaskProgress { TaskId = task.Id }).ToArray(),
        Loops = EffectiveLoops().ToArray(),
        LoopStates = [],
        Loop = null,
        LoopProgress = new(),
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}
