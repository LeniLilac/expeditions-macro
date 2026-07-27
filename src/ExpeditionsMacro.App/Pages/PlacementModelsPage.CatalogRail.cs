using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ExpeditionsMacro.App.Controls;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private const double ExpandedCatalogWidth = 320;
    private const double CollapsedCatalogWidth = 56;
    private bool _catalogCollapsed;

    private void InitializeCatalogRail() =>
        SetCatalogCollapsed(false);

    private void CatalogRailToggle_Click(
        object sender,
        RoutedEventArgs e) =>
        SetCatalogCollapsed(!_catalogCollapsed);

    private void SetCatalogCollapsed(
        bool collapsed)
    {
        _catalogCollapsed = collapsed;
        CatalogColumn.Width =
            new GridLength(
                collapsed
                    ? CollapsedCatalogWidth
                    : ExpandedCatalogWidth);
        CatalogLayout.Margin = collapsed
            ? new Thickness(0, 18, 0, 14)
            : new Thickness(12, 18, 12, 14);
        CatalogHeader.Margin = collapsed
            ? new Thickness(0, 0, 0, 10)
            : new Thickness(6, 0, 6, 10);
        Grid.SetColumn(
            CatalogRailToggleButton,
            collapsed
                ? 0
                : 2);
        Grid.SetColumnSpan(
            CatalogRailToggleButton,
            collapsed
                ? 3
                : 1);
        CatalogRailToggleButton.HorizontalAlignment =
            collapsed
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Right;
        ModelsHeaderText.Visibility = collapsed
            ? Visibility.Collapsed
            : Visibility.Visible;

        CatalogRailToggleButton.ToolTip =
            collapsed
                ? "Expand placement routes"
                : "Collapse placement routes";
        AutomationProperties.SetName(
            CatalogRailToggleButton,
            CatalogRailToggleButton
                .ToolTip.ToString()!);

        ApplyCatalogContentVisibility();
    }

    private void ApplyCatalogContentVisibility()
    {
        bool fast = FastWorkflow;
        ModelsList.Visibility =
            !_catalogCollapsed && !fast
                ? Visibility.Visible
                : Visibility.Collapsed;
        FastSetupList.Visibility =
            !_catalogCollapsed && fast
                ? Visibility.Visible
                : Visibility.Collapsed;
        NewModelButton.Visibility =
            !_catalogCollapsed && !fast
                ? Visibility.Visible
                : Visibility.Collapsed;
        DeleteModelButton.Visibility =
            !_catalogCollapsed && !fast
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    internal void SetCatalogCollapsedForSnapshot(
        bool collapsed) =>
        SetCatalogCollapsed(collapsed);
}
