using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Pages;

namespace ExpeditionsMacro.App;

public partial class MainWindow
{
    private const double ExpandedNavigationWidth = 216;
    private const double CollapsedNavigationWidth = 60;
    private const double ResponsiveNavigationBreakpoint = 1110;
    private bool _navigationCollapsedBeforeForced;
    private bool _navigationForcedCollapsed;
    private bool _navigationRailCollapsed;

    private RadioButton[] NavigationButtons =>
    [
        DashboardNav,
        MacroPlanNav,
        PlacementNav,
        RecordingsNav,
        DebugNav,
        SettingsNav,
    ];

    private void InitializeNavigationRail()
    {
        foreach (RadioButton button in
                 NavigationButtons)
        {
            string label =
                button.Tag as string ??
                "Navigation";
            button.ToolTip = label;
            AutomationProperties.SetName(
                button,
                label);
        }
        SetNavigationRailCollapsed(false);
        SizeChanged += NavigationRail_SizeChanged;
    }

    private void NavigationRailToggle_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_navigationForcedCollapsed)
        {
            return;
        }
        SetNavigationRailCollapsed(
            !_navigationRailCollapsed);
    }

    private void NavigationRail_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (_snapshotMode)
        {
            return;
        }

        bool forceCollapsed =
            e.NewSize.Width <
                ResponsiveNavigationBreakpoint;
        if (forceCollapsed ==
            _navigationForcedCollapsed)
        {
            return;
        }

        if (forceCollapsed)
        {
            _navigationCollapsedBeforeForced =
                _navigationRailCollapsed;
            _navigationForcedCollapsed = true;
            SetNavigationRailCollapsed(true);
            return;
        }

        bool restoreCollapsed =
            _navigationCollapsedBeforeForced;
        _navigationForcedCollapsed = false;
        SetNavigationRailCollapsed(
            restoreCollapsed);
    }

    private void SetNavigationRailCollapsed(
        bool collapsed)
    {
        _navigationRailCollapsed = collapsed;
        double width = collapsed
            ? CollapsedNavigationWidth
            : ExpandedNavigationWidth;
        NavigationColumn.Width =
            new GridLength(width);
        TitleNavigationColumn.Width =
            new GridLength(width);
        BrandContent.Visibility = collapsed
            ? Visibility.Collapsed
            : Visibility.Visible;
        WorkspaceHeader.Visibility = collapsed
            ? Visibility.Collapsed
            : Visibility.Visible;
        ToolsHeader.Visibility = collapsed
            ? Visibility.Collapsed
            : Visibility.Visible;
        WorkspaceHeaderRow.Margin = collapsed
            ? new Thickness(0, 10, 0, 7)
            : new Thickness(10, 10, 10, 7);

        foreach (RadioButton button in
                 NavigationButtons)
        {
            button.Content = collapsed
                ? null
                : button.Tag;
        }

        NavigationRailToggleButton.ToolTip =
            _navigationForcedCollapsed
                ? "Navigation stays compact at this window size"
                : collapsed
                    ? "Expand navigation"
                    : "Collapse navigation";
        NavigationRailToggleButton.IsEnabled =
            !_navigationForcedCollapsed;
        AutomationProperties.SetName(
            NavigationRailToggleButton,
            NavigationRailToggleButton
                .ToolTip.ToString()!);
        NavigationRailToggleButton
            .HorizontalAlignment = collapsed
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Right;
        NavigationRailToggleButton.Margin =
            new Thickness(0);

        SetFooterButtonCollapsed(
            SetupGuideButton,
            collapsed,
            "Setup guide");
        SetFooterButtonCollapsed(
            JoinDiscordButton,
            collapsed,
            "Join Discord");
        OperationLabel.Visibility = collapsed
            ? Visibility.Collapsed
            : Visibility.Visible;
        HotkeyHint.Visibility = collapsed
            ? Visibility.Collapsed
            : Visibility.Visible;
        VersionLabel.Visibility = collapsed
            ? Visibility.Collapsed
            : Visibility.Visible;
        OperationStatusBorder.Margin = collapsed
            ? new Thickness(10, 0, 10, 8)
            : new Thickness(12, 0, 12, 6);
        OperationStatusBorder.Padding = collapsed
            ? new Thickness(0)
            : new Thickness(10);
        OperationStatusBorder.Height = collapsed
            ? 36
            : double.NaN;
        OperationSummary.HorizontalAlignment =
            collapsed
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Left;
        OperationStatusContent.VerticalAlignment =
            collapsed
                ? VerticalAlignment.Center
                : VerticalAlignment.Stretch;
        OperationDot.Margin = collapsed
            ? new Thickness(0)
            : new Thickness(0, 0, 8, 0);
    }

    private void SetFooterButtonCollapsed(
        Button button,
        bool collapsed,
        string expandedContent)
    {
        button.Content = collapsed
            ? null
            : expandedContent;
        button.Margin = collapsed
            ? new Thickness(10, 0, 10, 8)
            : new Thickness(12, 0, 12, 8);
        if (collapsed)
        {
            button.Style =
                (Style)FindResource(
                    "IconButton");
            button.Width = 40;
            return;
        }

        button.ClearValue(
            FrameworkElement.StyleProperty);
        button.ClearValue(
            FrameworkElement.WidthProperty);
    }

    internal void SetNavigationRailCollapsedForSnapshot(
        bool collapsed) =>
        SetNavigationRailCollapsed(collapsed);

    internal void SetPlacementCatalogCollapsedForSnapshot(
        bool collapsed)
    {
        if (_pages.TryGetValue(
                "Placement Setup",
                out IAppPage? page) &&
            page is PlacementModelsPage placement)
        {
            placement
                .SetCatalogCollapsedForSnapshot(
                    collapsed);
        }
    }
}
