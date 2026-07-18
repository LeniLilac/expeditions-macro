using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Services;

internal static class UiSnapshotRenderer
{
    private static readonly (string Key, string File)[] Pages =
    [
        ("Expeditions", "expeditions"),
        ("Camera Models", "camera-models"),
        ("Placement Models", "placement-models"),
        ("Settings", "settings"),
    ];

    public static async Task RenderAsync(AppServices services, string outputDirectory)
    {
        string output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        MainWindow window = new(services)
        {
            Width = 1200,
            Height = 780,
            ShowInTaskbar = false,
            ShowActivated = false,
        };

        foreach (AppTheme theme in new[] { AppTheme.Dark, AppTheme.Light })
        {
            ThemeService.Apply(theme);
            foreach ((string key, string file) in Pages)
            {
                await window.SelectPageForSnapshotAsync(key);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (window.Content is not FrameworkElement root) throw new InvalidOperationException("The main window has no renderable content.");
                Size size = new(1200, 780);
                root.Measure(size);
                root.Arrange(new Rect(size));
                root.UpdateLayout();
                RenderTargetBitmap bitmap = new(1200, 780, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                await using FileStream stream = new(Path.Combine(output, $"{file}-{theme.ToString().ToLowerInvariant()}.png"), FileMode.Create, FileAccess.Write, FileShare.None);
                encoder.Save(stream);
            }
        }
    }
}
