using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.App;

public partial class MainWindow
{
    private const double DefaultMinimumWidth = 960;
    private const double DefaultMinimumHeight = 640;
    private HwndSource? _windowSource;
    private HwndSourceHook? _windowHook;

    private void Window_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        nint handle =
            new WindowInteropHelper(this).Handle;
        _windowSource =
            HwndSource.FromHwnd(handle);
        _windowHook = WindowMessage;
        _windowSource?.AddHook(_windowHook);
    }

    private void Window_Closed(
        object? sender,
        EventArgs e)
    {
        if (_windowSource is not null &&
            _windowHook is not null)
        {
            _windowSource.RemoveHook(_windowHook);
        }
        _windowHook = null;
        _windowSource = null;
    }

    private nint WindowMessage(
        nint window,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        int minimumWidth = checked(
            (int)Math.Ceiling(
                MinWidth * dpi.DpiScaleX));
        int minimumHeight = checked(
            (int)Math.Ceiling(
                MinHeight * dpi.DpiScaleY));
        if (WindowsMaximizedWorkArea.TryApply(
                window,
                message,
                lParam,
                minimumWidth,
                minimumHeight))
        {
            handled = true;
        }
        return nint.Zero;
    }

    private void EnsureWorkspaceSize(string key)
    {
        if (_snapshotMode ||
            WindowState != WindowState.Normal ||
            !NeedsExpandedWorkspace(key))
        {
            return;
        }

        nint handle =
            new WindowInteropHelper(this).Handle;
        DpiScale dpi =
            VisualTreeHelper.GetDpi(this);
        if (!WindowsWindowWorkArea.TryGet(
                handle,
                dpi.DpiScaleX,
                dpi.DpiScaleY,
                out DesktopWorkAreaBounds workArea))
        {
            return;
        }

        MinWidth = Math.Min(
            DefaultMinimumWidth,
            workArea.Width);
        MinHeight = Math.Min(
            DefaultMinimumHeight,
            workArea.Height);
        DesktopWorkAreaBounds fitted =
            WindowsWindowWorkArea
                .FitNormalBounds(
                    new DesktopWorkAreaBounds(
                        Left,
                        Top,
                        ActualWidth > 0
                            ? ActualWidth
                            : Width,
                        ActualHeight > 0
                            ? ActualHeight
                            : Height),
                    workArea,
                    desiredWidth: 1660,
                    desiredHeight: 1040);
        Width = fitted.Width;
        Height = fitted.Height;
        Left = fitted.Left;
        Top = fitted.Top;
    }

    private bool NeedsExpandedWorkspace(
        string key) =>
        string.Equals(
            key,
            "Dashboard",
            StringComparison.OrdinalIgnoreCase) ||
        (string.Equals(
            key,
            "Placement Setup",
            StringComparison.OrdinalIgnoreCase) &&
         _services.Settings.FastNoAlignEnabled);
}
