using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class PlacementFastEditorView
{
    private const double CompactWorkspaceBreakpoint = 980;
    private const double StackedRouteHeaderBreakpoint = 640;
    private const double CompactMapHeight = 520;
    private const double CompactStepsHeight = 320;
    private const double CompactWorkspaceGap = 14;
    private bool _compactWorkspace;

    private void Editor_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        bool compact =
            e.NewSize.Width <
            CompactWorkspaceBreakpoint;
        bool layoutChanged =
            _compactWorkspace != compact ||
            !(FastWorkspaceGrid.Height > 0);
        _compactWorkspace = compact;
        ApplyResponsiveActionLayout(e.NewSize.Width);
        ApplyResponsiveRouteHeaderLayout(
            e.NewSize.Width);
        if (!layoutChanged)
        {
            return;
        }

        ApplyResponsiveWorkspaceLayout();
    }

    private void WorkspaceViewport_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (_compactWorkspace)
        {
            return;
        }

        FastWorkspaceGrid.Height =
            Math.Max(1, e.NewSize.Height);
    }

    internal void SetCompactSnapshotViewport(
        bool showPlacementSteps)
    {
        if (!_compactWorkspace)
        {
            throw new InvalidOperationException(
                "The compact Placement Setup snapshot was not using the responsive workspace.");
        }

        UpdateLayout();
        if (showPlacementSteps)
        {
            FastTimingButton.BringIntoView();
            VerifyScrollableStepFieldsForSnapshot();
        }
        else
        {
            FastRouteControls.BringIntoView();
        }
        FastWorkspaceScrollViewer.UpdateLayout();
    }

    private void VerifyScrollableStepFieldsForSnapshot()
    {
        PlacementStepRow afterStart =
            FastStepsList.Items
                .OfType<PlacementStepRow>()
                .First(row =>
                    row.Phase ==
                    PlacementPhase.AfterStart);
        FastStepsList.SelectedItem = afterStart;
        FastStepsList.ScrollIntoView(afterStart);
        FastStepsList.UpdateLayout();
        if (FastStepsList.ItemContainerGenerator
                .ContainerFromItem(afterStart) is not
            ListBoxItem container)
        {
            throw new InvalidOperationException(
                "The compact Placement Setup snapshot did not realize its After Start step.");
        }

        ScrollViewer? viewer =
            FindVisualChild<ScrollViewer>(
                FastStepsList);
        ComboBox? autoUpgrade =
            FindVisualChild<ComboBox>(
                container,
                control =>
                    string.Equals(
                        AutomationProperties.GetName(
                            control),
                        "Auto upgrade priority",
                        StringComparison.Ordinal));
        TextBox? delay =
            FindVisualChild<TextBox>(
                container,
                control =>
                    string.Equals(
                        AutomationProperties.GetName(
                            control),
                        "After Start delay",
                        StringComparison.Ordinal));
        if (viewer is null ||
            autoUpgrade is null ||
            delay is null)
        {
            throw new InvalidOperationException(
                "The compact Placement Setup snapshot could not resolve its scrollable step fields.");
        }
        if (viewer.CanContentScroll)
        {
            throw new InvalidOperationException(
                "Placement steps must use pixel scrolling so oversized rows remain reachable.");
        }
        if (!KeyboardNavigation.GetIsTabStop(
                autoUpgrade) ||
            !KeyboardNavigation.GetIsTabStop(delay))
        {
            throw new InvalidOperationException(
                "Placement step fields must remain in the keyboard tab order.");
        }

        viewer.ScrollToTop();
        viewer.UpdateLayout();
        viewer.LineDown();
        viewer.UpdateLayout();
        double lineDelta = viewer.VerticalOffset;
        if (lineDelta <= 0 ||
            lineDelta >= container.ActualHeight)
        {
            throw new InvalidOperationException(
                "Placement step wheel scrolling did not advance by a pixel range within the oversized row.");
        }

        const double fractionalOffset = 0.5;
        viewer.ScrollToVerticalOffset(
            fractionalOffset);
        viewer.UpdateLayout();
        if (Math.Abs(
                viewer.VerticalOffset -
                fractionalOffset) > 0.01)
        {
            throw new InvalidOperationException(
                "Placement step scrollbar movement was coerced to a whole-item offset.");
        }

        autoUpgrade.BringIntoView();
        delay.BringIntoView();
        viewer.UpdateLayout();
        VerifyFullyVisible(
            autoUpgrade,
            viewer,
            "Auto upgrade priority");
        VerifyFullyVisible(
            delay,
            viewer,
            "After Start delay");

        double restoredInnerOffset =
            viewer.VerticalOffset;
        viewer.ScrollToEnd();
        double restoredOuterOffset =
            FastWorkspaceScrollViewer.VerticalOffset;
        FastWorkspaceScrollViewer.ScrollToTop();
        FastWorkspaceScrollViewer.UpdateLayout();
        double outerOffset =
            FastWorkspaceScrollViewer.VerticalOffset;
        MouseWheelEventArgs wheel =
            new(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                -120)
            {
                RoutedEvent =
                    Mouse.PreviewMouseWheelEvent,
            };
        delay.RaiseEvent(wheel);
        FastWorkspaceScrollViewer.UpdateLayout();
        if (FastWorkspaceScrollViewer.VerticalOffset <=
            outerOffset)
        {
            throw new InvalidOperationException(
                "The compact Placement Setup trapped a downward wheel gesture at the placement-step boundary.");
        }

        viewer.ScrollToTop();
        FastWorkspaceScrollViewer.ScrollToEnd();
        FastWorkspaceScrollViewer.UpdateLayout();
        outerOffset =
            FastWorkspaceScrollViewer.VerticalOffset;
        wheel =
            new MouseWheelEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                120)
            {
                RoutedEvent =
                    Mouse.PreviewMouseWheelEvent,
            };
        autoUpgrade.RaiseEvent(wheel);
        FastWorkspaceScrollViewer.UpdateLayout();
        if (FastWorkspaceScrollViewer.VerticalOffset >=
            outerOffset)
        {
            throw new InvalidOperationException(
                "The compact Placement Setup trapped an upward wheel gesture at the placement-step boundary.");
        }

        FastWorkspaceScrollViewer.ScrollToVerticalOffset(
            restoredOuterOffset);
        FastWorkspaceScrollViewer.UpdateLayout();
        viewer.ScrollToVerticalOffset(
            restoredInnerOffset);
        viewer.UpdateLayout();
    }

    private void FastStepsList_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (!_compactWorkspace ||
            e.Handled ||
            e.Delta == 0)
        {
            return;
        }

        ScrollViewer? viewer =
            FindVisualChild<ScrollViewer>(
                FastStepsList);
        if (viewer is null)
        {
            return;
        }

        const double boundaryTolerance = 0.5;
        bool atBoundary =
            e.Delta > 0
                ? viewer.VerticalOffset <=
                  boundaryTolerance
                : viewer.VerticalOffset >=
                  viewer.ScrollableHeight -
                  boundaryTolerance;
        if (!atBoundary)
        {
            return;
        }

        e.Handled = true;
        MouseWheelEventArgs forwarded =
            new(
                e.MouseDevice,
                e.Timestamp,
                e.Delta)
            {
                RoutedEvent =
                    Mouse.MouseWheelEvent,
            };
        FastWorkspaceScrollViewer.RaiseEvent(
            forwarded);
    }

    private static void VerifyFullyVisible(
        FrameworkElement control,
        ScrollViewer viewer,
        string name)
    {
        Point topLeft =
            control.TranslatePoint(
                new Point(),
                viewer);
        const double tolerance = 0.5;
        if (topLeft.Y < -tolerance ||
            topLeft.Y +
            control.ActualHeight >
            viewer.ViewportHeight +
            tolerance)
        {
            throw new InvalidOperationException(
                $"The {name} field was still clipped after keyboard-style BringIntoView.");
        }
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent,
        Predicate<T> match)
        where T : DependencyObject
    {
        for (int index = 0;
             index <
             VisualTreeHelper.GetChildrenCount(
                 parent);
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child is T candidate &&
                match(candidate))
            {
                return candidate;
            }
            T? nested =
                FindVisualChild(
                    child,
                    match);
            if (nested is not null)
            {
                return nested;
            }
        }
        return null;
    }

    private void ApplyResponsiveWorkspaceLayout()
    {
        if (_compactWorkspace)
        {
            FastMapColumn.Width =
                new GridLength(1, GridUnitType.Star);
            FastWorkspaceColumnGap.Width =
                new GridLength(0);
            FastStepsColumn.Width =
                new GridLength(0);
            FastMapRow.Height =
                new GridLength(CompactMapHeight);
            FastWorkspaceRowGap.Height =
                new GridLength(CompactWorkspaceGap);
            FastStepsRow.Height =
                new GridLength(CompactStepsHeight);
            Grid.SetRow(FastStepsPanel, 2);
            Grid.SetColumn(FastStepsPanel, 0);
            FastWorkspaceScrollViewer
                .VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto;
            FastWorkspaceGrid.Height =
                CompactMapHeight +
                CompactWorkspaceGap +
                CompactStepsHeight;
            return;
        }

        FastMapColumn.Width =
            new GridLength(1, GridUnitType.Star);
        FastWorkspaceColumnGap.Width =
            new GridLength(14);
        FastStepsColumn.Width =
            new GridLength(300);
        FastMapRow.Height =
            new GridLength(1, GridUnitType.Star);
        FastWorkspaceRowGap.Height =
            new GridLength(0);
        FastStepsRow.Height =
            new GridLength(0);
        Grid.SetRow(FastStepsPanel, 0);
        Grid.SetColumn(FastStepsPanel, 2);
        FastWorkspaceScrollViewer
            .VerticalScrollBarVisibility =
            ScrollBarVisibility.Disabled;
        FastWorkspaceGrid.Height = Math.Max(
            1,
            FastWorkspaceScrollViewer.ActualHeight);
    }

    private void ApplyResponsiveActionLayout(
        double availableWidth)
    {
        if (_compactWorkspace)
        {
            Grid.SetRow(FastActionPanel, 1);
            Grid.SetColumn(FastActionPanel, 0);
            Grid.SetColumnSpan(FastActionPanel, 2);
            FastActionPanel.Margin =
                new Thickness(0, 8, 0, 0);
            FastActionPanel.HorizontalAlignment =
                HorizontalAlignment.Left;
            FastActionPanel.Width =
                Math.Max(1, availableWidth);
            Grid.SetRow(FastOperationProgress, 2);
            return;
        }

        Grid.SetRow(FastActionPanel, 0);
        Grid.SetColumn(FastActionPanel, 1);
        Grid.SetColumnSpan(FastActionPanel, 1);
        FastActionPanel.Margin =
            new Thickness(18, 0, 0, 0);
        FastActionPanel.HorizontalAlignment =
            HorizontalAlignment.Right;
        FastActionPanel.ClearValue(WidthProperty);
        Grid.SetRow(FastOperationProgress, 1);
    }

    private void ApplyResponsiveRouteHeaderLayout(
        double availableWidth)
    {
        bool stacked =
            availableWidth <
            StackedRouteHeaderBreakpoint;
        Grid.SetRow(
            FastPlaybackModeSelector,
            stacked ? 1 : 0);
        Grid.SetColumn(
            FastPlaybackModeSelector,
            stacked ? 0 : 1);
        Grid.SetColumnSpan(
            FastPlaybackModeSelector,
            stacked ? 2 : 1);
        FastPlaybackModeSelector.Margin =
            stacked
                ? new Thickness(0, 12, 0, 0)
                : new Thickness(24, 0, 0, 0);
        FastPlaybackModeSelector
            .HorizontalAlignment =
            stacked
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Right;
    }
}
