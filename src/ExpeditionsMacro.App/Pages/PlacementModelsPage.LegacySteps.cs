using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void AddRow_Click(
        object sender,
        RoutedEventArgs e)
    {
        int delay = ReadPlaybackDelay();
        _steps.Add(new PlacementStepRow
        {
            UnitKey = 1,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = delay,
        });
        ActiveStepsSelector.SelectedIndex = _steps.Count - 1;
    }

    private void RemoveRow_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ActiveStepsSelector.SelectedItem is
            PlacementStepRow row)
        {
            _steps.Remove(row);
        }
    }

    private void MoveUp_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveSelected(-1);

    private void MoveDown_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveSelected(1);

    private void MoveSelected(int offset)
    {
        if (ActiveStepsSelector.SelectedItem is not
            PlacementStepRow row)
        {
            return;
        }
        int current = _steps.IndexOf(row);
        int target = current + offset;
        if (target < 0 || target >= _steps.Count) return;
        _steps.Move(current, target);
        ActiveStepsSelector.SelectedItem = row;
    }

    private int ReadPlaybackDelay()
    {
        TextBox field = FastWorkflow
            ? FastDefaultDelayText
            : DefaultDelayText;
        return TryReadDelay(field, out int delay)
            ? delay
            : 900;
    }

    private static bool TryReadDelay(
        TextBox field,
        out int delay) =>
        int.TryParse(
            field.Text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out delay) &&
        delay >= 0;
}
