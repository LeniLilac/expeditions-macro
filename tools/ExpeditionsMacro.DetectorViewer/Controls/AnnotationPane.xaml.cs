using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.DetectorViewer.Models;

namespace ExpeditionsMacro.DetectorViewer.Controls;

public partial class AnnotationPane : UserControl
{
    private readonly IReadOnlyList<AnnotationExpectedOption>
        _expectedOptions =
        [
            new(
                DetectorExpectedResult.Review,
                "Needs review"),
            new(
                DetectorExpectedResult.Match,
                "Should match"),
            new(
                DetectorExpectedResult.NoMatch,
                "Should not match"),
        ];
    private DetectorImageAnnotation? _annotation;
    private bool _suppressEvents;

    public AnnotationPane()
    {
        InitializeComponent();
        ExpectedResultBox.ItemsSource =
            _expectedOptions.Select(option =>
                option.Label);
        ExpectedResultBox.SelectedIndex = 0;
    }

    public event EventHandler? AnnotationChanged;

    public event EventHandler? SelectedRegionChanged;

    public Guid? SelectedRegionId =>
        RegionList.SelectedItem is
            AnnotationRegionListItem item
                ? item.Id
                : null;

    public void SetUnavailable(string message)
    {
        _annotation = null;
        EditorPanel.IsEnabled = false;
        AnnotationStatusText.Text = message;
        DetectorNameText.Text =
            "No detector selected";
        RefreshRegions();
    }

    public void SetAnnotation(
        string detectorName,
        DetectorImageAnnotation annotation,
        string storePath)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        _suppressEvents = true;
        _annotation = annotation;
        EditorPanel.IsEnabled = true;
        DetectorNameText.Text = detectorName;
        AnnotationStatusText.Text =
            $"Autosaves to {storePath}";
        ExpectedResultBox.SelectedIndex =
            _expectedOptions
                .Select((option, index) =>
                    new
                    {
                        option.Value,
                        Index = index,
                    })
                .First(item =>
                    item.Value == annotation.Expected)
                .Index;
        NotesTextBox.Text = annotation.Notes;
        RefreshRegions();
        _suppressEvents = false;
    }

    public void AddRegion(
        DetectorAnnotationRegion region)
    {
        if (_annotation is null)
        {
            return;
        }
        _annotation.Regions.Add(region);
        RefreshRegions(region.Id);
        RaiseAnnotationChanged();
    }

    private void RefreshRegions(
        Guid? selectedId = null)
    {
        Guid? desired =
            selectedId ?? SelectedRegionId;
        AnnotationRegionListItem[] items =
            _annotation?.Regions
                .Select(region =>
                    new AnnotationRegionListItem(
                        region.Id,
                        region.Label,
                        region.CoordinateSummary))
                .ToArray() ??
            [];
        RegionList.ItemsSource = items;
        RegionCountText.Text =
            $"{items.Length:N0} region{(items.Length == 1 ? string.Empty : "s")}";
        RegionList.SelectedItem = items
            .FirstOrDefault(item =>
                item.Id == desired);
        if (RegionList.SelectedItem is null &&
            items.Length > 0)
        {
            RegionList.SelectedIndex = 0;
        }
        UpdateRegionEditor();
    }

    private void UpdateRegionEditor()
    {
        DetectorAnnotationRegion? region =
            SelectedRegion();
        _suppressEvents = true;
        RegionEditor.IsEnabled = region is not null;
        RegionLabelTextBox.Text =
            region?.Label ?? string.Empty;
        SelectedCoordinatesText.Text =
            region?.CoordinateSummary ??
            "Select a region to edit it.";
        _suppressEvents = false;
    }

    private DetectorAnnotationRegion? SelectedRegion()
    {
        Guid? id = SelectedRegionId;
        return id is null
            ? null
            : _annotation?.Regions.FirstOrDefault(
                region => region.Id == id);
    }

    private void ExpectedResultBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_suppressEvents &&
            _annotation is not null &&
            ExpectedResultBox.SelectedIndex >= 0)
        {
            _annotation.Expected =
                _expectedOptions[
                    ExpectedResultBox.SelectedIndex]
                    .Value;
            RaiseAnnotationChanged();
        }
    }

    private void NotesTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!_suppressEvents &&
            _annotation is not null)
        {
            _annotation.Notes = NotesTextBox.Text;
            RaiseAnnotationChanged();
        }
    }

    private void RegionList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_suppressEvents)
        {
            UpdateRegionEditor();
            SelectedRegionChanged?.Invoke(
                this,
                EventArgs.Empty);
        }
    }

    private void RegionLabelTextBox_LostFocus(
        object sender,
        RoutedEventArgs e)
    {
        DetectorAnnotationRegion? region =
            SelectedRegion();
        if (_suppressEvents || region is null)
        {
            return;
        }
        string label =
            RegionLabelTextBox.Text.Trim();
        region.Label = string.IsNullOrWhiteSpace(label)
            ? "Detection area"
            : label;
        RefreshRegions(region.Id);
        RaiseAnnotationChanged();
    }

    private void DeleteRegion_Click(
        object sender,
        RoutedEventArgs e)
    {
        DetectorAnnotationRegion? region =
            SelectedRegion();
        if (_annotation is null || region is null)
        {
            return;
        }
        _annotation.Regions.Remove(region);
        RefreshRegions();
        RaiseAnnotationChanged();
    }

    private void RaiseAnnotationChanged() =>
        AnnotationChanged?.Invoke(
            this,
            EventArgs.Empty);
}
