using System.Windows;
using System.Windows.Controls;
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
            return TryDetach(out error);
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
            return TryDetach(out error);
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
    {
        bool detached =
            _pin.TryUnpin(
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

    private void Preview_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _refreshTimer.Start();
        RefreshPin();
    }

    private void Preview_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        _ = TryDetach(out _);
    }

    private void RefreshPin()
    {
        if (!_nativeDockingEnabled ||
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
                Window.GetWindow(this);
            if (owner is null ||
                owner.WindowState ==
                    WindowState.Minimized ||
                !TryGetPinBounds(
                    owner,
                    out WindowBounds target))
            {
                if (!_pin.TryUnpin(
                    out string detachError))
                {
                    ShowPlaceholder(detachError);
                    return;
                }
                ShowPlaceholder(
                    "Keep the Roblox live view fully visible to pin it.");
                return;
            }

            if (_pin.IsPinned)
            {
                _pin.UpdateBounds(target);
                PlaceholderPanel.Visibility =
                    Visibility.Collapsed;
                return;
            }

            RobloxWindow? source =
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
