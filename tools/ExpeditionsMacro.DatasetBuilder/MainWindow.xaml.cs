using System.IO;
using System.Windows;
using ExpeditionsMacro.Vision.Packs;
using Microsoft.Win32;

namespace ExpeditionsMacro.DatasetBuilder;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        string repository = FindRepositoryRoot();
        DatasetsPath.Text = Path.Combine(repository, "datasets", "anime-expeditions", "expeditions");
        OutputPath.Text = Path.Combine(repository, "detector-packs");
    }

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        BuildButton.IsEnabled = false;
        BusyProgress.Visibility = Visibility.Visible;
        LogText.Clear();
        try
        {
            DetectorPackBuilder builder = new();
            Progress<string> progress = new(message =>
            {
                LogText.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
                LogText.ScrollToEnd();
            });
            string path = await builder.BuildAsync(DatasetsPath.Text.Trim(), OutputPath.Text.Trim(), VersionText.Text.Trim(), progress);
            LogText.AppendText($"{Environment.NewLine}Ready: {path}{Environment.NewLine}");
        }
        catch (Exception error)
        {
            LogText.AppendText($"ERROR: {error.Message}{Environment.NewLine}");
        }
        finally
        {
            BuildButton.IsEnabled = true;
            BusyProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void BrowseDatasets_Click(object sender, RoutedEventArgs e) => BrowseInto(DatasetsPath);

    private void BrowseOutput_Click(object sender, RoutedEventArgs e) => BrowseInto(OutputPath);

    private void BrowseInto(System.Windows.Controls.TextBox target)
    {
        OpenFolderDialog dialog = new() { InitialDirectory = Directory.Exists(target.Text) ? target.Text : null };
        if (dialog.ShowDialog(this) == true) target.Text = dialog.FolderName;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ExpeditionsMacro.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        return Environment.CurrentDirectory;
    }
}
