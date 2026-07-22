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
}
