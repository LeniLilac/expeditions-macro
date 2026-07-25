using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Services;

internal static class UiSnapshotRenderer
{
    private static readonly (
        string Key,
        string File,
        bool ShowPageEnd,
        bool ShowDebugUtilities)[] Pages =
    [
        ("Macro", "macro", false, false),
        ("Macro", "macro-status", true, false),
        ("Expeditions", "expeditions", false, false),
        ("Challenges", "challenges", false, false),
        ("Challenges", "challenges-status", true, false),
        ("Story", "story", false, false),
        ("Raid", "raid", false, false),
        ("Camera Models", "camera-models", false, false),
        ("Placement Setup", "placement-setup", false, false),
        ("Debug", "debug", false, false),
        ("Debug", "debug-refuel", true, false),
        ("Debug", "debug-utilities", false, true),
        ("Settings", "settings", false, false),
        ("Settings", "settings-debug", true, false),
    ];

    public static async Task RenderAsync(AppServices services, string outputDirectory)
    {
        string output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        MainWindow window = new(services, snapshotMode: true)
        {
            Width = 1200,
            Height = 780,
            Left = 0,
            Top = 0,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0.01,
        };
        VerifyBundledFont(window);
        window.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            await window.VerifyBackgroundModelRefreshAsync();
            foreach (AppTheme theme in new[] { AppTheme.Dark, AppTheme.Light })
            {
                ThemeService.Apply(theme);
                foreach ((
                    string key,
                    string file,
                    bool showPageEnd,
                    bool showDebugUtilities) in Pages)
                {
                    await window.SelectPageForSnapshotAsync(
                        key,
                        showPageEnd,
                        showDebugUtilities);
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if (window.Content is not FrameworkElement root) throw new InvalidOperationException("The main window has no renderable content.");
                    Size size = string.Equals(
                        key,
                        "Placement Setup",
                        StringComparison.OrdinalIgnoreCase)
                        ? new Size(1660, 1040)
                        : new Size(1200, 780);
                    window.Width = size.Width;
                    window.Height = size.Height;
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();
                    RenderTargetBitmap bitmap = new(
                        (int)size.Width,
                        (int)size.Height,
                        96,
                        96,
                        PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    EnsureVisiblePixels(bitmap, file, theme);
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    await using FileStream stream = new(Path.Combine(output, $"{file}-{theme.ToString().ToLowerInvariant()}.png"), FileMode.Create, FileAccess.Write, FileShare.None);
                    encoder.Save(stream);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void VerifyBundledFont(MainWindow window)
    {
        if (!window.FontFamily.FamilyNames.Values.Any(name => string.Equals(name, "Fredoka", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("The main window did not inherit the embedded Fredoka font. WPF would silently use a fallback typeface.");
        }
    }

    private static void EnsureVisiblePixels(RenderTargetBitmap bitmap, string page, AppTheme theme)
    {
        int stride = checked(bitmap.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        int visible = 0;
        for (int index = 3; index < pixels.Length; index += 4)
        {
            if (pixels[index] != 0) visible++;
        }
        if (visible < bitmap.PixelWidth * bitmap.PixelHeight / 2)
        {
            throw new InvalidOperationException($"The {page} {theme.ToString().ToLowerInvariant()} UI snapshot rendered mostly transparent.");
        }
    }
}
