using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Automation.Diagnostics;

internal sealed class DeepDebugArtifactResolver
{
    private const string DetectorPackId =
        "anime-expeditions-expeditions";

    private readonly AppPaths _paths;
    private readonly DeepDebugArchiveTextWriter _archiveText;

    public DeepDebugArtifactResolver(
        AppPaths paths,
        DeepDebugArchiveTextWriter archiveText)
    {
        _paths = paths;
        _archiveText = archiveText;
    }

    public async Task<ResolvedArtifacts> ResolveAsync(
        DeepDebugOperationContext context,
        string snapshotRoot)
    {
        ResolvedArtifacts resolved = new();
        if (!string.IsNullOrWhiteSpace(
                context.MacroPlanId))
        {
            string id = ValidateId(context.MacroPlanId);
            MacroPlan? plan =
                await ReadAndCopyAsync<MacroPlan>(
                    Path.Combine(
                        _paths.MacroPlans,
                        $"{id}.json"),
                    Path.Combine(
                        snapshotRoot,
                        "macro-plan.json"))
                    .ConfigureAwait(false);
            if (plan is not null)
            {
                foreach (MacroTaskDefinition task in
                         plan.Tasks)
                {
                    await ResolveTaskAsync(
                        task,
                        snapshotRoot,
                        resolved).ConfigureAwait(false);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(
                context.ExpeditionPresetId))
        {
            await ResolveExpeditionAsync(
                context.ExpeditionPresetId,
                snapshotRoot,
                resolved).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(
                context.ChallengePresetId))
        {
            await ResolveChallengeAsync(
                context.ChallengePresetId,
                snapshotRoot,
                resolved).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(
                context.StoryPresetId))
        {
            await ResolveStoryAsync(
                context.StoryPresetId,
                snapshotRoot,
                resolved).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(
                context.RaidPresetId))
        {
            await ResolveRaidAsync(
                context.RaidPresetId,
                snapshotRoot,
                resolved).ConfigureAwait(false);
        }
        return resolved;
    }

    private async Task ResolveTaskAsync(
        MacroTaskDefinition task,
        string root,
        ResolvedArtifacts resolved)
    {
        if (task.UsesPlacementSetup)
        {
            foreach (string id in
                     SetupIdsFor(task))
            {
                resolved.PlacementModelIds.Add(id);
            }
            resolved.DetectorPackIds.Add(
                DetectorPackId);
            return;
        }

        switch (task.Kind)
        {
            case MacroTaskKind.Expedition:
                await ResolveExpeditionAsync(
                    task.PresetId,
                    root,
                    resolved).ConfigureAwait(false);
                break;
            case MacroTaskKind.Challenge:
                await ResolveChallengeAsync(
                    task.PresetId,
                    root,
                    resolved).ConfigureAwait(false);
                break;
            case MacroTaskKind.Story:
                await ResolveStoryAsync(
                    task.PresetId,
                    root,
                    resolved).ConfigureAwait(false);
                break;
            case MacroTaskKind.Raid:
                await ResolveRaidAsync(
                    task.PresetId,
                    root,
                    resolved).ConfigureAwait(false);
                break;
        }
    }

    private static IEnumerable<string> SetupIdsFor(
        MacroTaskDefinition task)
    {
        if (task.Kind == MacroTaskKind.Challenge)
        {
            return PlacementSetupCatalog.All
                .Where(route =>
                    route.Target.Mode ==
                    PlacementTargetMode.Challenge)
                .Select(route => route.ModelId);
        }

        PlacementTarget target =
            task.PlacementTarget ??
            throw new InvalidDataException(
                "A Fast no align task is missing its placement route.");
        return [PlacementSetupCatalog.IdFor(target)];
    }

    private async Task ResolveExpeditionAsync(
        string id,
        string root,
        ResolvedArtifacts resolved)
    {
        string safeId = ValidateId(id);
        if (!resolved.ExpeditionPresetIds.Add(safeId))
        {
            return;
        }
        ExpeditionPreset? preset =
            await ReadAndCopyAsync<ExpeditionPreset>(
                Path.Combine(
                    _paths.Presets,
                    $"{safeId}.json"),
                Path.Combine(
                    root,
                    "presets",
                    "expeditions",
                    $"{safeId}.json"))
                .ConfigureAwait(false);
        if (preset is null) return;
        AddId(
            resolved.CameraModelIds,
            preset.CameraModelId);
        AddId(
            resolved.PlacementModelIds,
            preset.PlacementModelId);
        AddId(
            resolved.DetectorPackIds,
            preset.DetectorPackId);
    }

    private async Task ResolveChallengeAsync(
        string id,
        string root,
        ResolvedArtifacts resolved)
    {
        string safeId = ValidateId(id);
        if (!resolved.ChallengePresetIds.Add(safeId))
        {
            return;
        }
        ChallengePreset? preset =
            await ReadAndCopyAsync<ChallengePreset>(
                Path.Combine(
                    _paths.ChallengePresets,
                    $"{safeId}.json"),
                Path.Combine(
                    root,
                    "presets",
                    "challenges",
                    $"{safeId}.json"))
                .ConfigureAwait(false);
        if (preset is null) return;
        AddId(
            resolved.DetectorPackIds,
            preset.DetectorPackId);
        foreach (ChallengeMapProfile profile in preset.Maps)
        {
            AddId(
                resolved.CameraModelIds,
                profile.CameraModelId);
            AddId(
                resolved.PlacementModelIds,
                profile.PrestartPlacementModelId);
            AddId(
                resolved.PlacementModelIds,
                profile.DelayedPlacementModelId);
        }
    }

    private async Task ResolveStoryAsync(
        string id,
        string root,
        ResolvedArtifacts resolved)
    {
        string safeId = ValidateId(id);
        if (!resolved.StoryPresetIds.Add(safeId))
        {
            return;
        }
        StoryPreset? preset =
            await ReadAndCopyAsync<StoryPreset>(
                Path.Combine(
                    _paths.StoryPresets,
                    $"{safeId}.json"),
                Path.Combine(
                    root,
                    "presets",
                    "story",
                    $"{safeId}.json"))
                .ConfigureAwait(false);
        if (preset is null) return;
        AddId(
            resolved.CameraModelIds,
            preset.CameraModelId);
        AddId(
            resolved.PlacementModelIds,
            preset.PrestartPlacementModelId);
        AddId(
            resolved.PlacementModelIds,
            preset.DelayedPlacementModelId);
        resolved.DetectorPackIds.Add(DetectorPackId);
    }

    private async Task ResolveRaidAsync(
        string id,
        string root,
        ResolvedArtifacts resolved)
    {
        string safeId = ValidateId(id);
        if (!resolved.RaidPresetIds.Add(safeId))
        {
            return;
        }
        RaidPreset? preset =
            await ReadAndCopyAsync<RaidPreset>(
                Path.Combine(
                    _paths.RaidPresets,
                    $"{safeId}.json"),
                Path.Combine(
                    root,
                    "presets",
                    "raid",
                    $"{safeId}.json"))
                .ConfigureAwait(false);
        if (preset is null) return;
        AddId(
            resolved.CameraModelIds,
            preset.CameraModelId);
        AddId(
            resolved.PlacementModelIds,
            preset.PrestartPlacementModelId);
        AddId(
            resolved.PlacementModelIds,
            preset.DelayedPlacementModelId);
        resolved.DetectorPackIds.Add(DetectorPackId);
    }

    private async Task<T?> ReadAndCopyAsync<T>(
        string source,
        string destination)
    {
        if (!File.Exists(source)) return default;
        T? value =
            await JsonFileStore.ReadAsync<T>(
                    source,
                    CancellationToken.None)
                .ConfigureAwait(false);
        if (value is not null)
        {
            await _archiveText
                .WriteJsonAsync(destination, value)
                .ConfigureAwait(false);
        }
        return value;
    }

    private static void AddId(
        HashSet<string> target,
        string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            target.Add(ValidateId(id));
        }
    }

    internal static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) ||
            name != id ||
            id is "." or "..")
        {
            throw new InvalidDataException(
                "A referenced model, preset, or detector id is invalid.");
        }
        return id;
    }
}

internal sealed class ResolvedArtifacts
{
    public HashSet<string> ExpeditionPresetIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ChallengePresetIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> StoryPresetIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RaidPresetIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CameraModelIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PlacementModelIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DetectorPackIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
