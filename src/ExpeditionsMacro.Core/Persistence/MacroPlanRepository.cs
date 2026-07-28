using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class MacroPlanRepository
{
    private readonly AppPaths _paths;

    public MacroPlanRepository(AppPaths paths) => _paths = paths;

    public async Task<IReadOnlyList<MacroPlan>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MacroPlan> plans =
            await NamedJsonRepository
                .ListAsync<MacroPlan>(
                    _paths.MacroPlans,
                    plan => plan.Name,
                    plan => plan.Validate(),
                    cancellationToken)
                .ConfigureAwait(false);
        return plans
            .Select(StoryHardModePolicy.Normalize)
            .ToArray();
    }

    public async Task<MacroPlan?> LoadAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        MacroPlan? plan =
            await NamedJsonRepository
                .LoadAsync<MacroPlan>(
                    _paths.MacroPlans,
                    id,
                    value => value.Validate(),
                    cancellationToken)
                .ConfigureAwait(false);
        return plan is null
            ? null
            : StoryHardModePolicy.Normalize(plan);
    }

    public Task SaveAsync(MacroPlan plan, CancellationToken cancellationToken = default)
    {
        MacroPlan normalized =
            StoryHardModePolicy.Normalize(plan);
        normalized.Validate();
        return NamedJsonRepository.SaveAsync(
            _paths.MacroPlans,
            normalized.Id,
            normalized,
            cancellationToken);
    }

    public async Task SaveReplacingAsync(
        MacroPlan plan,
        string? previousId,
        CancellationToken cancellationToken =
            default)
    {
        plan = StoryHardModePolicy.Normalize(plan);
        if (!string.Equals(
                previousId,
                plan.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            MacroPlan? existing =
                await LoadAsync(
                        plan.Id,
                        cancellationToken)
                    .ConfigureAwait(false);
            bool samePendingRename =
                !string.IsNullOrWhiteSpace(
                    previousId) &&
                existing is not null &&
                existing.UpdatedAt ==
                    plan.UpdatedAt &&
                string.Equals(
                    existing.Name,
                    plan.Name,
                    StringComparison.Ordinal);
            if (existing is not null &&
                !samePendingRename)
            {
                throw new InvalidDataException(
                    $"A plan named '{plan.Name}' already exists.");
            }
        }
        await SaveAsync(
                plan,
                cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(
                previousId) &&
            !string.Equals(
                previousId,
                plan.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            await NamedJsonRepository.DeleteAsync(
                    _paths.MacroPlans,
                    previousId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
