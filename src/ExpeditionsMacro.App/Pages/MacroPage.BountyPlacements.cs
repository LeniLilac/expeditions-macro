using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task<IReadOnlyList<PlacementModel>>
        ResolveBountyPlacementsAsync(
        CancellationToken cancellationToken)
    {
        List<PlacementModel> placements = [];
        foreach (PlacementTarget target in
                 BountyCatalog.RequiredPlacementTargets)
        {
            placements.Add(
                await LoadPlacementSetupAsync(
                        target,
                        cancellationToken)
                    .ConfigureAwait(false));
        }
        return placements;
    }
}
