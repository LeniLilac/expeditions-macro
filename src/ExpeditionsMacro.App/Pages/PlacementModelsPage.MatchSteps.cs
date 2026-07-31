using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void UpdateFastPlacementCount()
    {
        int actions = _steps.Count(
            step => !step.IsStartGame);
        FastPlacementCountText.Text =
            $"{actions} action{(actions == 1 ? string.Empty : "s")} · 1 required Start Game";
    }

    private void FastStepSettingsOpening(
        object? sender,
        PlacementStepEditorOpeningEventArgs e) =>
        OpenMatchStepEditor(e);

    private void OpenMatchStepEditor(
        PlacementStepEditorOpeningEventArgs e)
    {
        int existingIndex =
            e.Step is null
                ? -1
                : _steps.IndexOf(e.Step);
        int insertionIndex =
            e.Step is null
                ? NewStepInsertionIndex()
                : existingIndex >= 0
                    ? existingIndex
                    : _steps.Count;
        HashSet<string> soldPlacementIds =
            _steps.Take(
                    Math.Max(0, insertionIndex))
                .Where(row =>
                    row.Kind ==
                    MatchStepKind.SellUnit)
                .Select(row =>
                    row.TargetPlacementId)
                .ToHashSet(
                    StringComparer.Ordinal);
        PlacementStepRow[] available =
            _steps.Take(
                    Math.Max(0, insertionIndex))
                .Where(row =>
                    row.Kind ==
                        MatchStepKind.Placement &&
                    !soldPlacementIds.Contains(
                        row.PlacementId))
                .ToArray();
        MatchStepEditorDialog
            .SetAvailablePlacements(
                available.Select(row =>
                        new PlacementEditorChoice<string>(
                            row.PlacementId,
                            $"{row.DisplayUnitId} · Unit {row.UnitKey} at ({row.X}, {row.Y})"))
                    .ToArray());

        MatchStepEditorOverlay.Visibility =
            Visibility.Visible;
        if (e.Step is null)
        {
            PlacementStepRow? suggested =
                e.SuggestedPlacement is not null &&
                available.Contains(
                    e.SuggestedPlacement)
                    ? e.SuggestedPlacement
                    : available.LastOrDefault();
            MatchStepEditorDialog.SetNewStep(
                suggested,
                _fastDefaultTargetingPriority,
                _fastDefaultAutoUpgradePriority);
        }
        else
        {
            MatchStepEditorDialog.SetStep(e.Step);
        }
    }

    private void MatchStepEditorDialog_CancelRequested(
        object? sender,
        EventArgs e) =>
        CloseMatchStepEditor();

    private void MatchStepEditorOverlay_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }
        CloseMatchStepEditor();
        e.Handled = true;
    }

    private void CloseMatchStepEditor()
    {
        MatchStepEditorOverlay.Visibility =
            Visibility.Collapsed;
        FastStepsList.Focus();
    }

    private void FastStepSettingsApplied(
        object? sender,
        PlacementStepSettingsApplyEventArgs e)
    {
        PlacementStep candidate;
        int insertionIndex =
            e.Step is null
                ? NewStepInsertionIndex()
                : _steps.IndexOf(e.Step);
        try
        {
            candidate = BuildMatchStep(e);
            ValidateMatchStepEdit(
                e.Step,
                candidate,
                insertionIndex);
        }
        catch (Exception error) when (
            error is InvalidDataException or
            InvalidOperationException or
            FormatException)
        {
            MatchStepEditorDialog.ShowError(
                error.Message);
            return;
        }

        PlacementStepRow row =
            e.Step ??
            PlacementStepRow.FromModel(candidate);
        using (SuspendPlacementAutoSave())
        {
            if (e.Step is null)
            {
                _steps.Insert(insertionIndex, row);
            }
            else
            {
                ApplyMatchStep(row, candidate);
            }
            NormalizeTimelineRows();
        }

        FastStepsList.SelectedItem = row;
        FastStepsList.ScrollIntoView(row);
        CloseMatchStepEditor();
        FastStatusText.Text =
            e.Step is null
                ? $"{row.StepTypeLabel} added."
                : $"{row.StepTypeLabel} updated.";
        SchedulePlacementAutoSave();
    }

    private PlacementStep BuildMatchStep(
        PlacementStepSettingsApplyEventArgs e)
    {
        PlacementStepEditorValues values = e.Values;
        bool placement =
            values.Kind == MatchStepKind.Placement;
        bool placementReference =
            values.Kind is
                MatchStepKind.ReconfigureUnit or
                MatchStepKind.UpgradeUnit or
                MatchStepKind.SellUnit;
        PlacementStepRow? target =
            placementReference
                ? _steps.FirstOrDefault(row =>
                    row.Kind ==
                        MatchStepKind.Placement &&
                    string.Equals(
                        row.PlacementId,
                        values.TargetPlacementId,
                        StringComparison.Ordinal))
                : null;
        if (placementReference &&
            target is null)
        {
            throw new InvalidOperationException(
                "Choose a placed unit that still appears earlier in Match Steps.");
        }
        int x = placement
            ? ParseInteger(
                values.CoordinateXText,
                0,
                807,
                "Coordinate X")
            : 0;
        int y = placement
            ? ParseInteger(
                values.CoordinateYText,
                0,
                610,
                "Coordinate Y")
            : 0;
        int delayDuration =
            values.Kind == MatchStepKind.Delay
                ? ParseInteger(
                    values.DelayDurationText,
                    1,
                    PlacementStep
                        .MaximumDelayDurationMilliseconds,
                    "Delay")
                : 0;
        int upgradeCount =
            values.Kind == MatchStepKind.UpgradeUnit
                ? ParseInteger(
                    values.UpgradeCountText,
                    1,
                    PlacementStep.MaximumUpgradeCount,
                    "Upgrade count")
                : 0;

        PlacementStep step = new()
        {
            Kind = values.Kind,
            PlacementId =
                placement
                    ? e.Step?.Kind ==
                            MatchStepKind.Placement
                        ? e.Step.PlacementId
                        : PlacementReferencePolicy
                            .CreatePlacementId()
                    : string.Empty,
            TargetPlacementId =
                placementReference
                    ? target!.PlacementId
                    : string.Empty,
            UnitKey =
                placement
                    ? values.UnitKey
                    : placementReference
                        ? target!.UnitKey
                        : 0,
            X = x,
            Y = y,
            Phase = PlacementPhase.BeforeStart,
            DelayAfterMilliseconds =
                placement ||
                placementReference
                    ? e.Step?.DelayAfterMilliseconds ??
                      _fastPlacementIntervalMilliseconds
                    : 0,
            TargetingPriority =
                values.Kind ==
                    MatchStepKind.ReconfigureUnit
                    ? values.ReconfigureTargeting
                    : values.PlacementTargeting,
            AutoUpgradePriority =
                values.Kind == MatchStepKind.Placement
                    ? values.PlacementAutoUpgrade
                    : UnitAutoUpgradePriority.Off,
            ChangeTargetingPriority =
                values.Kind ==
                    MatchStepKind.ReconfigureUnit &&
                values.ChangeTargeting,
            AutoUpgradeAction =
                values.Kind ==
                    MatchStepKind.ReconfigureUnit
                    ? values.AutoUpgradeAction
                    : MatchAutoUpgradeAction.NoChange,
            DelayDurationMilliseconds = delayDuration,
            UpgradeCount = upgradeCount,
        };
        step.Validate(808, 611);
        return step;
    }

    private void ValidateMatchStepEdit(
        PlacementStepRow? existing,
        PlacementStep candidate,
        int insertionIndex)
    {
        if (candidate.Kind ==
            MatchStepKind.StartGame)
        {
            throw new InvalidOperationException(
                "Start Game is managed by the timeline and cannot be added or edited.");
        }
        if (candidate.HasCoordinate &&
            PlacementSafetyRules
                .IsOnCanonicalClientEdge(
                    candidate.X,
                    candidate.Y))
        {
            throw new InvalidOperationException(
                "That coordinate is on the client edge, not inside the battlefield. Choose a map point away from the outer border.");
        }
        if (candidate.HasCoordinate &&
            PlacementSafetyRules
                .IsInsideFixedCentralHotbar(
                    candidate.X,
                    candidate.Y))
        {
            throw new InvalidOperationException(
                "That coordinate is inside the fixed center unit hotbar. Choose a map point outside the bottom hotbar.");
        }

        List<PlacementStep> prospective =
            _steps.Select(row => row.ToModel())
                .ToList();
        if (existing is null)
        {
            prospective.Insert(
                insertionIndex,
                candidate);
        }
        else
        {
            int index = _steps.IndexOf(existing);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    "The match step is no longer available.");
            }
            prospective[index] = candidate;
        }
        ValidateTimeline(prospective);
    }

    private void ValidateTimeline(
        IReadOnlyList<PlacementStep> steps)
    {
        PlacementStep[] normalized =
            PlacementTimelinePolicy
                .NormalizeSteps(steps)
                .ToArray();
        foreach (PlacementStep step in normalized)
        {
            step.Validate(808, 611);
        }
        PlacementAuthoringRules.ValidateMinimumSpacing(
            normalized);
        PlacementAuthoringRules.ValidateBeforeStartSafety(
            normalized);
        PlacementAuthoringRules
            .ValidateMatchStepReferences(normalized);

        if (CurrentFastTarget().Mode ==
                PlacementTargetMode.Expedition &&
            PlacementSafetyRules.FindDuplicateUnitSlot(
                PlacementTargetMode.Expedition,
                normalized.Where(step =>
                        step.Kind ==
                        MatchStepKind.Placement)
                    .Select(step => step.UnitKey)) is int
                duplicate)
        {
            throw new InvalidOperationException(
                $"Expedition setups allow one placement for Unit {duplicate}.");
        }
    }

    private void ApplyMatchStep(
        PlacementStepRow row,
        PlacementStep step)
    {
        if (row.IsStartGame)
        {
            throw new InvalidOperationException(
                "Start Game cannot be edited.");
        }
        row.Kind = step.Kind;
        row.PlacementId = step.PlacementId;
        row.TargetPlacementId =
            step.TargetPlacementId;
        row.UnitKey = step.UnitKey;
        row.X = step.X;
        row.Y = step.Y;
        row.DelayAfterMilliseconds =
            step.DelayAfterMilliseconds;
        row.DelayAfterStartMilliseconds = 0;
        row.TargetingPriority =
            step.TargetingPriority;
        row.AutoUpgradePriority =
            step.AutoUpgradePriority;
        row.ChangeTargetingPriority =
            step.ChangeTargetingPriority;
        row.AutoUpgradeAction =
            step.AutoUpgradeAction;
        row.DelayDurationMilliseconds =
            step.DelayDurationMilliseconds;
        row.UpgradeCount = step.UpgradeCount;
    }

    private int NewStepInsertionIndex()
        => PlacementTimelinePolicy
            .NewActionInsertionIndex(
                _steps.Select(row =>
                        row.ToModel())
                    .ToArray());

    private int StartGameRowIndex()
    {
        int index = _steps
            .Select((row, position) =>
                new
                {
                    Row = row,
                    Position = position,
                })
            .Where(item => item.Row.IsStartGame)
            .Select(item => item.Position)
            .SingleOrDefault(-1);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "The required Start Game step is missing.");
        }
        return index;
    }

    private void NormalizeTimelineRows()
    {
        int start = StartGameRowIndex();
        IReadOnlyList<PlacementStep> normalized =
            PlacementTimelinePolicy
                .NormalizeSteps(
                    _steps.Select(row =>
                            row.ToModel())
                        .ToArray());
        _normalizingPlacementStepPhase = true;
        try
        {
            for (int index = 0;
                 index < _steps.Count;
                 index++)
            {
                PlacementStepRow row = _steps[index];
                PlacementStep normalizedStep =
                    normalized[index];
                row.Phase =
                    index <= start
                        ? PlacementPhase.BeforeStart
                        : PlacementPhase.AfterStart;
                row.DelayAfterStartMilliseconds = 0;
                row.PlacementId =
                    normalizedStep.PlacementId;
                row.TargetPlacementId =
                    normalizedStep.TargetPlacementId;
                if (row.HasPlacementReference)
                {
                    row.UnitKey =
                        normalizedStep.UnitKey;
                    row.X = 0;
                    row.Y = 0;
                }
            }
        }
        finally
        {
            _normalizingPlacementStepPhase = false;
        }
        UpdateFastPlacementCount();
        UpdatePlacementMarkerLayout();
    }

    private static int ParseInteger(
        string text,
        int minimum,
        int maximum,
        string label)
    {
        if (!int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out int value) ||
            value < minimum ||
            value > maximum)
        {
            throw new FormatException(
                $"{label} must be {minimum:N0} through {maximum:N0}.");
        }
        return value;
    }
}
