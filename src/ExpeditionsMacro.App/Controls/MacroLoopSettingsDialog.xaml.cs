using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExpeditionsMacro.App.Controls;

public sealed class MacroLoopSettingsApplyEventArgs(
    bool forever,
    int repeatCount) : EventArgs
{
    public bool Forever { get; } = forever;

    public int RepeatCount { get; } = repeatCount;
}

public partial class MacroLoopSettingsDialog :
    UserControl
{
    public MacroLoopSettingsDialog()
    {
        InitializeComponent();
    }

    public event EventHandler<
        MacroLoopSettingsApplyEventArgs>?
        ApplyRequested;

    public event EventHandler? CancelRequested;

    public void SetValues(
        string loopLabel,
        bool forever,
        int repeatCount)
    {
        TitleText.Text = $"{loopLabel} settings";
        ForeverCheck.IsChecked = forever;
        RepeatCountText.Text = repeatCount.ToString(
            CultureInfo.CurrentCulture);
        ShowError(string.Empty);
        UpdateRepeatCountState();
    }

    public void FocusInitial()
    {
        if (ForeverCheck.IsChecked == true)
        {
            ForeverCheck.Focus();
            return;
        }
        RepeatCountText.Focus();
        RepeatCountText.SelectAll();
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility =
            string.IsNullOrWhiteSpace(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void Apply_Click(
        object sender,
        RoutedEventArgs e)
    {
        bool forever =
            ForeverCheck.IsChecked == true;
        bool validCount = int.TryParse(
                RepeatCountText.Text.Trim(),
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out int repeatCount) &&
            repeatCount is >= 1 and <= 100000;
        if (!validCount &&
            !forever)
        {
            ShowError(
                "Repeat count must be 1 through 100000.");
            RepeatCountText.Focus();
            RepeatCountText.SelectAll();
            return;
        }
        if (!validCount)
        {
            repeatCount = 2;
        }
        ApplyRequested?.Invoke(
            this,
            new MacroLoopSettingsApplyEventArgs(
                forever,
                repeatCount));
    }

    private void Cancel_Click(
        object sender,
        RoutedEventArgs e) =>
        CancelRequested?.Invoke(
            this,
            EventArgs.Empty);

    private void ForeverCheck_Changed(
        object sender,
        RoutedEventArgs e) =>
        UpdateRepeatCountState();

    private void UpdateRepeatCountState() =>
        RepeatCountText.IsEnabled =
            ForeverCheck.IsChecked != true;

    private void Dialog_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }
        CancelRequested?.Invoke(
            this,
            EventArgs.Empty);
        e.Handled = true;
    }
}
