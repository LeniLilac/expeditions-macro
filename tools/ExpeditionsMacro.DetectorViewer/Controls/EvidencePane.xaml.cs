using System.Windows.Controls;
using System.Windows.Media;
using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Controls;

public partial class EvidencePane : UserControl
{
    private DetectorCatalogItem? _definition;

    public EvidencePane()
    {
        InitializeComponent();
    }

    public event EventHandler? SelectedCheckChanged;

    public InspectionCheckItem? SelectedCheck =>
        EvidenceGrid.SelectedItem as
            InspectionCheckItem;

    public void SetDefinition(
        DetectorCatalogItem? item)
    {
        _definition = item;
        DetectorNameText.Text =
            item?.Name ??
            "Select a detector";
        DetectorDescriptionText.Text =
            item?.Description ??
            "Choose a catalog item to evaluate this frame.";
        if (item is null)
        {
            ClearResult();
            return;
        }
        CoverageNoteText.Text =
            item.Definition.Limitation ??
            "Static geometry and named gates come directly from the loaded production detector.";
        CoverageNoteIcon.Icon =
            item.IsUnavailable
                ? ViewerIconKind.AlertCircle
                : ViewerIconKind.Info;
        CoverageNoteIcon.Foreground =
            (Brush)FindResource(
                item.IsUnavailable
                    ? "WarningBrush"
                    : "MutedBrush");
    }

    public void SetEvaluating()
    {
        SetStatus(
            "Evaluating",
            "AccentBrush");
        FinalStateText.Text = "Working…";
        ConfidenceText.Text = "Not available";
        DecisionThresholdText.Text = "Not available";
        ActionText.Text = "Not available";
        EvidenceGrid.ItemsSource = null;
        ClearSelectedCheck();
    }

    public void SetReport(
        DetectorInspectionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        FinalStateText.Text = report.FinalState;
        ConfidenceText.Text =
            report.Confidence is double confidence
                ? confidence.ToString(
                    "0.000",
                    System.Globalization
                        .CultureInfo.InvariantCulture)
                : "Not exposed";
        DecisionThresholdText.Text =
            report.DecisionThreshold;
        ActionText.Text =
            report.Action is null
                ? "None"
                : $"({report.Action.X}, {report.Action.Y})  {(
                    report.Action.IsLive
                        ? "Live"
                        : "Advisory")}";
        ActionText.ToolTip =
            report.Action?.Provenance ??
            "No detector-owned action for this result.";
        if (report.Passed is true)
        {
            SetStatus(
                "Pass",
                "SuccessBrush");
        }
        else if (report.Passed is false)
        {
            SetStatus(
                "Fail",
                "ErrorBrush");
        }
        else
        {
            SetStatus(
                "Observed",
                "WarningBrush");
        }
        if (report.Notes.Count > 0)
        {
            CoverageNoteText.Text =
                string.Join(
                    Environment.NewLine,
                    report.Notes);
        }
        InspectionCheckItem[] checks =
            report.Checks
                .Select(check =>
                    new InspectionCheckItem(
                        check))
                .ToArray();
        EvidenceGrid.ItemsSource = checks;
        EvidenceGrid.SelectedIndex =
            checks.Length > 0
                ? 0
                : -1;
        UpdateSelectedCheck();
    }

    public void SetError(string message)
    {
        SetStatus(
            "Error",
            "ErrorBrush");
        FinalStateText.Text = "Evaluation failed";
        ConfidenceText.Text = "Not available";
        DecisionThresholdText.Text = "Not available";
        ActionText.Text = "None";
        CoverageNoteText.Text = message;
        CoverageNoteIcon.Icon =
            ViewerIconKind.AlertCircle;
        CoverageNoteIcon.Foreground =
            (Brush)FindResource("ErrorBrush");
        EvidenceGrid.ItemsSource = null;
        ClearSelectedCheck();
    }

    private void ClearResult()
    {
        SetStatus(
            "Waiting",
            "MutedBrush");
        FinalStateText.Text = "Not available";
        ConfidenceText.Text = "Not available";
        DecisionThresholdText.Text = "Not available";
        ActionText.Text = "Not available";
        EvidenceGrid.ItemsSource = null;
        ClearSelectedCheck();
    }

    private void SetStatus(
        string text,
        string brushResource)
    {
        ResultStatusText.Text = text;
        ResultStatusText.Foreground =
            (Brush)FindResource(brushResource);
    }

    private void EvidenceGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateSelectedCheck();
        SelectedCheckChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void UpdateSelectedCheck()
    {
        InspectionCheckItem? item =
            SelectedCheck;
        if (item is null)
        {
            ClearSelectedCheck();
            return;
        }
        SelectedCheckDetail.Visibility =
            System.Windows.Visibility.Visible;
        SelectedCheckNameText.Text = item.Label;
        SelectedCheckStatusText.Text =
            item.StatusText;
        SelectedCheckStatusText.Foreground =
            (Brush)FindResource(
                item.IsPassed
                    ? "SuccessBrush"
                    : item.IsFailed
                        ? "ErrorBrush"
                        : item.IsNotExposed
                            ? "WarningBrush"
                            : "MutedBrush");
        SelectedCheckExpectedText.Text =
            item.Expected;
        SelectedCheckMeasuredText.Text =
            item.Measured;
        SelectedCheckGateText.Text =
            item.Threshold;
    }

    private void ClearSelectedCheck()
    {
        if (SelectedCheckDetail is null)
        {
            return;
        }
        SelectedCheckDetail.Visibility =
            System.Windows.Visibility.Collapsed;
    }
}
