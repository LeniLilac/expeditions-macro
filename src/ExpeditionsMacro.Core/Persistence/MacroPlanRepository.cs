using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class MacroPlanRepository
{
    private readonly AppPaths _paths;

    public MacroPlanRepository(AppPaths paths) => _paths = paths;

    public Task<IReadOnlyList<MacroPlan>> ListAsync(CancellationToken cancellationToken = default) =>
        NamedJsonRepository.ListAsync<MacroPlan>(_paths.MacroPlans, plan => plan.Name, plan => plan.Validate(), cancellationToken);

    public Task<MacroPlan?> LoadAsync(string id, CancellationToken cancellationToken = default) =>
        NamedJsonRepository.LoadAsync<MacroPlan>(_paths.MacroPlans, id, plan => plan.Validate(), cancellationToken);

    public Task SaveAsync(MacroPlan plan, CancellationToken cancellationToken = default)
    {
        plan.Validate();
        return NamedJsonRepository.SaveAsync(_paths.MacroPlans, plan.Id, plan, cancellationToken);
    }

    public async Task SaveReplacingAsync(
        MacroPlan plan,
        string? previousId,
        CancellationToken cancellationToken =
            default)
    {
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
