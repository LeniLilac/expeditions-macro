using System.Windows;
using Microsoft.Win32;

namespace ExpeditionsMacro.DetectorViewer.Services;

internal static class ViewerSourcePicker
{
    public static string? PickFile(
        Window owner)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Open detector image source",
            Filter =
                "Supported sources|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.zip|Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Deep Debug archives|*.zip|All files|*.*",
            CheckFileExists = true,
        };
        return dialog.ShowDialog(owner) == true
            ? dialog.FileName
            : null;
    }

    public static string? PickFolder(
        Window owner)
    {
        OpenFolderDialog dialog = new()
        {
            Title = "Open folder of detector images",
            Multiselect = false,
        };
        return dialog.ShowDialog(owner) == true
            ? dialog.FolderName
            : null;
    }
}
