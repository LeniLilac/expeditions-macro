using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private MacroTaskDefinition BuildLegacyPresetTask()
    {
        NamedChoice<MacroTaskKind> kind =
            TaskKindCombo.SelectedItem as
                NamedChoice<MacroTaskKind>
            ?? throw new InvalidOperationException(
                "Choose a task mode.");
        MacroPresetChoice preset =
            TaskPresetCombo.SelectedItem as
                MacroPresetChoice
            ?? throw new InvalidOperationException(
                $"Create and select a {Label(kind.Value)} preset first.");
        bool runtimeTarget = IsInfiniteStory(preset);
        int target = kind.Value == MacroTaskKind.Challenge
            ? 1
            : ParsePositiveInt(
                TaskTargetText,
                runtimeTarget
                    ? "Runtime minutes"
                    : "Victory target");
        MacroTaskDefinition definition = new()
        {
            Id = _editingTaskId ??
                $"task-{Guid.NewGuid():N}",
            Kind = kind.Value,
            PresetId = preset.Id,
            Name = preset.Name,
            Priority = 1,
            Enabled =
                TaskEnabledCheck.IsChecked == true,
            TargetVictories =
                runtimeTarget ? 1 : target,
            TargetRuntimeMinutes =
                runtimeTarget ? target : 180,
            CompleteOnRuntimeDefeat =
                runtimeTarget,
        };
        definition.Validate();
        return definition;
    }

    private async Task RefreshPresetCatalogAsync()
    {
        _allPresets.Clear();
        _storyPresets.Clear();
        if (FastTaskWorkflow) return;

        foreach (ChallengePreset preset in
                 await _services.ChallengePresets
                     .ListAsync())
        {
            _allPresets.Add(
                new MacroPresetChoice(
                    MacroTaskKind.Challenge,
                    preset.Id,
                    preset.Name));
        }
        foreach (ExpeditionPreset preset in
                 await _services.Presets.ListAsync())
        {
            _allPresets.Add(
                new MacroPresetChoice(
                    MacroTaskKind.Expedition,
                    preset.Id,
                    preset.Name));
        }
        foreach (StoryPreset preset in
                 await _services.StoryPresets
                     .ListAsync())
        {
            _storyPresets[preset.Id] = preset;
            _allPresets.Add(
                new MacroPresetChoice(
                    MacroTaskKind.Story,
                    preset.Id,
                    preset.Name));
        }
        foreach (RaidPreset preset in
                 await _services.RaidPresets
                     .ListAsync())
        {
            _allPresets.Add(
                new MacroPresetChoice(
                    MacroTaskKind.Raid,
                    preset.Id,
                    preset.Name));
        }
    }

    private void RefreshVisiblePresets()
    {
        if (FastTaskWorkflow)
        {
            RefreshVisibleRoutes();
            return;
        }

        MacroPresetChoice? selected =
            TaskPresetCombo.SelectedItem as
                MacroPresetChoice;
        MacroTaskKind kind = SelectedTaskKind();
        _visiblePresets.Clear();
        foreach (MacroPresetChoice preset in
                 _allPresets
                     .Where(value =>
                         value.Kind == kind)
                     .OrderBy(
                         value => value.Name,
                         StringComparer
                             .CurrentCultureIgnoreCase))
        {
            _visiblePresets.Add(preset);
        }
        TaskPresetCombo.SelectedItem =
            _visiblePresets.FirstOrDefault(
                value =>
                    value.Id == selected?.Id) ??
            _visiblePresets.FirstOrDefault();
        TaskEditorStatusText.Text =
            _visiblePresets.Count == 0
                ? $"Create a {Label(kind)} preset before adding this task."
                : string.Empty;
    }
}
