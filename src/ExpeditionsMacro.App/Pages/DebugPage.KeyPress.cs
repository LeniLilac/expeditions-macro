using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class DebugPage
{
    private int _keyPressVirtualKey =
        AutomationKeyPress.DefaultVirtualKey;
    private bool _capturingKeyPressKey;

    private void KeyPressKey_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy) return;
        _capturingKeyPressKey = true;
        KeyPressKeyButton.Content = "Press a key...";
        ShowKeyPressStatus(
            "Press Escape to cancel key selection.");
        Keyboard.Focus(KeyPressKeyButton);
    }

    private void DebugPage_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (!_capturingKeyPressKey) return;
        e.Handled = true;
        Key key = e.Key == Key.System
            ? e.SystemKey
            : e.Key;
        if (key == Key.Escape)
        {
            FinishKeyCapture();
            HideKeyPressStatus();
            return;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        try
        {
            _ = AutomationKeyPress.Create(
                virtualKey,
                AutomationKeyPress.DefaultHoldMilliseconds,
                _services.Settings.MacroHotkeyVirtualKey);
        }
        catch (Exception error)
        {
            ShowKeyPressStatus(error.Message);
            return;
        }

        _keyPressVirtualKey = virtualKey;
        FinishKeyCapture();
        HideKeyPressStatus();
    }

    private void FinishKeyCapture()
    {
        _capturingKeyPressKey = false;
        KeyPressKeyButton.Content =
            KeyboardKey.GetDisplayName(
                _keyPressVirtualKey);
    }

    private void ArmKeyPress_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy) return;
        try
        {
            AutomationKeyPress keyPress =
                ReadKeyPress();
            ArmKeyPress(keyPress);
        }
        catch (Exception error)
        {
            ShowKeyPressStatus(error.Message);
        }
    }

    private AutomationKeyPress ReadKeyPress()
    {
        if (!int.TryParse(
                KeyPressDurationText.Text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int holdMilliseconds))
        {
            throw new InvalidDataException(
                "Enter the keypress time in milliseconds.");
        }
        return AutomationKeyPress.Create(
            _keyPressVirtualKey,
            holdMilliseconds,
            _services.Settings.MacroHotkeyVirtualKey);
    }

    private void ArmKeyPress(
        AutomationKeyPress keyPress)
    {
        _timeline.Clear();
        _followLive = true;
        ShowCheckpoint(index: -1);
        HideKeyPressStatus();
        DebugStepMode mode = SelectedStepMode();
        string description =
            $"Debug {keyPress.KeyName} key press";
        DeepDebugOperationContext context = new()
        {
            DebugTool = "key-press",
            DebugStepMode = mode.ToString(),
            OperationSettings = new
            {
                Key = keyPress.KeyName,
                keyPress.VirtualKey,
                keyPress.HoldMilliseconds,
            },
        };
        _services.Coordinator.Arm(
            description,
            token => RunArmedKeyPressAsync(
                description,
                keyPress,
                mode,
                token),
            context);
        UpdateControls();
    }

    private async Task RunArmedKeyPressAsync(
        string description,
        AutomationKeyPress keyPress,
        DebugStepMode mode,
        CancellationToken cancellationToken)
    {
        _services.DebugCheckpoints.Begin(
            mode,
            cancellationToken);
        try
        {
            _services.DebugCheckpoints.RecordStatus(
                "Debug operation started",
                description);
            await Task.Run(
                () => ExecuteKeyPressAsync(
                    keyPress,
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
            _services.DebugCheckpoints.RecordStatus(
                "Debug operation completed",
                $"{keyPress.KeyName} was held for " +
                $"{keyPress.HoldMilliseconds} ms.");
            SetKeyPressResult(
                $"{keyPress.KeyName} held for " +
                $"{keyPress.HoldMilliseconds} ms.");
        }
        catch (OperationCanceledException)
        {
            SetKeyPressResult(
                $"{keyPress.KeyName} was released.");
            throw;
        }
        catch (Exception error)
        {
            SetKeyPressResult(error.Message);
            throw;
        }
        finally
        {
            _services.DebugCheckpoints.Complete();
        }
    }

    private async Task ExecuteKeyPressAsync(
        AutomationKeyPress keyPress,
        CancellationToken cancellationToken)
    {
        RobloxWindow window = RequireRobloxWindow();
        RequireFocus(window);
        await _services.Automation.HoldKeyAsync(
            window,
            keyPress.VirtualKey,
            keyPress.HoldMilliseconds,
            cancellationToken).ConfigureAwait(false);
    }

    private void SetKeyPressResult(string message) =>
        Dispatcher.BeginInvoke(() =>
            ShowKeyPressStatus(message));

    private void ShowKeyPressStatus(string message)
    {
        KeyPressStatusText.Text = message;
        KeyPressStatusText.Visibility =
            Visibility.Visible;
    }

    private void HideKeyPressStatus()
    {
        KeyPressStatusText.Text = string.Empty;
        KeyPressStatusText.Visibility =
            Visibility.Collapsed;
    }
}
