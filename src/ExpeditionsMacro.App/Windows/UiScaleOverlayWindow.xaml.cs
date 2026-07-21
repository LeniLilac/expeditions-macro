using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Windows;

public partial class UiScaleOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTopmost = new(-1);

    private readonly RobloxWindow _robloxWindow;
    private readonly IRobloxAutomation _automation;
    private readonly DispatcherTimer _positionTimer;
    private nint _handle;

    public UiScaleOverlayWindow(RobloxWindow robloxWindow, IRobloxAutomation automation)
    {
        _robloxWindow = robloxWindow;
        _automation = automation;
        InitializeComponent();
        OverlayImage.Source = LoadOverlayImage();
        _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _positionTimer.Tick += PositionTimer_Tick;
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) => _positionTimer.Stop();
    }

    public void RefreshPosition()
    {
        if (_handle != nint.Zero) PositionOverRoblox();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        nint style = GetWindowLongPtr(_handle, GwlExStyle);
        SetWindowLongPtr(_handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
        PositionOverRoblox();
        _positionTimer.Start();
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            PositionOverRoblox();
        }
        catch
        {
            Close();
        }
    }

    private void PositionOverRoblox()
    {
        ClientBounds bounds = _automation.GetClientBounds(_robloxWindow);
        if (bounds.Width != RobloxClientProfile.Width || bounds.Height != RobloxClientProfile.Height)
        {
            throw new InvalidOperationException("Roblox is no longer at the standard client size.");
        }
        if (!SetWindowPos(
            _handle,
            HwndTopmost,
            bounds.X,
            bounds.Y,
            RobloxClientProfile.Width,
            RobloxClientProfile.Height,
            SwpNoActivate | SwpShowWindow))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not position the calibration overlay over Roblox.");
        }
    }

    private static BitmapSource LoadOverlayImage()
    {
        Uri resourceUri = new("pack://application:,,,/Resources/standard-ui-overlay.png", UriKind.Absolute);
        var resource = Application.GetResourceStream(resourceUri)
            ?? throw new InvalidOperationException("The UI scale calibration image is missing from this build.");
        using Stream stream = resource.Stream;
        PngBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource image = decoder.Frames[0];
        if (image.PixelWidth != RobloxClientProfile.Width || image.PixelHeight != RobloxClientProfile.Height)
        {
            throw new InvalidOperationException(
                $"The UI scale calibration image must be {RobloxClientProfile.Width} by {RobloxClientProfile.Height} pixels.");
        }
        image.Freeze();
        return image;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
