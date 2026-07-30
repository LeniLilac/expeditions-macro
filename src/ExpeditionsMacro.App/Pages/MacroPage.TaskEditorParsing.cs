using System.Globalization;
using System.IO;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private static int ParseWholeNumber(
        TextBox field,
        string label,
        int minimum,
        int maximum) =>
        int.TryParse(
            field.Text.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value) &&
        value >= minimum &&
        value <= maximum
            ? value
            : throw new InvalidDataException(
                $"{label} must be {minimum} through {maximum}.");
}
