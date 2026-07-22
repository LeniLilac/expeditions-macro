using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class SettingsKeyBindingsPanel : UserControl
{
    private AppServices? _services;
    private BindingTarget _captureTarget;
    private bool _saving;
    private bool _busy;

    public SettingsKeyBindingsPanel() => InitializeComponent();

    public event EventHandler? BindingsChanged;

    public string MacroDiagnostic { get; private set; } = "Unavailable";

    public string PlayDiagnostic { get; private set; } = "Not set";

    public string UnitDiagnostic { get; private set; } = "Not set";

    public string ShiftLockDiagnostic { get; private set; } = "Left Ctrl";

    public string HotkeyDisplayName => _services?.Hotkey.DisplayName ?? "F6";

    public void Initialize(AppServices services)
    {
        if (_services is not null) return;
        _services = services;
        services.Hotkey.BindingChanged += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            Refresh();
            BindingsChanged?.Invoke(this, EventArgs.Empty);
        });
        services.Hotkey.Pressed += (_, _) => Dispatcher.BeginInvoke(CancelForActiveMacroHotkey);
        Unloaded += (_, _) => CancelCapture("Key change canceled.");
        Refresh();
    }

    public void Refresh()
    {
        if (_services is null) return;
        UpdateMacroDisplay();
        UpdatePlayDisplay();
        UpdateUnitDisplay();
        UpdateShiftLockDisplay();
        UpdateButtons();
    }

    public void UpdateBusyState(bool busy)
    {
        _busy = busy;
        if (busy && _captureTarget != BindingTarget.None)
        {
            CancelCapture("Stop the current operation before changing control keys.");
        }
        UpdateButtons();
    }

    private void MacroButton_Click(object sender, RoutedEventArgs e) => BeginCapture(BindingTarget.Macro, MacroButton);

    private void PlayButton_Click(object sender, RoutedEventArgs e) => BeginCapture(BindingTarget.Play, PlayButton);

    private void UnitButton_Click(object sender, RoutedEventArgs e) => BeginCapture(BindingTarget.Unit, UnitButton);

    private void ShiftLockButton_Click(object sender, RoutedEventArgs e) => BeginCapture(BindingTarget.ShiftLock, ShiftLockButton);

    private void BeginCapture(BindingTarget target, Button button)
    {
        if (_busy || _saving) return;
        _captureTarget = target;
        button.Content = target is BindingTarget.Play or BindingTarget.Unit ? "Press a letter..." : "Press a key...";
        StatusFor(target).Text = target switch
        {
            BindingTarget.Macro => "Press a letter, number, punctuation, numpad, or function key. Escape cancels; F12 is reserved.",
            BindingTarget.Play => "Press the letter assigned to Toggle Play Menu. Escape cancels.",
            BindingTarget.Unit => "Press the letter assigned to Toggle Units. Escape cancels.",
            _ => "Press the key assigned to Shift Lock, including left/right Shift or Ctrl. Escape cancels.",
        };
        Keyboard.Focus(button);
        UpdateButtons();
    }

    private async void Panel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_captureTarget == BindingTarget.None) return;
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            CancelCapture("Key change canceled.");
            return;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        BindingTarget target = _captureTarget;
        if (target is BindingTarget.Play or BindingTarget.Unit)
        {
            if (virtualKey is < 0x41 or > 0x5A)
            {
                StatusFor(target).Text = "This binding must use one letter from A through Z. Press a letter, or Escape to cancel.";
                return;
            }
        }
        else if (target == BindingTarget.Macro && !KeyboardKey.IsSupportedMacroHotkey(virtualKey))
        {
            MacroStatusText.Text = "That macro hotkey is not supported. Choose a letter, number, punctuation, numpad, or supported function key.";
            return;
        }
        else if (target == BindingTarget.ShiftLock && !KeyboardKey.IsSupportedShiftLockKey(virtualKey))
        {
            ShiftLockStatusText.Text = "Choose Left/Right Shift, Left/Right Ctrl, or a supported letter, number, symbol, numpad, function, or common control key.";
            return;
        }

        _captureTarget = BindingTarget.None;
        await ApplyAsync(target, virtualKey);
    }

    private async Task ApplyAsync(BindingTarget target, int virtualKey)
    {
        _saving = true;
        UpdateButtons();
        try
        {
            switch (target)
            {
                case BindingTarget.Macro:
                    await ApplyMacroAsync(virtualKey);
                    break;
                case BindingTarget.Play:
                    await ApplyPlayAsync((char)virtualKey);
                    break;
                case BindingTarget.Unit:
                    await ApplyUnitAsync((char)virtualKey);
                    break;
                case BindingTarget.ShiftLock:
                    await ApplyShiftLockAsync(virtualKey);
                    break;
            }
            Refresh();
            BindingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception error)
        {
            Refresh();
            StatusFor(target).Text = $"Could not save the key: {error.Message}";
        }
        finally
        {
            _saving = false;
            UpdateButtons();
        }
    }

    private async Task ApplyMacroAsync(int virtualKey)
    {
        AppServices services = Services;
        ValidateBindings(virtualKey, services.Settings.PlayMenuKey, services.Settings.UnitMenuKey, services.Settings.ShiftLockVirtualKey);
        int previous = services.Hotkey.VirtualKey;
        string display = KeyboardKey.GetDisplayName(virtualKey);
        MacroButton.Content = display;
        MacroStatusText.Text = $"Registering {display} globally...";
        try
        {
            await Task.Run(() => services.Hotkey.Rebind(virtualKey));
            await services.UpdateSettingsAsync(settings => settings with { MacroHotkeyVirtualKey = virtualKey });
            MacroStatusText.Text = $"{display} is now the macro start and stop key.";
        }
        catch
        {
            if (services.Hotkey.VirtualKey != previous)
            {
                try { await Task.Run(() => services.Hotkey.Rebind(previous)); } catch { }
            }
            throw;
        }
    }

    private async Task ApplyPlayAsync(char key)
    {
        AppServices services = Services;
        _ = AppSettings.ParsePlayMenuKey(key.ToString(), services.Hotkey.VirtualKey);
        if (!string.IsNullOrWhiteSpace(services.Settings.UnitMenuKey))
        {
            _ = AppSettings.ParseUnitMenuKey(services.Settings.UnitMenuKey, services.Hotkey.VirtualKey, key.ToString());
        }
        _ = AppSettings.ParseShiftLockKey(services.Settings.ShiftLockVirtualKey, services.Hotkey.VirtualKey, key.ToString(), services.Settings.UnitMenuKey);
        await services.UpdateSettingsAsync(settings => settings with { PlayMenuKey = key.ToString() });
    }

    private async Task ApplyUnitAsync(char key)
    {
        AppServices services = Services;
        _ = AppSettings.ParseUnitMenuKey(key.ToString(), services.Hotkey.VirtualKey, services.Settings.PlayMenuKey);
        _ = AppSettings.ParseShiftLockKey(services.Settings.ShiftLockVirtualKey, services.Hotkey.VirtualKey, services.Settings.PlayMenuKey, key.ToString());
        await services.UpdateSettingsAsync(settings => settings with { UnitMenuKey = key.ToString() });
    }

    private async Task ApplyShiftLockAsync(int virtualKey)
    {
        AppServices services = Services;
        _ = AppSettings.ParseShiftLockKey(virtualKey, services.Hotkey.VirtualKey, services.Settings.PlayMenuKey, services.Settings.UnitMenuKey);
        await services.UpdateSettingsAsync(settings => settings with { ShiftLockVirtualKey = virtualKey });
    }

    private void ValidateBindings(int macroKey, string playKey, string unitKey, int shiftLockKey)
    {
        if (!string.IsNullOrWhiteSpace(playKey)) _ = AppSettings.ParsePlayMenuKey(playKey, macroKey);
        if (!string.IsNullOrWhiteSpace(unitKey)) _ = AppSettings.ParseUnitMenuKey(unitKey, macroKey, playKey);
        _ = AppSettings.ParseShiftLockKey(shiftLockKey, macroKey, playKey, unitKey);
    }

    private void UpdateMacroDisplay()
    {
        AppServices services = Services;
        string hotkey = services.Hotkey.DisplayName;
        if (_captureTarget != BindingTarget.Macro) MacroButton.Content = hotkey;
        MacroDiagnostic = services.Hotkey.IsRegistered ? $"{hotkey} registered" : "Unavailable";
        if (_captureTarget != BindingTarget.Macro && !_saving) MacroStatusText.Text = $"{hotkey} is registered globally for every macro workflow.";
    }

    private void UpdatePlayDisplay()
    {
        AppServices services = Services;
        try
        {
            char key = AppSettings.ParsePlayMenuKey(services.Settings.PlayMenuKey, services.Hotkey.VirtualKey);
            if (_captureTarget != BindingTarget.Play) PlayButton.Content = key.ToString();
            PlayStatusText.Text = $"{key} must match Anime Expeditions' Toggle Play Menu binding.";
            PlayDiagnostic = key.ToString();
        }
        catch (InvalidDataException error)
        {
            bool empty = string.IsNullOrWhiteSpace(services.Settings.PlayMenuKey);
            if (_captureTarget != BindingTarget.Play) PlayButton.Content = empty ? "Set key" : services.Settings.PlayMenuKey;
            PlayStatusText.Text = empty ? "Required before a macro can start." : error.Message;
            PlayDiagnostic = empty ? "Not set" : "Conflict";
        }
    }

    private void UpdateUnitDisplay()
    {
        AppServices services = Services;
        try
        {
            char key = AppSettings.ParseUnitMenuKey(services.Settings.UnitMenuKey, services.Hotkey.VirtualKey, services.Settings.PlayMenuKey);
            if (_captureTarget != BindingTarget.Unit) UnitButton.Content = key.ToString();
            UnitStatusText.Text = $"{key} must match Anime Expeditions' Toggle Units binding.";
            UnitDiagnostic = key.ToString();
        }
        catch (InvalidDataException error)
        {
            bool empty = string.IsNullOrWhiteSpace(services.Settings.UnitMenuKey);
            if (_captureTarget != BindingTarget.Unit) UnitButton.Content = empty ? "Set key" : services.Settings.UnitMenuKey;
            UnitStatusText.Text = empty ? "Required only when a preset changes the active team." : error.Message;
            UnitDiagnostic = empty ? "Not set" : "Conflict";
        }
    }

    private void UpdateShiftLockDisplay()
    {
        AppServices services = Services;
        string display = KeyboardKey.GetDisplayName(services.Settings.ShiftLockVirtualKey);
        try
        {
            _ = AppSettings.ParseShiftLockKey(services.Settings.ShiftLockVirtualKey, services.Hotkey.VirtualKey, services.Settings.PlayMenuKey, services.Settings.UnitMenuKey);
            if (_captureTarget != BindingTarget.ShiftLock) ShiftLockButton.Content = display;
            ShiftLockStatusText.Text = $"{display} must match Anime Expeditions' Shift Lock binding.";
            ShiftLockDiagnostic = display;
        }
        catch (InvalidDataException error)
        {
            if (_captureTarget != BindingTarget.ShiftLock) ShiftLockButton.Content = display;
            ShiftLockStatusText.Text = error.Message;
            ShiftLockDiagnostic = "Conflict";
        }
    }

    private void CancelForActiveMacroHotkey()
    {
        if (_captureTarget == BindingTarget.None) return;
        string message = _captureTarget == BindingTarget.Macro
            ? $"{Services.Hotkey.DisplayName} is already the macro hotkey."
            : $"{Services.Hotkey.DisplayName} is already the macro hotkey. Choose a different binding.";
        CancelCapture(message);
    }

    private void CancelCapture(string status)
    {
        if (_captureTarget == BindingTarget.None) return;
        BindingTarget target = _captureTarget;
        _captureTarget = BindingTarget.None;
        Refresh();
        StatusFor(target).Text = status;
    }

    private void UpdateButtons()
    {
        bool enabled = !_busy && !_saving;
        MacroButton.IsEnabled = enabled && _captureTarget is BindingTarget.None or BindingTarget.Macro;
        PlayButton.IsEnabled = enabled && _captureTarget is BindingTarget.None or BindingTarget.Play;
        UnitButton.IsEnabled = enabled && _captureTarget is BindingTarget.None or BindingTarget.Unit;
        ShiftLockButton.IsEnabled = enabled && _captureTarget is BindingTarget.None or BindingTarget.ShiftLock;
    }

    private TextBlock StatusFor(BindingTarget target) => target switch
    {
        BindingTarget.Macro => MacroStatusText,
        BindingTarget.Play => PlayStatusText,
        BindingTarget.Unit => UnitStatusText,
        BindingTarget.ShiftLock => ShiftLockStatusText,
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private AppServices Services => _services ?? throw new InvalidOperationException("The key bindings panel has not been initialized.");

    private enum BindingTarget
    {
        None,
        Macro,
        Play,
        Unit,
        ShiftLock,
    }
}
