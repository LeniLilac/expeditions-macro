using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class PresetInUseException : InvalidOperationException
{
    public PresetInUseException(string message)
        : base(message)
    {
    }
}

public sealed class PresetDeletionService
{
    private readonly PresetRepository _expeditions;
    private readonly ChallengePresetRepository _challenges;
    private readonly StoryPresetRepository _stories;
    private readonly RaidPresetRepository _raids;
    private readonly MacroPlanRepository _plans;

    public PresetDeletionService(
        PresetRepository expeditions,
        ChallengePresetRepository challenges,
        StoryPresetRepository stories,
        RaidPresetRepository raids,
        MacroPlanRepository plans)
    {
        _expeditions = expeditions;
        _challenges = challenges;
        _stories = stories;
        _raids = raids;
        _plans = plans;
    }

    public async Task DeleteAsync(
        MacroTaskKind kind,
        string presetId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));

        IReadOnlyList<MacroPlan> plans = await _plans.ListAsync(cancellationToken).ConfigureAwait(false);
        string[] planNames = plans
            .Where(plan => plan.Tasks.Any(task =>
                task.Kind == kind &&
                string.Equals(task.PresetId, presetId, StringComparison.OrdinalIgnoreCase)))
            .Select(plan => plan.Name)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        string[] challengeNames = kind == MacroTaskKind.Expedition
            ? (await _challenges.ListAsync(cancellationToken).ConfigureAwait(false))
                .Where(preset =>
                    preset.IdleBehavior == ChallengeIdleBehavior.RunExpeditions &&
                    string.Equals(preset.ExpeditionPresetId, presetId, StringComparison.OrdinalIgnoreCase))
                .Select(preset => preset.Name)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToArray()
            : [];

        if (planNames.Length > 0 || challengeNames.Length > 0)
        {
            List<string> references = [];
            references.AddRange(planNames.Select(name => $"Macro plan '{name}'"));
            references.AddRange(challengeNames.Select(name => $"Challenge preset '{name}'"));
            throw new PresetInUseException(
                $"This preset is still used by {JoinReferences(references)}. Remove those references before deleting it.");
        }

        await DeleteUnreferencedAsync(kind, presetId, cancellationToken).ConfigureAwait(false);
    }

    private Task DeleteUnreferencedAsync(
        MacroTaskKind kind,
        string presetId,
        CancellationToken cancellationToken) => kind switch
        {
            MacroTaskKind.Expedition => _expeditions.DeleteAsync(presetId, cancellationToken),
            MacroTaskKind.Challenge => _challenges.DeleteAsync(presetId, cancellationToken),
            MacroTaskKind.Story => _stories.DeleteAsync(presetId, cancellationToken),
            MacroTaskKind.Raid => _raids.DeleteAsync(presetId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static string JoinReferences(IReadOnlyList<string> references) => references.Count switch
    {
        1 => references[0],
        2 => $"{references[0]} and {references[1]}",
        _ => $"{string.Join(", ", references.Take(references.Count - 1))}, and {references[^1]}",
    };
}
