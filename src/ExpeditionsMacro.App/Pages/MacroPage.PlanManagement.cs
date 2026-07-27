using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async void SavePlan_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            string webhook = CurrentWebhook();
            string discordUserId =
                DiscordUserIdText.Text.Trim();
            ValidateDiscord(webhook, discordUserId);
            PrivateServerRecoverySelection
                privateServerRecovery =
                    ReadPrivateServerRecoverySelection();
            MacroPlan plan =
                await SavePlanInternalAsync();
            await SaveReportingSettingsAsync(
                webhook,
                discordUserId);
            await SavePrivateServerRecoverySettingsAsync(
                privateServerRecovery);
            PhaseText.Text =
                $"Plan '{plan.Name}' saved locally.";
        }
        catch (Exception error)
        {
            PhaseText.Text = error.Message;
        }
    }

    private async Task<MacroPlan>
        SavePlanInternalAsync()
    {
        MacroPlan plan = BuildPlan();
        await _services.MacroPlans.SaveAsync(plan);
        await _services.UpdateSettingsAsync(
            settings => settings with
            {
                SelectedMacroPlanId = plan.Id,
            });
        await RefreshPlansAsync();
        PlanCombo.SelectedItem =
            _plans.FirstOrDefault(
                value => value.Id == plan.Id);
        return plan;
    }

    private MacroPlan BuildPlan()
    {
        ReindexRows();
        string name = PlanNameText.Text.Trim();
        MacroTaskDefinition[] tasks =
            TaskRows
                .Select(row => row.Definition)
                .ToArray();
        IReadOnlyList<MacroPlanLoopDefinition> loops =
            LoopEditor.ReadDefinitions(tasks);
        MacroPlan plan = new()
        {
            Id = ModelId.FromName(name),
            Name = name,
            Tasks = tasks,
            Progress = TaskRows
                .Select(row => row.Progress)
                .ToArray(),
            Loops = loops,
            LoopStates =
                LoopEditor.ProgressFor(loops),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        plan.Validate();
        return plan;
    }

    private async void PlanCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loading ||
            PlanCombo.SelectedItem is not
                MacroPlan plan)
        {
            return;
        }
        ApplyPlan(plan);
        await _services.UpdateSettingsAsync(
            settings => settings with
            {
                SelectedMacroPlanId = plan.Id,
            });
    }

    private void NewPlan_Click(
        object sender,
        RoutedEventArgs e)
    {
        PlanCombo.SelectedItem = null;
        ApplyNewPlan();
    }

    private void ApplyNewPlan()
    {
        PlanNameText.Text = "Daily rotation";
        TaskRows.Clear();
        LoopEditor.SetTasks(TaskRows);
        EmptyTasksText.Visibility =
            Visibility.Visible;
        LoopEditor.Apply([], []);
        ResetTaskEditor();
        ApplyTotals();
    }

    private void ApplyPlan(MacroPlan plan)
    {
        PlanNameText.Text = plan.Name;
        TaskRows.Clear();
        foreach (MacroTaskDefinition definition in
                 plan.Tasks.OrderBy(
                     task => task.Priority))
        {
            TaskRows.Add(new MacroTaskRow
            {
                Definition = definition,
                Progress =
                    plan.ProgressFor(definition.Id),
            });
        }
        ReindexRows();
        LoopEditor.Apply(
            plan.EffectiveLoops(),
            plan.EffectiveLoopStates());
        ResetTaskEditor();
        ApplyTotals();
    }

    private void ApplyPlanProgress(
        MacroPlan plan)
    {
        LoopEditor.UpdateProgress(
            plan.EffectiveLoopStates());
        Dictionary<string, MacroTaskProgress>
            progress = plan.Progress.ToDictionary(
                value => value.TaskId,
                StringComparer.OrdinalIgnoreCase);
        for (int index = 0;
             index < TaskRows.Count;
             index++)
        {
            MacroTaskRow row = TaskRows[index];
            TaskRows[index] = new MacroTaskRow
            {
                Definition = row.Definition,
                Progress =
                    progress.GetValueOrDefault(
                        row.Definition.Id) ??
                    row.Progress,
            };
        }
        ApplyTotals();
    }

    private async void ResetProgress_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (TaskRows.Count == 0) return;
        MessageBoxResult answer = MessageBox.Show(
            Window.GetWindow(this),
            "Reset victories, defeats, runtime, cooldowns, and completion for every task in this plan?",
            "Reset plan progress",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            MacroPlan reset =
                await _services.Scheduler
                    .ResetProgressAsync(BuildPlan());
            ApplyPlan(reset);
            PhaseText.Text =
                "Plan progress reset.";
        }
        catch (Exception error)
        {
            PhaseText.Text = error.Message;
        }
    }

    private async Task RefreshPlansAsync()
    {
        string? selected =
            (PlanCombo.SelectedItem as MacroPlan)?.Id;
        _plans.Clear();
        foreach (MacroPlan plan in
                 (await _services.MacroPlans
                     .ListAsync())
                 .Where(plan =>
                     plan.UsesPlacementSetupWorkflow ==
                     FastTaskWorkflow))
        {
            _plans.Add(plan);
        }
        PlanCombo.SelectedItem =
            _plans.FirstOrDefault(
                value => value.Id == selected);
    }

    private void ApplyTotals()
    {
        VictoriesText.Text = TaskRows
            .Sum(row => row.Progress.Victories)
            .ToString(
                CultureInfo.InvariantCulture);
        DefeatsText.Text = TaskRows
            .Sum(row => row.Progress.Defeats)
            .ToString(
                CultureInfo.InvariantCulture);
    }
}
