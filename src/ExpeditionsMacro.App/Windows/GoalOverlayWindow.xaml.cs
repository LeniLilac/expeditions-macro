using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Windows;

public partial class GoalOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTopmost = new(-1);
    private readonly ScreenRegion _bounds;

    public GoalOverlayWindow(CameraModel model, IRobloxAutomation automation)
    {
        InitializeComponent();
        RobloxWindow window = automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");
        ClientBounds client = automation.GetClientBounds(window);
        _bounds = client.AsRegion();
        GoalImage.Source = BitmapSourceFactory.Create(model.GoalOverlay);
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;
        nint style = GetWindowLongPtr(handle, GwlExStyle);
        SetWindowLongPtr(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
        SetWindowPos(handle, HwndTopmost, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, SwpNoActivate | SwpShowWindow);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
