using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ExpeditionsMacro.App.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class MacroPlanLoopEditor
{
    private MacroPlanLoopBlockNode? _settingsLoop;

    private void InitializeLoopSettings()
    {
        LoopSettingsDialog.ApplyRequested +=
            LoopSettingsDialog_ApplyRequested;
        LoopSettingsDialog.CancelRequested +=
            (_, _) => LoopSettingsPopup.IsOpen = false;
        LoopSettingsPopup.CustomPopupPlacementCallback =
            PlaceLoopSettingsPopup;
    }

    private void EditLoopSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_interactionEnabled ||
            sender is not FrameworkElement
            {
                Tag: MacroPlanLoopBlockNode loop,
            } button)
        {
            return;
        }
        OpenLoopSettings(loop, button);
    }

    internal void OpenLoopSettingsForSnapshot(
        MacroPlanLoopBlockNode loop)
    {
        StructureTree.ApplyTemplate();
        StructureTree.UpdateLayout();
        Button? target = FindSettingsButton(
            StructureTree,
            loop);
        if (target is null)
        {
            throw new InvalidOperationException(
                "The loop settings button was not generated for the UI snapshot.");
        }
        OpenLoopSettings(loop, target);
        LoopSettingsPopup.UpdateLayout();
    }

    internal bool TryGetLoopSettingsSnapshotVisual(
        FrameworkElement snapshotRoot,
        out FrameworkElement visual,
        out Point origin)
    {
        visual = LoopSettingsDialog;
        origin = default;
        if (!LoopSettingsPopup.IsOpen ||
            visual.ActualWidth <= 0 ||
            visual.ActualHeight <= 0)
        {
            return false;
        }
        Point screenOrigin = visual.PointToScreen(
            new Point());
        origin = snapshotRoot.PointFromScreen(
            screenOrigin);
        return true;
    }

    private void OpenLoopSettings(
        MacroPlanLoopBlockNode loop,
        FrameworkElement button)
    {
        _settingsLoop = loop;
        int repeatCount = int.TryParse(
                loop.AmountText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
            ? parsed
            : 2;
        LoopSettingsDialog.SetValues(
            loop.BlockLabel,
            loop.Forever,
            repeatCount);
        LoopSettingsPopup.PlacementTarget = button;
        LoopSettingsPopup.IsOpen = true;
        Dispatcher.BeginInvoke(
            LoopSettingsDialog.FocusInitial);
    }

    private void LoopSettingsDialog_ApplyRequested(
        object? sender,
        MacroLoopSettingsApplyEventArgs e)
    {
        if (_settingsLoop is null)
        {
            LoopSettingsPopup.IsOpen = false;
            return;
        }
        _updating = true;
        try
        {
            _settingsLoop.AmountText =
                e.RepeatCount.ToString(
                    CultureInfo.InvariantCulture);
            _settingsLoop.Forever = e.Forever;
            if (e.Forever)
            {
                MacroPlanStructureMove.MakeForever(
                    RootBlocks,
                    _settingsLoop);
            }
        }
        finally
        {
            _updating = false;
        }
        LoopSettingsPopup.IsOpen = false;
        CompleteChange();
    }

    private void LoopSettingsPopup_Closed(
        object sender,
        EventArgs e)
    {
        _settingsLoop = null;
        LoopSettingsDialog.ShowError(string.Empty);
    }

    private static CustomPopupPlacement[]
        PlaceLoopSettingsPopup(
        Size popupSize,
        Size targetSize,
        Point offset) =>
        [
            new CustomPopupPlacement(
                new Point(
                    targetSize.Width -
                    popupSize.Width,
                    targetSize.Height + 6),
                PopupPrimaryAxis.Horizontal),
            new CustomPopupPlacement(
                new Point(
                    targetSize.Width -
                    popupSize.Width,
                    -popupSize.Height - 6),
                PopupPrimaryAxis.Horizontal),
        ];

    private static Button? FindSettingsButton(
        DependencyObject parent,
        MacroPlanLoopBlockNode loop)
    {
        for (int index = 0;
             index < VisualTreeHelper.GetChildrenCount(
                 parent);
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child is Button
                {
                    Tag:
                        MacroPlanLoopBlockNode candidate,
                } button &&
                ReferenceEquals(candidate, loop) &&
                string.Equals(
                    button.ToolTip as string,
                    "Edit loop settings",
                    StringComparison.Ordinal))
            {
                return button;
            }
            Button? nested =
                FindSettingsButton(child, loop);
            if (nested is not null)
            {
                return nested;
            }
        }
        return null;
    }
}
