using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task ValidateBountyTaskAsync(
        MacroTaskDefinition definition)
    {
        if (definition.Kind != MacroTaskKind.Bounty)
        {
            return;
        }

        AddTaskButton.IsEnabled = false;
        try
        {
            foreach (PlacementTarget target in
                     BountyCatalog.RequiredPlacementTargets)
            {
                await LoadPlacementSetupAsync(
                    target,
                    CancellationToken.None);
            }
        }
        finally
        {
            AddTaskButton.IsEnabled = true;
        }
    }
}
