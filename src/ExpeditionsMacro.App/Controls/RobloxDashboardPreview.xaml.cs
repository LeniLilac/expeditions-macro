using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.App.Controls;

public partial class RobloxDashboardPreview :
    UserControl
{
    private readonly WindowsRobloxWindowPin _pin =
        new();
    private readonly DispatcherTimer _refreshTimer;
    private AppServices? _services;
    private bool _pinned = true;
    private bool _dashboardActive = true;
    private bool _ownerVisible = true;
    private bool _nativeDockingEnabled = true;
    private bool _releaseForClose;
    private int _ownedDialogSuspendDepth;
    private Window? _owner;

    private enum PreviewReleaseDisposition
    {
        Restore,
        MinimizeRetained,
    }

    public RobloxDashboardPreview()
    {
        InitializeComponent();
        _refreshTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            (_, _) => RefreshPin(),
            Dispatcher);
    }

    public bool RequiresVisibleOwner =>
        _nativeDockingEnabled &&
        !_releaseForClose &&
        _pinned &&
        _dashboardActive;

    public void Initialize(AppServices services)
    {
        _services ??= services;
    }

    public void SetNativeDockingEnabled(
        bool enabled)
    {
        _nativeDockingEnabled = enabled;
        if (!enabled)
        {
            _ = TryDetach(out _);
            ShowPlaceholder(
                "Roblox pinning is disabled while rendering UI snapshots.");
        }
    }

    public bool SetDashboardActive(
        bool active,
        out string error)
    {
        _dashboardActive = active;
        if (!active)
        {
            return TryAutoDetach(out error);
        }

        error = string.Empty;
        RefreshPin();
        return true;
    }

    public bool SetOwnerVisible(
        bool visible,
        out string error)
    {
        _ownerVisible = visible;
        if (!visible)
        {
            return TryAutoDetach(out error);
        }

        error = string.Empty;
        RefreshPin();
        return true;
    }

    public bool SetPinned(
        bool pinned,
        out string error)
    {
        _pinned = pinned;
        if (!pinned)
        {
            bool detached =
                TryDetach(out error);
            if (detached)
            {
                ShowPlaceholder(
                    "Roblox is unpinned and remains in its normal window.");
            }
            return detached;
        }

        error = string.Empty;
        RefreshPin();
        return true;
    }

    public bool TryDetach(out string error)
        => TryRelease(
            PreviewReleaseDisposition.Restore,
            out error);

    private bool TryAutoDetach(out string error)
        => TryRelease(
            PreviewReleaseDisposition
                .MinimizeRetained,
            out error);

    public bool TryPrepareForClose(
        out string error)
    {
        _releaseForClose = true;
        if (TryDetach(out error))
        {
            return true;
        }

        _releaseForClose = false;
        RefreshPin();
        return false;
    }

    private bool TryRelease(
        PreviewReleaseDisposition disposition,
        out string error)
    {
        bool detached =
            disposition ==
                PreviewReleaseDisposition
                    .MinimizeRetained
                ? _pin.TryUnpinAndMinimize(
                    out error)
                : _pin.TryUnpin(
                    out error);
        if (!detached)
        {
            PreviewStatusText.Text = error;
            return false;
        }

        PlaceholderPanel.Visibility =
            Visibility.Visible;
        return true;
    }

    public void RefreshNow() =>
        RefreshPin();

    public bool TrySuspendForOwnedDialog(
        out string error)
    {
        if (_ownedDialogSuspendDepth > 0)
        {
            _ownedDialogSuspendDepth++;
            error = string.Empty;
            return true;
        }

        if (!_pin.TrySuspend(out error))
        {
            return false;
        }

        _ownedDialogSuspendDepth = 1;
        ShowPlaceholder(
            "Roblox pinning pauses while a macro message is open.");
        return true;
    }

    public void ResumeAfterOwnedDialog()
    {
        if (_ownedDialogSuspendDepth <= 0)
        {
            return;
        }

        _ownedDialogSuspendDepth--;
        if (_ownedDialogSuspendDepth == 0)
        {
            RefreshPin();
        }
    }

    private void Preview_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _owner = Window.GetWindow(this);
        if (_owner is not null)
        {
            _owner.Activated +=
                Owner_ActivationChanged;
            _owner.Deactivated +=
                Owner_ActivationChanged;
        }
        _refreshTimer.Start();
        RefreshPin();
    }

    private void Preview_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        if (_owner is not null)
        {
            _owner.Activated -=
                Owner_ActivationChanged;
            _owner.Deactivated -=
                Owner_ActivationChanged;
            _owner = null;
        }
        _ = _releaseForClose
            ? TryDetach(out _)
            : TryAutoDetach(out _);
    }

    private void Owner_ActivationChanged(
        object? sender,
        EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(RefreshPin));
    }

    private void RefreshPin()
    {
        if (!_nativeDockingEnabled ||
            _releaseForClose ||
            _ownedDialogSuspendDepth > 0 ||
            !_pinned ||
            !_dashboardActive ||
            !_ownerVisible ||
            !IsLoaded ||
            _services is null)
        {
            return;
        }

        try
        {
            Window? owner =
                _owner ??
                Window.GetWindow(this);
            if (owner is null ||
                owner.WindowState ==
                    WindowState.Minimized ||
                !TryGetPinBounds(
                    owner,
                    out WindowBounds target))
            {
                if (!_pin.TryUnpinAndMinimize(
                    out string detachError))
                {
                    ShowPlaceholder(detachError);
                    return;
                }
                ShowPlaceholder(
                    "Roblox was minimized because the live view is not fully visible.");
                return;
            }

            nint ownerHandle =
                new WindowInteropHelper(owner)
                    .Handle;
            RobloxWindow? source = null;
            nint sourceHandle =
                _pin.SourceHandle;
            if (sourceHandle == nint.Zero)
            {
                source =
                    _services.Automation.FindWindow();
                sourceHandle =
                    source?.Handle ??
                    nint.Zero;
            }
            if (!_pin.IsDashboardExposed(
                    ownerHandle,
                    sourceHandle))
            {
                if (!_pin.TrySuspend(
                    out string detachError))
                {
                    ShowPlaceholder(detachError);
                    return;
                }
                ShowPlaceholder(
                    "Roblox pinning pauses while another window covers the Dashboard.");
                return;
            }

            if (_pin.IsPinned)
            {
                _pin.UpdateBounds(target);
                PlaceholderPanel.Visibility =
                    Visibility.Collapsed;
                return;
            }

            source ??=
                _services.Automation.FindWindow();
            if (source is null)
            {
                ShowPlaceholder(
                    "Roblox is not open. It will pin here automatically.");
                return;
            }

            _pin.Pin(
                source.Value.Handle,
                target);
            PlaceholderPanel.Visibility =
                Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            ShowPlaceholder(
                $"Windows could not pin Roblox: {exception.Message}");
        }
    }

    private bool TryGetPinBounds(
        Window owner,
        out WindowBounds bounds)
    {
        bounds = default;
        if (!PinTarget.IsVisible ||
            PinTarget.ActualWidth < 1 ||
            PinTarget.ActualHeight < 1)
        {
            return false;
        }

        Point targetTopLeft =
            PinTarget.PointToScreen(
                new Point(0, 0));
        Point ownerTopLeft =
            owner.PointToScreen(
                new Point(0, 0));
        Point ownerBottomRight =
            owner.PointToScreen(
                new Point(
                    owner.ActualWidth,
                    owner.ActualHeight));
        int x =
            checked((int)Math.Round(
                targetTopLeft.X));
        int y =
            checked((int)Math.Round(
                targetTopLeft.Y));
        int ownerLeft =
            checked((int)Math.Floor(
                ownerTopLeft.X));
        int ownerTop =
            checked((int)Math.Floor(
                ownerTopLeft.Y));
        int ownerRight =
            checked((int)Math.Ceiling(
                ownerBottomRight.X));
        int ownerBottom =
            checked((int)Math.Ceiling(
                ownerBottomRight.Y));
        if (x < ownerLeft ||
            y < ownerTop ||
            x + WindowsRobloxWindowPin.ClientWidth >
                ownerRight ||
            y + WindowsRobloxWindowPin.ClientHeight >
                ownerBottom)
        {
            return false;
        }

        bounds = new WindowBounds(
            x,
            y,
            WindowsRobloxWindowPin.ClientWidth,
            WindowsRobloxWindowPin.ClientHeight);
        return true;
    }

    private void ShowPlaceholder(
        string message)
    {
        PreviewStatusText.Text = message;
        PlaceholderPanel.Visibility =
            Visibility.Visible;
    }
}
