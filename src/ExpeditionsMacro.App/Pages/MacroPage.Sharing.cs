using System.Windows;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async void ExportPlanCode_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy) return;
        try
        {
            MacroPlan plan =
                await SavePlanInternalAsync();
            string code =
                await _services.FastNoAlignShare
                    .ExportAsync(plan);
            FastNoAlignShareBundle bundle =
                _services.FastNoAlignShare.Read(code);
            int presetCount = PresetCount(bundle);
            ShareCodeText.Text = code;
            ShareCodeText.SelectAll();
            ShareCodeText.Focus();
            SharePlanStatusText.Text =
                $"Exported {plan.Tasks.Count} task{Plural(plan.Tasks.Count)}, {presetCount} referenced preset{Plural(presetCount)}, and {bundle.PlacementSetups.Count} placement setup{Plural(bundle.PlacementSetups.Count)} without run history, manual recordings, or account settings.";
        }
        catch (Exception error)
        {
            SharePlanStatusText.Text = error.Message;
        }
    }

    private void CopyPlanCode_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            string code = ShareCodeText.Text.Trim();
            if (code.Length == 0)
            {
                throw new InvalidOperationException(
                    "Export a plan first.");
            }
            Clipboard.SetText(code);
            SharePlanStatusText.Text =
                "Share code copied.";
        }
        catch (Exception error)
        {
            SharePlanStatusText.Text = error.Message;
        }
    }

    private async void ImportPlanCode_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy) return;
        try
        {
            FastNoAlignShareBundle bundle =
                _services.FastNoAlignShare.Read(
                    ShareCodeText.Text);
            int presetCount = PresetCount(bundle);
            MessageBoxResult answer = MessageBox.Show(
                Window.GetWindow(this),
                $"Import '{bundle.Plan.Name}' with {bundle.Plan.Tasks.Count} task{Plural(bundle.Plan.Tasks.Count)}, {presetCount} referenced preset{Plural(presetCount)}, and {bundle.PlacementSetups.Count} placement setup{Plural(bundle.PlacementSetups.Count)}?\n\nA local plan, preset, or placement setup with the same identity will be replaced. Manual recordings and unrelated app settings are not included.",
                "Import Fast no align plan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            await _services.FastNoAlignShare
                .ImportAsync(bundle);
            await _services.UpdateSettingsAsync(
                settings => settings with
                {
                    SelectedMacroPlanId =
                        bundle.Plan.Id,
                });
            await RefreshPlansAsync();
            MacroPlan imported =
                _plans.First(plan =>
                    string.Equals(
                        plan.Id,
                        bundle.Plan.Id,
                        StringComparison.OrdinalIgnoreCase));
            PlanCombo.SelectedItem = imported;
            ApplyPlan(imported);
            SharePlanStatusText.Text =
                $"Imported '{imported.Name}'.";
            PhaseText.Text =
                $"Plan '{imported.Name}' imported locally.";
        }
        catch (Exception error)
        {
            SharePlanStatusText.Text = error.Message;
        }
    }

    private static string Plural(int count) =>
        count == 1 ? string.Empty : "s";

    private static int PresetCount(
        FastNoAlignShareBundle bundle) =>
        bundle.ExpeditionPresets.Count +
        bundle.ChallengePresets.Count +
        bundle.StoryPresets.Count +
        bundle.RaidPresets.Count;
}
