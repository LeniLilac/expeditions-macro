using System.Windows.Controls;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

internal static class CameraPresetSelection
{
    public static void Apply(
        ComboBox combo,
        TextBlock status,
        IEnumerable<CameraModelManifest> models,
        string? selectedId)
    {
        ArgumentNullException.ThrowIfNull(combo);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(models);

        CameraModelManifest? selected = models.FirstOrDefault(
            model => model.Id == selectedId);
        combo.SelectedItem = selected;
        if (selected is not null ||
            string.IsNullOrWhiteSpace(selectedId))
        {
            return;
        }

        status.Text =
            "This preset references an old or missing camera model. " +
            "Choose a current camera model and click Save preset.";
    }
}
