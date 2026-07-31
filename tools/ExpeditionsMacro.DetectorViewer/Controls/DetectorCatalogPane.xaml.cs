using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using ExpeditionsMacro.DetectorViewer.Models;

namespace ExpeditionsMacro.DetectorViewer.Controls;

public partial class DetectorCatalogPane :
    UserControl
{
    private IReadOnlyList<DetectorCatalogItem> _items =
        [];
    private ICollectionView? _view;

    public DetectorCatalogPane()
    {
        InitializeComponent();
    }

    public event EventHandler? SelectedDetectorChanged;

    public DetectorCatalogItem? SelectedItem =>
        CatalogList.SelectedItem as
            DetectorCatalogItem;

    public void SetCatalog(
        IReadOnlyList<DetectorCatalogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        ListCollectionView view =
            new(items.ToList());
        view.GroupDescriptions.Add(
            new PropertyGroupDescription(
                nameof(DetectorCatalogItem.Group)));
        view.Filter = FilterItem;
        _view = view;
        CatalogList.ItemsSource = view;
        UpdateCount();
        if (CatalogList.SelectedItem is null)
        {
            CatalogList.SelectedItem =
                items.FirstOrDefault(item =>
                    !item.IsUnavailable) ??
                items.FirstOrDefault();
        }
    }

    public bool SelectDetector(string id)
    {
        DetectorCatalogItem? item =
            _items.FirstOrDefault(candidate =>
                candidate.Id.Equals(
                    id,
                    StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return false;
        }
        CatalogList.SelectedItem = item;
        CatalogList.ScrollIntoView(item);
        return true;
    }

    private bool FilterItem(object value)
    {
        if (value is not DetectorCatalogItem item)
        {
            return false;
        }
        bool includeUnavailable =
            IncludeUnavailableCheckBox.IsChecked ==
            true;
        if (!includeUnavailable &&
            item.IsUnavailable)
        {
            return false;
        }
        string[] tokens =
            SearchBox.Text
                .Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions
                        .TrimEntries);
        return tokens.All(token =>
            item.SearchText.Contains(
                token,
                StringComparison.OrdinalIgnoreCase));
    }

    private void SearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!IsInitialized ||
            SearchPlaceholder is null)
        {
            return;
        }
        SearchPlaceholder.Visibility =
            string.IsNullOrEmpty(SearchBox.Text)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        RefreshFilter();
    }

    private void Filter_Changed(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            RefreshFilter();
        }
    }

    private void RefreshFilter()
    {
        _view?.Refresh();
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (CountText is null)
        {
            return;
        }
        int visible =
            _view?.Cast<object>().Count() ??
            0;
        CountText.Text =
            $"{visible:N0} of {_items.Count:N0} detectors";
    }

    private void CatalogList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        SelectedDetectorChanged?.Invoke(
            this,
            EventArgs.Empty);
}
