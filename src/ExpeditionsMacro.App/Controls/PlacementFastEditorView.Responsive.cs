using System.Windows;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Controls;

public partial class PlacementFastEditorView
{
    private const double CompactWorkspaceBreakpoint = 980;
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
        }
        else
        {
            FastRouteControls.BringIntoView();
        }
        FastWorkspaceScrollViewer.UpdateLayout();
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
}
