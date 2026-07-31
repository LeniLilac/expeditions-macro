using System.IO;
using System.Windows;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.DetectorViewer.Services;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer;

public partial class App : Application
{
    private async void App_Startup(
        object sender,
        StartupEventArgs e)
    {
        bool headless =
            e.Args.FirstOrDefault() is
                "--smoke" or
                "--snapshot-ui";
        string? outputDirectory =
            e.Args.Length > 1
                ? Path.GetFullPath(e.Args[1])
                : null;
        try
        {
            if (headless)
            {
                IDetectorPack pack =
                    await BundledDetectorPackLoader
                        .LoadAsync();
                DetectorInspectionCatalogResult catalog =
                    DetectorInspectionCatalog.Create(pack);
                if (e.Args[0].Equals(
                        "--smoke",
                        StringComparison.Ordinal))
                {
                    await DetectorViewerSmoke.RunAsync(
                        catalog,
                        outputDirectory);
                }
                else
                {
                    if (outputDirectory is null)
                    {
                        throw new ArgumentException(
                            "--snapshot-ui requires an output directory.");
                    }
                    await DetectorViewerSnapshotRenderer
                        .RenderAsync(
                            catalog,
                            outputDirectory);
                }
                Shutdown(0);
                return;
            }

            IDetectorPack? detectorPack = null;
            string? warning = null;
            try
            {
                detectorPack =
                    await BundledDetectorPackLoader
                        .LoadAsync();
            }
            catch (Exception error)
            {
                warning =
                    $"Bundled expedition pack unavailable: {error.Message}";
            }
            DetectorInspectionCatalogResult result =
                DetectorInspectionCatalog.Create(
                    detectorPack);
            MainWindow window =
                new(
                    result,
                    warning,
                    shutdownApplicationOnClose: true);
            MainWindow = window;
            window.Show();
            if (e.Args.FirstOrDefault() is
                string initialPath)
            {
                await window.OpenInitialPathAsync(
                    initialPath);
            }
        }
        catch (Exception error)
        {
            TryWriteFailure(
                outputDirectory,
                error);
            if (!headless)
            {
                MessageBox.Show(
                    error.Message,
                    "Detector Viewer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            Shutdown(1);
        }
    }

    private static void TryWriteFailure(
        string? directory,
        Exception error)
    {
        if (directory is null)
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(
                    directory,
                    "detector-viewer-error.txt"),
                error.ToString());
        }
        catch
        {
        }
    }
}
