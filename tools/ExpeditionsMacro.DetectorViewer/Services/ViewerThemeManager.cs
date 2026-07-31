using System.Windows;
using Microsoft.Win32;

namespace ExpeditionsMacro.DetectorViewer.Services;

public enum ViewerTheme
{
    Dark,
    Light,
}

public static class ViewerThemeManager
{
    public static ViewerTheme SystemTheme()
    {
        try
        {
            object? value = Registry.GetValue(
                "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                "AppsUseLightTheme",
                0);
            return Convert.ToInt32(
                       value,
                       System.Globalization
                           .CultureInfo.InvariantCulture) != 0
                ? ViewerTheme.Light
                : ViewerTheme.Dark;
        }
        catch
        {
            return ViewerTheme.Dark;
        }
    }

    public static void Apply(ViewerTheme theme)
    {
        ResourceDictionary resources =
            Application.Current.Resources;
        if (resources.MergedDictionaries.Count < 2)
        {
            throw new InvalidOperationException(
                "The viewer theme resources are incomplete.");
        }
        resources.MergedDictionaries[1] =
            new ResourceDictionary
            {
                Source = new Uri(
                    $"Themes/{theme}.xaml",
                    UriKind.Relative),
            };
    }
}
