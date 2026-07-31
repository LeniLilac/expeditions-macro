using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Services;

internal static class DetectorViewerSnapshotRenderer
{
    private sealed record SnapshotSpec(
        ViewerTheme Theme,
        SnapshotScenario Scenario,
        bool MinimumSize = false)
    {
        public string Name =>
            $"detector-viewer-{Theme.ToString().ToLowerInvariant()}-{(MinimumSize ? "min-size" : Scenario.ToString().ToLowerInvariant())}.png";
    }

    public static async Task RenderAsync(
        DetectorInspectionCatalogResult catalog,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        SnapshotSpec[] specs =
        [
            new(ViewerTheme.Dark, SnapshotScenario.Matched),
            new(ViewerTheme.Light, SnapshotScenario.Matched),
            new(ViewerTheme.Dark, SnapshotScenario.Annotation),
            new(ViewerTheme.Light, SnapshotScenario.Annotation),
            new(ViewerTheme.Dark, SnapshotScenario.Negative),
            new(ViewerTheme.Light, SnapshotScenario.Negative),
            new(ViewerTheme.Dark, SnapshotScenario.Error),
            new(ViewerTheme.Light, SnapshotScenario.Error),
            new(ViewerTheme.Dark, SnapshotScenario.Negative, true),
            new(ViewerTheme.Light, SnapshotScenario.Negative, true),
        ];
        foreach (SnapshotSpec spec in specs)
        {
            MainWindow window =
                CreateWindow(
                    catalog,
                    spec.MinimumSize);
            try
            {
                window.Show();
                window.SetTheme(spec.Theme);
                await window.Dispatcher.InvokeAsync(
                    () =>
                    {
                    },
                    DispatcherPriority.Loaded);
                await window.PrepareSnapshotAsync(
                    SnapshotFixture.Create(),
                    "Snapshot fixture",
                    spec.Scenario);
                await window.Dispatcher.InvokeAsync(
                    () =>
                    {
                    },
                    DispatcherPriority.Render);
                SaveWindow(
                    window,
                    Path.Combine(
                        outputDirectory,
                        spec.Name));
            }
            finally
            {
                window.Close();
            }
        }
        await File.WriteAllTextAsync(
            Path.Combine(
                outputDirectory,
                "detector-viewer-snapshots.txt"),
            $"PASS: {specs.Length} dark/light matched, annotation, negative, error, and minimum-size snapshots rendered from the packaged executable.");
    }

    private static MainWindow CreateWindow(
        DetectorInspectionCatalogResult catalog,
        bool minimumSize) =>
        new(catalog)
        {
            Width = minimumSize
                ? 1040
                : 1440,
            Height = minimumSize
                ? 680
                : 900,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation =
                WindowStartupLocation.Manual,
            Left = 24,
            Top = 24,
        };

    private static void SaveWindow(
        Window window,
        string path)
    {
        window.UpdateLayout();
        int width =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    window.ActualWidth));
        int height =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    window.ActualHeight));
        RenderTargetBitmap target =
            new(
                width,
                height,
                96,
                96,
                PixelFormats.Pbgra32);
        target.Render(window);
        ValidatePixels(target);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(
            BitmapFrame.Create(target));
        using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        encoder.Save(stream);
    }

    private static void ValidatePixels(
        BitmapSource image)
    {
        int stride =
            checked(image.PixelWidth * 4);
        byte[] pixels =
            new byte[checked(
                stride * image.PixelHeight)];
        image.CopyPixels(
            pixels,
            stride,
            0);
        int opaquePixels = 0;
        for (int index = 3;
             index < pixels.Length;
             index += 4)
        {
            if (pixels[index] > 0)
            {
                opaquePixels++;
            }
        }
        if (opaquePixels <
            image.PixelWidth *
            image.PixelHeight /
            2)
        {
            throw new InvalidDataException(
                "The rendered UI snapshot is unexpectedly transparent.");
        }
    }
}
