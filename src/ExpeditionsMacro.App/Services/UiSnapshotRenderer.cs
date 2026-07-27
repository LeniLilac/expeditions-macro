using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExpeditionsMacro.App.Pages;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Services;

internal static class UiSnapshotRenderer
{
    private static readonly (
        string Key,
        string File,
        bool ShowPageEnd,
        bool ShowDebugUtilities,
        MacroPlanSnapshotState MacroPlanState)[] Pages =
    [
        ("Dashboard", "dashboard", false, false, MacroPlanSnapshotState.Empty),
        ("Dashboard", "dashboard-controls", true, false, MacroPlanSnapshotState.Empty),
        ("Macro Plan", "macro-plan-empty", false, false, MacroPlanSnapshotState.Empty),
        ("Macro Plan", "macro-plan-tasks-only", false, false, MacroPlanSnapshotState.TasksOnly),
        ("Macro Plan", "macro-plan", false, false, MacroPlanSnapshotState.NestedLoops),
        ("Macro Plan", "macro-plan-share", true, false, MacroPlanSnapshotState.NestedLoops),
        ("Expeditions", "expeditions", false, false, MacroPlanSnapshotState.Empty),
        ("Challenges", "challenges", false, false, MacroPlanSnapshotState.Empty),
        ("Challenges", "challenges-status", true, false, MacroPlanSnapshotState.Empty),
        ("Story", "story", false, false, MacroPlanSnapshotState.Empty),
        ("Raid", "raid", false, false, MacroPlanSnapshotState.Empty),
        ("Camera Models", "camera-models", false, false, MacroPlanSnapshotState.Empty),
        ("Placement Setup", "placement-setup", false, false, MacroPlanSnapshotState.Empty),
        ("Debug", "debug", false, false, MacroPlanSnapshotState.Empty),
        ("Debug", "debug-refuel", true, false, MacroPlanSnapshotState.Empty),
        ("Debug", "debug-utilities", false, true, MacroPlanSnapshotState.Empty),
        ("Settings", "settings", false, false, MacroPlanSnapshotState.Empty),
        ("Settings", "settings-debug", true, false, MacroPlanSnapshotState.Empty),
    ];

    public static async Task RenderAsync(AppServices services, string outputDirectory)
    {
        string output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        string progressPath =
            Path.Combine(output, "snapshot-progress.txt");
        await File.WriteAllTextAsync(
            progressPath,
            "Creating snapshot window.");
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
            await File.WriteAllTextAsync(
                progressPath,
                "Refreshing model-backed pages.");
            await window.VerifyBackgroundModelRefreshAsync();
            int rendered = 0;
            foreach (AppTheme theme in new[] { AppTheme.Dark, AppTheme.Light })
            {
                ThemeService.Apply(theme);
                foreach ((
                    string key,
                    string file,
                    bool showPageEnd,
                    bool showDebugUtilities,
                    MacroPlanSnapshotState macroPlanState) in Pages)
                {
                    await window.SelectPageForSnapshotAsync(
                        key,
                        showPageEnd,
                        showDebugUtilities,
                        macroPlanState);
                    Size size = SnapshotSize(key);
                    await File.WriteAllTextAsync(
                        progressPath,
                        $"Rendering {file} ({theme.ToString().ToLowerInvariant()}) at {size.Width:0}x{size.Height:0}.");
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if (window.Content is not FrameworkElement root) throw new InvalidOperationException("The main window has no renderable content.");
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
                    rendered++;
                }
            }
            await File.WriteAllTextAsync(
                progressPath,
                $"Completed {rendered} snapshots.");
        }
        finally
        {
            window.Close();
        }
    }

    private static Size SnapshotSize(string key)
    {
        Size standard = new(1200, 780);
        if (string.Equals(
                key,
                "Macro Plan",
                StringComparison.OrdinalIgnoreCase))
        {
            return new Size(1440, 1080);
        }
        if (!string.Equals(
                key,
                "Placement Setup",
                StringComparison.OrdinalIgnoreCase))
        {
            return standard;
        }

        Size wide = new(1660, 1040);
        if (string.Equals(
                Environment.GetEnvironmentVariable("CI"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return standard;
        }
        Rect workArea = SystemParameters.WorkArea;
        return workArea.Width >= wide.Width &&
               workArea.Height >= wide.Height
            ? wide
            : standard;
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
