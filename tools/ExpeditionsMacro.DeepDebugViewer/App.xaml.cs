using System.Windows;
using Microsoft.Win32;

namespace ExpeditionsMacro.DeepDebugViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplySystemTheme();
        MainWindow window = new();
        MainWindow = window;
        window.Show();
        if (e.Args.Length > 0) window.OpenArchiveFromCommandLine(e.Args[0]);
    }

    private static void ApplySystemTheme()
    {
        bool light;
        try
        {
            object? value = Registry.GetValue(
                "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                "AppsUseLightTheme",
                0);
            light = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 0;
        }
        catch
        {
            light = false;
        }

        ResourceDictionary resources = Current.Resources;
        resources.MergedDictionaries[1] = new ResourceDictionary
        {
            Source = new Uri($"Themes/{(light ? "Light" : "Dark")}.xaml", UriKind.Relative),
        };
    }
}
