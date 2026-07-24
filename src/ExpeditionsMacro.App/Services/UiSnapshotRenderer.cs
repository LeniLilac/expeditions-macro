using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Services;

internal static class UiSnapshotRenderer
{
    private static readonly (string Key, string File, bool ShowPageEnd)[] Pages =
    [
        ("Macro", "macro", false),
        ("Macro", "macro-status", true),
        ("Expeditions", "expeditions", false),
        ("Challenges", "challenges", false),
        ("Challenges", "challenges-status", true),
        ("Story", "story", false),
        ("Raid", "raid", false),
        ("Camera Models", "camera-models", false),
        ("Placement Models", "placement-models", false),
        ("Debug", "debug", false),
        ("Settings", "settings", false),
        ("Settings", "settings-debug", true),
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
                foreach ((string key, string file, bool showPageEnd) in Pages)
                {
                    await window.SelectPageForSnapshotAsync(key, showPageEnd);
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if (window.Content is not FrameworkElement root) throw new InvalidOperationException("The main window has no renderable content.");
                    Size size = new(1200, 780);
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();
                    RenderTargetBitmap bitmap = new(1200, 780, 96, 96, PixelFormats.Pbgra32);
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
