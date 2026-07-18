using System.Windows;
using ExpeditionsMacro.Core.Models;
using Microsoft.Win32;

namespace ExpeditionsMacro.App.Services;

public static class ThemeService
{
    private static AppTheme _requested = AppTheme.System;

    static ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (_requested == AppTheme.System && Application.Current is not null) Application.Current.Dispatcher.BeginInvoke(() => Apply(AppTheme.System));
        };
    }

    public static void Apply(AppTheme theme)
    {
        _requested = theme;
        bool light = theme == AppTheme.Light || (theme == AppTheme.System && SystemUsesLightTheme());
        ResourceDictionary resources = Application.Current.Resources;
        ResourceDictionary? existing = resources.MergedDictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.EndsWith("/Dark.xaml", StringComparison.OrdinalIgnoreCase) == true
            || dictionary.Source?.OriginalString.EndsWith("/Light.xaml", StringComparison.OrdinalIgnoreCase) == true
            || dictionary.Source?.OriginalString.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase) == true
            || dictionary.Source?.OriginalString.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) == true);
        ResourceDictionary replacement = new() { Source = new Uri($"Themes/{(light ? "Light" : "Dark")}.xaml", UriKind.Relative) };
        if (existing is null) resources.MergedDictionaries.Add(replacement);
        else
        {
            int index = resources.MergedDictionaries.IndexOf(existing);
            resources.MergedDictionaries[index] = replacement;
        }
    }

    private static bool SystemUsesLightTheme()
    {
        try
        {
            object? value = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", 0);
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 0;
        }
        catch
        {
            return false;
        }
    }
}
