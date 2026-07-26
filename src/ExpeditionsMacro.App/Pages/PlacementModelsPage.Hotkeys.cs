using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void PlacementModelsPage_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None ||
            Keyboard.FocusedElement is TextBoxBase or
                PasswordBox or ComboBox)
        {
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = RemoveSelectedPlacementStep();
            return;
        }

        if (FastEditorPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        int unit = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 1,
            Key.D2 or Key.NumPad2 => 2,
            Key.D3 or Key.NumPad3 => 3,
            Key.D4 or Key.NumPad4 => 4,
            Key.D5 or Key.NumPad5 => 5,
            Key.D6 or Key.NumPad6 => 6,
            _ => 0,
        };
        if (unit == 0) return;

        RadioButton button = unit switch
        {
            1 => FastUnit1Button,
            2 => FastUnit2Button,
            3 => FastUnit3Button,
            4 => FastUnit4Button,
            5 => FastUnit5Button,
            6 => FastUnit6Button,
            _ => throw new ArgumentOutOfRangeException(
                nameof(unit)),
        };
        button.IsChecked = true;
        e.Handled = true;
    }
}
