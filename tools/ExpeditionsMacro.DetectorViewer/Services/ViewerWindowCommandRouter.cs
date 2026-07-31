using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ExpeditionsMacro.DetectorViewer.Services;

internal static class ViewerWindowCommandRouter
{
    public static void ApplyDragOver(
        DragEventArgs args)
    {
        args.Effects =
            args.Data.GetDataPresent(
                DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        args.Handled = true;
    }

    public static string? DroppedPath(
        DragEventArgs args) =>
        args.Data.GetData(
            DataFormats.FileDrop) is
            string[] { Length: > 0 } paths
                ? paths[0]
                : null;

    public static void HandleKey(
        KeyEventArgs args,
        Action openSource,
        Action openFolder,
        Action openRepositoryDatasets,
        Action<int> loadFrame,
        int frameIndex,
        Action fit)
    {
        if (args.Key == Key.D &&
            Keyboard.Modifiers ==
                ModifierKeys.Control)
        {
            openRepositoryDatasets();
            args.Handled = true;
            return;
        }
        if (args.Key == Key.O &&
            Keyboard.Modifiers.HasFlag(
                ModifierKeys.Control))
        {
            if (Keyboard.Modifiers.HasFlag(
                    ModifierKeys.Shift))
            {
                openFolder();
            }
            else
            {
                openSource();
            }
            args.Handled = true;
            return;
        }
        if (Keyboard.FocusedElement is
            TextBoxBase)
        {
            return;
        }
        if (args.Key == Key.Left)
        {
            loadFrame(frameIndex - 1);
            args.Handled = true;
        }
        else if (args.Key == Key.Right)
        {
            loadFrame(frameIndex + 1);
            args.Handled = true;
        }
        else if (args.Key == Key.F)
        {
            fit();
            args.Handled = true;
        }
    }
}
