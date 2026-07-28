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

    public string AreasDiagnostic { get; private set; } = "Not set";

    public string CancelPlacementDiagnostic { get; private set; } = "Z";

    public string QuickPlacementDiagnostic { get; private set; } = "Not set";

    public string TargetingDiagnostic { get; private set; } = "Not set";

    public string UpgradeDiagnostic { get; private set; } = "Not set";

    public string AutoUpgradeDiagnostic { get; private set; } = "Not set";

    public string ToggleAutoUpgradePlacedUnitsDiagnostic
    {
        get;
        private set;
    } = "Not set";

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
        UpdateAreasDisplay();
        UpdateCancelPlacementDisplay();
        UpdateQuickPlacementDisplay();
        UpdateTargetingDisplay();
        UpdateUpgradeDisplay();
        UpdateAutoUpgradeDisplay();
        UpdateToggleAutoUpgradePlacedUnitsDisplay();
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

    private void AreasButton_Click(object sender, RoutedEventArgs e) => BeginCapture(BindingTarget.Areas, AreasButton);

    private void CancelPlacementButton_Click(
        object sender,
        RoutedEventArgs e) =>
        BeginCapture(
            BindingTarget.CancelPlacement,
            CancelPlacementButton);

    private void QuickPlacementButton_Click(
        object sender,
        RoutedEventArgs e) =>
        BeginCapture(
            BindingTarget.QuickPlacement,
            QuickPlacementButton);

    private void TargetingButton_Click(
        object sender,
        RoutedEventArgs e) =>
        BeginCapture(
            BindingTarget.Targeting,
            TargetingButton);

    private void UpgradeButton_Click(
        object sender,
        RoutedEventArgs e) =>
        BeginCapture(
            BindingTarget.Upgrade,
            UpgradeButton);

    private void AutoUpgradeUnitButton_Click(
        object sender,
        RoutedEventArgs e) =>
        BeginCapture(
            BindingTarget.AutoUpgradeUnit,
            AutoUpgradeUnitButton);

    private void ToggleAutoUpgradePlacedUnitsButton_Click(
        object sender,
        RoutedEventArgs e) =>
        BeginCapture(
            BindingTarget.ToggleAutoUpgradePlacedUnits,
            ToggleAutoUpgradePlacedUnitsButton);

    private void ShiftLockButton_Click(object sender, RoutedEventArgs e) => BeginCapture(BindingTarget.ShiftLock, ShiftLockButton);

    private async void ClearBindingButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy ||
            _saving ||
            _captureTarget != BindingTarget.None ||
            sender is not Button { Tag: string targetName } ||
            !Enum.TryParse(
                targetName,
                ignoreCase: false,
                out BindingTarget target) ||
            target is BindingTarget.None or BindingTarget.Macro)
        {
            return;
        }

        _saving = true;
        UpdateButtons();
        try
        {
            await ClearBindingAsync(target);
            Refresh();
            StatusFor(target).Text =
                $"{BindingLabel(target)} key is now unset.";
            BindingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception error)
        {
            Refresh();
            StatusFor(target).Text =
                $"Could not unset {BindingLabel(target)} key: {error.Message}";
        }
        finally
        {
            _saving = false;
            UpdateButtons();
        }
    }

    private void BeginCapture(BindingTarget target, Button button)
    {
        if (_busy || _saving) return;
        _captureTarget = target;
        button.Content = target is
            BindingTarget.Play or
            BindingTarget.Unit or
            BindingTarget.Areas or
            BindingTarget.CancelPlacement or
            BindingTarget.Targeting or
            BindingTarget.Upgrade or
            BindingTarget.AutoUpgradeUnit or
            BindingTarget.ToggleAutoUpgradePlacedUnits
                ? "Press a letter..."
                : "Press a key...";
        StatusFor(target).Text = target switch
        {
            BindingTarget.Macro => "Press a letter, number, punctuation, numpad, or function key. Escape cancels; F12 is reserved.",
            BindingTarget.Play => "Press the letter assigned to Toggle Play Menu. Escape cancels.",
            BindingTarget.Unit => "Press the letter assigned to Toggle Unit Inventory. Escape cancels.",
            BindingTarget.Areas => "Press the letter assigned to Toggle Areas Menu. Escape cancels.",
            BindingTarget.CancelPlacement => "Press the letter assigned to Toggle Cancel Unit Placement. Escape cancels.",
            BindingTarget.QuickPlacement => "Press the physical key assigned to Quick Placement. Escape cancels.",
            BindingTarget.Targeting => "Press the letter assigned to Change Unit Targeting. Escape cancels.",
            BindingTarget.Upgrade => "Press the letter assigned to Upgrade Unit. Escape cancels.",
            BindingTarget.AutoUpgradeUnit => "Press the letter assigned to Auto Upgrade Unit. Escape cancels.",
            BindingTarget.ToggleAutoUpgradePlacedUnits => "Press the letter assigned to Toggle Auto Upgrade Placed Units. Escape cancels.",
            _ => "Press the key assigned to Toggle Shift Lock, including left/right Shift or Ctrl. Escape cancels.",
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
        if (target is
            BindingTarget.Play or
            BindingTarget.Unit or
            BindingTarget.Areas or
            BindingTarget.CancelPlacement or
            BindingTarget.Targeting or
            BindingTarget.Upgrade or
            BindingTarget.AutoUpgradeUnit or
            BindingTarget.ToggleAutoUpgradePlacedUnits)
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
        else if (target == BindingTarget.ShiftLock &&
            !KeyboardKey.IsSupportedShiftLockKey(
                virtualKey))
        {
            ShiftLockStatusText.Text =
                "Choose Left/Right Shift, Left/Right Ctrl, or a supported letter, number, symbol, numpad, function, or common control key.";
            return;
        }
        else if (target ==
                BindingTarget.QuickPlacement &&
            !KeyboardKey.IsSupportedQuickPlacementKey(
                virtualKey))
        {
            QuickPlacementStatusText.Text =
                "Choose Left/Right Shift, Left/Right Ctrl, a letter, symbol, numpad key, function key, or common control key. Number-row keys 0-9 are reserved for unit slots.";
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
                case BindingTarget.Areas:
                    await ApplyAreasAsync((char)virtualKey);
                    break;
                case BindingTarget.CancelPlacement:
                    await ApplyCancelPlacementAsync(
                        (char)virtualKey);
                    break;
                case BindingTarget.QuickPlacement:
                    await ApplyQuickPlacementAsync(
                        virtualKey);
                    break;
                case BindingTarget.Targeting:
                    await ApplyTargetingAsync(
                        (char)virtualKey);
                    break;
                case BindingTarget.Upgrade:
                    await ApplyUpgradeAsync(
                        (char)virtualKey);
                    break;
                case BindingTarget.AutoUpgradeUnit:
                    await ApplyAutoUpgradeUnitAsync(
                        (char)virtualKey);
                    break;
                case BindingTarget
                    .ToggleAutoUpgradePlacedUnits:
                    await ApplyToggleAutoUpgradePlacedUnitsAsync(
                        (char)virtualKey);
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
        AreasButton.IsEnabled = enabled &&
            _captureTarget is BindingTarget.None or BindingTarget.Areas;
        CancelPlacementButton.IsEnabled = enabled &&
            _captureTarget is BindingTarget.None or
                BindingTarget.CancelPlacement;
        QuickPlacementButton.IsEnabled = enabled &&
            _captureTarget is BindingTarget.None or
                BindingTarget.QuickPlacement;
        TargetingButton.IsEnabled = enabled &&
            _captureTarget is BindingTarget.None or
                BindingTarget.Targeting;
        UpgradeButton.IsEnabled = enabled &&
            _captureTarget is BindingTarget.None or
                BindingTarget.Upgrade;
        AutoUpgradeUnitButton.IsEnabled = enabled &&
            _captureTarget is BindingTarget.None or
                BindingTarget.AutoUpgradeUnit;
        ToggleAutoUpgradePlacedUnitsButton.IsEnabled =
            enabled &&
            _captureTarget is BindingTarget.None or
                BindingTarget.ToggleAutoUpgradePlacedUnits;
        ShiftLockButton.IsEnabled = enabled && _captureTarget is BindingTarget.None or BindingTarget.ShiftLock;
        bool clearEnabled =
            enabled &&
            _captureTarget == BindingTarget.None;
        SetClearButtonState(ClearPlayButton, BindingTarget.Play, clearEnabled);
        SetClearButtonState(ClearUnitButton, BindingTarget.Unit, clearEnabled);
        SetClearButtonState(ClearAreasButton, BindingTarget.Areas, clearEnabled);
        SetClearButtonState(ClearCancelPlacementButton, BindingTarget.CancelPlacement, clearEnabled);
        SetClearButtonState(ClearQuickPlacementButton, BindingTarget.QuickPlacement, clearEnabled);
        SetClearButtonState(ClearTargetingButton, BindingTarget.Targeting, clearEnabled);
        SetClearButtonState(ClearUpgradeButton, BindingTarget.Upgrade, clearEnabled);
        SetClearButtonState(ClearAutoUpgradeUnitButton, BindingTarget.AutoUpgradeUnit, clearEnabled);
        SetClearButtonState(ClearToggleAutoUpgradePlacedUnitsButton, BindingTarget.ToggleAutoUpgradePlacedUnits, clearEnabled);
        SetClearButtonState(ClearShiftLockButton, BindingTarget.ShiftLock, clearEnabled);
    }

    private void SetClearButtonState(
        Button button,
        BindingTarget target,
        bool enabled)
    {
        bool configured = IsBindingConfigured(target);
        button.Visibility = configured
            ? Visibility.Visible
            : Visibility.Collapsed;
        button.IsEnabled = enabled && configured;
    }

    private bool IsBindingConfigured(
        BindingTarget target) => target switch
        {
            BindingTarget.Play =>
                !string.IsNullOrWhiteSpace(
                    Services.Settings.PlayMenuKey),
            BindingTarget.Unit =>
                !string.IsNullOrWhiteSpace(
                    Services.Settings.UnitMenuKey),
            BindingTarget.Areas =>
                !string.IsNullOrWhiteSpace(
                    Services.Settings.AreasMenuKey),
            BindingTarget.CancelPlacement =>
                !string.IsNullOrWhiteSpace(
                    Services.Settings.CancelPlacementKey),
            BindingTarget.QuickPlacement =>
                Services.Settings
                    .QuickPlacementVirtualKey != 0,
            BindingTarget.Targeting =>
                !string.IsNullOrWhiteSpace(
                    Services.Settings.ChangeUnitTargetingKey),
            BindingTarget.Upgrade =>
                !string.IsNullOrWhiteSpace(
                    Services.Settings.UpgradeUnitKey),
            BindingTarget.AutoUpgradeUnit =>
                !string.IsNullOrWhiteSpace(
                    Services.Settings.AutoUpgradeUnitKey),
            BindingTarget.ToggleAutoUpgradePlacedUnits =>
                !string.IsNullOrWhiteSpace(
                    Services.Settings
                        .ToggleAutoUpgradePlacedUnitsKey),
            BindingTarget.ShiftLock =>
                Services.Settings.ShiftLockVirtualKey != 0,
            _ => false,
        };

    private static string BindingLabel(
        BindingTarget target) => target switch
        {
            BindingTarget.Play => "Toggle Play Menu",
            BindingTarget.Unit => "Toggle Unit Inventory",
            BindingTarget.Areas => "Toggle Areas Menu",
            BindingTarget.CancelPlacement =>
                "Toggle Cancel Unit Placement",
            BindingTarget.QuickPlacement =>
                "Quick Placement",
            BindingTarget.Targeting => "Change Unit Targeting",
            BindingTarget.Upgrade => "Upgrade Unit",
            BindingTarget.AutoUpgradeUnit => "Auto Upgrade Unit",
            BindingTarget.ToggleAutoUpgradePlacedUnits =>
                "Toggle Auto Upgrade Placed Units",
            BindingTarget.ShiftLock => "Toggle Shift Lock",
            _ => throw new ArgumentOutOfRangeException(
                nameof(target)),
        };

    private TextBlock StatusFor(BindingTarget target) => target switch
    {
        BindingTarget.Macro => MacroStatusText,
        BindingTarget.Play => PlayStatusText,
        BindingTarget.Unit => UnitStatusText,
        BindingTarget.Areas => AreasStatusText,
        BindingTarget.CancelPlacement =>
            CancelPlacementStatusText,
        BindingTarget.QuickPlacement =>
            QuickPlacementStatusText,
        BindingTarget.Targeting => TargetingStatusText,
        BindingTarget.Upgrade => UpgradeStatusText,
        BindingTarget.AutoUpgradeUnit =>
            AutoUpgradeUnitStatusText,
        BindingTarget.ToggleAutoUpgradePlacedUnits =>
            ToggleAutoUpgradePlacedUnitsStatusText,
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
        Areas,
        CancelPlacement,
        QuickPlacement,
        Targeting,
        Upgrade,
        AutoUpgradeUnit,
        ToggleAutoUpgradePlacedUnits,
        ShiftLock,
    }
}
