using System.Windows;

namespace ExpeditionsMacro.App.Pages;

public partial class SettingsPage
{
    private async void ManualRecordingsCheck_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        ManualRecordingsCheck.IsEnabled = false;
        try
        {
            await _services.UpdateSettingsAsync(
                settings => settings with
                {
                    ManualInputRecordingEnabled =
                        ManualRecordingsCheck
                            .IsChecked == true,
                });
        }
        catch
        {
            _loading = true;
            ManualRecordingsCheck.IsChecked =
                _services.Settings
                    .ManualInputRecordingEnabled;
            _loading = false;
            throw;
        }
        finally
        {
            UpdateCaptureState();
        }
    }
}
