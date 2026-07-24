namespace ExpeditionsMacro.Automation.Diagnostics;

public sealed record DeepDebugOperationContext
{
    public string? MacroPlanId { get; init; }

    public string? ExpeditionPresetId { get; init; }

    public string? ChallengePresetId { get; init; }

    public string? StoryPresetId { get; init; }

    public string? RaidPresetId { get; init; }

    public IReadOnlyList<string> CameraModelIds { get; init; } = [];

    public IReadOnlyList<string> PlacementModelIds { get; init; } = [];

    public object? OperationSettings { get; init; }

    public string? DebugTool { get; init; }

    public string? DebugStepMode { get; init; }

    public bool RefreshReferencedModelsAfterOperation { get; init; }
}
