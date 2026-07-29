using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public sealed record PlacementStepEditorValues(
    MatchStepKind Kind,
    int UnitKey,
    string TargetPlacementId,
    string CoordinateXText,
    string CoordinateYText,
    UnitTargetingPriority PlacementTargeting,
    UnitAutoUpgradePriority PlacementAutoUpgrade,
    bool ChangeTargeting,
    UnitTargetingPriority ReconfigureTargeting,
    MatchAutoUpgradeAction AutoUpgradeAction,
    string DelayDurationText,
    string UpgradeCountText);

public sealed class PlacementStepSettingsApplyEventArgs(
    PlacementStepRow? step,
    PlacementStepEditorValues values) : EventArgs
{
    public PlacementStepRow? Step { get; } = step;

    public PlacementStepEditorValues Values { get; } =
        values;
}

public partial class PlacementStepEditorPopover : UserControl
{
    private PlacementStepRow? _step;

    public PlacementStepEditorPopover()
    {
        InitializeComponent();
    }

    public event EventHandler<PlacementStepSettingsApplyEventArgs>?
        ApplyRequested;

    public event EventHandler? CancelRequested;

    public void SetAvailablePlacements(
        IReadOnlyList<
            PlacementEditorChoice<string>> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        PlacementReferenceCombo.ItemsSource = choices;
    }

    public void SetStep(PlacementStepRow step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _step = step;
        EditorTitleText.Text = "Edit match step";
        StepIdentityText.Text =
            step.HasCoordinate
                ? $"{step.StepTypeLabel} · {step.X}, {step.Y}"
                : step.HasPlacementReference
                    ? $"{step.StepTypeLabel} · {step.DisplayUnitId}"
                    : step.StepTypeLabel;
        SetValues(step);
    }

    public void SetNewStep(PlacementStepRow? source)
    {
        _step = null;
        EditorTitleText.Text = "Add match step";
        StepIdentityText.Text =
            "Choose an action for this match timeline.";
        PlacementStepRow defaults =
            source?.Kind != MatchStepKind.Placement
                ? new PlacementStepRow
                {
                    Kind = MatchStepKind.Delay,
                    UnitKey = 1,
                    DelayDurationMilliseconds = 1000,
                    UpgradeCount = 1,
                }
                : new PlacementStepRow
                {
                    Kind =
                        MatchStepKind.ReconfigureUnit,
                    UnitKey = source.UnitKey,
                    TargetPlacementId =
                        source.PlacementId,
                    TargetingPriority =
                        source.TargetingPriority,
                    AutoUpgradeAction =
                        MatchAutoUpgradeAction.NoChange,
                    ChangeTargetingPriority = true,
                    DelayDurationMilliseconds = 1000,
                    UpgradeCount = 1,
                };
        SetValues(defaults);
    }

    public void ShowError(string message)
    {
        StepErrorText.Text = message;
        StepErrorText.Visibility =
            string.IsNullOrEmpty(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void SetValues(PlacementStepRow step)
    {
        KindCombo.SelectedValue = step.Kind;
        UnitCombo.SelectedItem =
            step.UnitKey is >= 1 and <= 6
                ? step.UnitKey
                : 1;
        PlacementReferenceCombo.SelectedValue =
            step.TargetPlacementId;
        CoordinateXText.Text =
            step.X.ToString(
                CultureInfo.CurrentCulture);
        CoordinateYText.Text =
            step.Y.ToString(
                CultureInfo.CurrentCulture);
        PlacementTargetingCombo.SelectedItem =
            step.TargetingPriority;
        PlacementAutoUpgradeCombo.SelectedValue =
            step.AutoUpgradePriority;
        ChangeTargetingCheck.IsChecked =
            step.ChangeTargetingPriority;
        ReconfigureTargetingCombo.SelectedItem =
            step.TargetingPriority;
        AutoUpgradeActionCombo.SelectedValue =
            step.AutoUpgradeAction;
        DelayDurationText.Text =
            Math.Max(
                    1,
                    step.DelayDurationMilliseconds)
                .ToString(
                    CultureInfo.CurrentCulture);
        UpgradeCountText.Text =
            Math.Max(1, step.UpgradeCount)
                .ToString(
                    CultureInfo.CurrentCulture);
        UpdateKindFields();
        ShowError(string.Empty);
        KindCombo.Focus();
    }

    private void KindCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        UpdateKindFields();

    private void UpdateKindFields()
    {
        MatchStepKind kind =
            KindCombo.SelectedValue is MatchStepKind value
                ? value
                : MatchStepKind.Placement;
        PlacementCoordinatePanel.Visibility =
            kind == MatchStepKind.Placement
                ? Visibility.Visible
                : Visibility.Collapsed;
        PlacementReferencePanel.Visibility =
            kind is MatchStepKind.ReconfigureUnit or
                MatchStepKind.UpgradeUnit
                ? Visibility.Visible
                : Visibility.Collapsed;
        PlacementFields.Visibility =
            kind == MatchStepKind.Placement
                ? Visibility.Visible
                : Visibility.Collapsed;
        ReconfigureFields.Visibility =
            kind == MatchStepKind.ReconfigureUnit
                ? Visibility.Visible
                : Visibility.Collapsed;
        DelayFields.Visibility =
            kind == MatchStepKind.Delay
                ? Visibility.Visible
                : Visibility.Collapsed;
        UpgradeFields.Visibility =
            kind == MatchStepKind.UpgradeUnit
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void Apply_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (KindCombo.SelectedValue is not
                MatchStepKind kind)
        {
            ShowError("Choose a match-step action.");
            return;
        }

        bool placement =
            kind == MatchStepKind.Placement;
        bool placementReference =
            kind is MatchStepKind.ReconfigureUnit or
                MatchStepKind.UpgradeUnit;
        string targetPlacementId =
            PlacementReferenceCombo.SelectedValue
                as string ?? string.Empty;
        if (placement &&
            (UnitCombo.SelectedItem is not int ||
             PlacementTargetingCombo.SelectedItem is not
                 UnitTargetingPriority ||
             PlacementAutoUpgradeCombo.SelectedValue is not
                 UnitAutoUpgradePriority))
        {
            ShowError("Choose every placement setting.");
            return;
        }
        if (placementReference &&
            string.IsNullOrWhiteSpace(
                targetPlacementId))
        {
            ShowError("Choose a placed-unit ID.");
            return;
        }
        if (kind == MatchStepKind.ReconfigureUnit &&
            (ReconfigureTargetingCombo.SelectedItem is not
                 UnitTargetingPriority ||
             AutoUpgradeActionCombo.SelectedValue is not
                 MatchAutoUpgradeAction))
        {
            ShowError(
                "Choose every reconfigure setting.");
            return;
        }

        int unit =
            UnitCombo.SelectedItem is int selectedUnit
                ? selectedUnit
                : 0;
        UnitTargetingPriority placementTarget =
            PlacementTargetingCombo.SelectedItem is
                UnitTargetingPriority selectedPlacementTarget
                ? selectedPlacementTarget
                : UnitTargetingPriority.First;
        UnitAutoUpgradePriority placementAuto =
            PlacementAutoUpgradeCombo.SelectedValue is
                UnitAutoUpgradePriority selectedPlacementAuto
                ? selectedPlacementAuto
                : UnitAutoUpgradePriority.Off;
        UnitTargetingPriority reconfigureTarget =
            ReconfigureTargetingCombo.SelectedItem is
                UnitTargetingPriority selectedReconfigureTarget
                ? selectedReconfigureTarget
                : UnitTargetingPriority.First;
        MatchAutoUpgradeAction autoAction =
            AutoUpgradeActionCombo.SelectedValue is
                MatchAutoUpgradeAction selectedAutoAction
                ? selectedAutoAction
                : MatchAutoUpgradeAction.NoChange;

        ApplyRequested?.Invoke(
            this,
            new PlacementStepSettingsApplyEventArgs(
                _step,
                new PlacementStepEditorValues(
                    kind,
                    unit,
                    placementReference
                        ? targetPlacementId
                        : string.Empty,
                    CoordinateXText.Text,
                    CoordinateYText.Text,
                    placementTarget,
                    placementAuto,
                    ChangeTargetingCheck.IsChecked == true,
                    reconfigureTarget,
                    autoAction,
                    DelayDurationText.Text,
                    UpgradeCountText.Text)));
    }

    private void Cancel_Click(
        object sender,
        RoutedEventArgs e) =>
        CancelRequested?.Invoke(
            this,
            EventArgs.Empty);
}
