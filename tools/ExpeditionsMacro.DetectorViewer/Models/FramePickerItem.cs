using System.IO;

namespace ExpeditionsMacro.DetectorViewer.Models;

public sealed record FramePickerItem(
    int Index,
    string DisplayPath)
{
    public string Label
    {
        get
        {
            string fileName = Path.GetFileName(
                DisplayPath);
            string? folder = Path.GetDirectoryName(
                DisplayPath);
            return string.IsNullOrWhiteSpace(folder)
                ? fileName
                : $"{fileName}  |  {folder}";
        }
    }
}
