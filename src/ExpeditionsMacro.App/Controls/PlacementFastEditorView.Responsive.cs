using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class PlacementFastEditorView
{
    private const double CompactWorkspaceBreakpoint = 980;
    private const double StackedRouteHeaderBreakpoint = 640;
    private bool _compactWorkspace;

    private void Editor_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        _compactWorkspace =
            e.NewSize.Width <
            CompactWorkspaceBreakpoint;
        ApplyResponsiveActionLayout(e.NewSize.Width);
        ApplyResponsiveRouteHeaderLayout(
            e.NewSize.Width);
    }

    private void WorkspaceViewport_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        // The map and Match Steps are intentionally one vertical
        // document. The outer viewer owns reachability at every size.
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
            FrameworkElement startGame =
                (FrameworkElement)
                FastStepsList
                    .ItemContainerGenerator
                    .ContainerFromItem(
                        FastStepsList.Items
                            .OfType<PlacementStepRow>()
                            .Single(step =>
                                step.IsStartGame));
            double startGameOffset =
                startGame.TranslatePoint(
                    new Point(),
                    FastWorkspaceGrid).Y;
            FastWorkspaceScrollViewer
                .ScrollToVerticalOffset(
                    Math.Max(
                        0,
                        startGameOffset - 72));
        }
        else
        {
            FastRouteControls.BringIntoView();
        }
        FastWorkspaceScrollViewer.UpdateLayout();
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
