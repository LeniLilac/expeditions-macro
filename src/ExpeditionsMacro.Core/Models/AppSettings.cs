using System.Text.Json.Serialization;

namespace ExpeditionsMacro.Core.Models;

public enum AppTheme
{
    System,
    Dark,
    Light,
}

public sealed record AppSettings
{
    private const string ControlConflictGuidance =
        " Scroll down to Controls on the Dashboard to choose different keys, and keep every game control matched to Anime Expeditions.";

    public const int CurrentSchemaVersion = 3;

    public const int DefaultMacroHotkeyVirtualKey = 0x75;

    public const int DefaultShiftLockVirtualKey = KeyboardKey.LeftControl;

    public const string DefaultPlayMenuKey = "";

    public const string DefaultUnitMenuKey = "";

    public const string DefaultAreasMenuKey = "";

    public const char DefaultCancelPlacementKeyChar = 'Z';

    public const string DefaultCancelPlacementKey = "Z";

    public const string DefaultChangeUnitTargetingKey = "";

    public const string DefaultUpgradeUnitKey = "";

    public const string DefaultAutoUpgradeUnitKey = "";

    public const string
        DefaultToggleAutoUpgradePlacedUnitsKey = "";

    public const string PlayMenuKeySetupInstructions =
        "1. Open Settings in Anime Expeditions\n" +
        "2. Open the Keybinds section\n" +
        "3. Set Toggle Play Menu to an A-Z letter\n" +
        "4. Open the Dashboard in Expeditions Macro\n" +
        "5. Scroll down to Controls and set Toggle Play Menu key to the same letter";

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public AppTheme Theme { get; init; } = AppTheme.System;

    public string SelectedPresetId { get; init; } = string.Empty;

    public string SelectedChallengePresetId { get; init; } = string.Empty;

    public string SelectedStoryPresetId { get; init; } = string.Empty;

    public string SelectedRaidPresetId { get; init; } = string.Empty;

    public string SelectedMacroPlanId { get; init; } = string.Empty;

    public string EncryptedWebhook { get; init; } = string.Empty;

    public string EncryptedPrivateServerLink { get; init; } = string.Empty;

    public bool RestartRobloxWithPrivateServer { get; init; } = true;

    public bool RestartRobloxAtMacroStart { get; init; } = true;

    public string DiscordErrorUserId { get; init; } = string.Empty;

    public bool AutoCaptureOnMacroError { get; init; } = true;

    public bool IncludeLogsInDiagnosticArchives { get; init; } = true;

    public bool DeepDebugEnabled { get; init; }

    public bool DebugModeEnabled { get; init; }

    public bool AutoCheckUiScaleOnStart { get; init; } = true;

    public bool AutoCheckGameSettingsOnStart { get; init; } = true;

    public bool FastNoAlignEnabled { get; init; } = true;

    public bool ManualInputRecordingEnabled { get; init; }

    public bool MinimizeDuringAutomation { get; init; }

    public int MacroHotkeyVirtualKey { get; init; } = DefaultMacroHotkeyVirtualKey;

    public int ShiftLockVirtualKey { get; init; } = DefaultShiftLockVirtualKey;

    public string PlayMenuKey { get; init; } = DefaultPlayMenuKey;

    public string UnitMenuKey { get; init; } = DefaultUnitMenuKey;

    public string AreasMenuKey { get; init; } = DefaultAreasMenuKey;

    public string CancelPlacementKey { get; init; } =
        DefaultCancelPlacementKey;

    public string ChangeUnitTargetingKey { get; init; } =
        DefaultChangeUnitTargetingKey;

    public string UpgradeUnitKey { get; init; } =
        DefaultUpgradeUnitKey;

    public string AutoUpgradeUnitKey { get; init; } =
        DefaultAutoUpgradeUnitKey;

    [JsonPropertyName("toggle_auto_upgrade_unit_key")]
    public string ToggleAutoUpgradePlacedUnitsKey
    {
        get;
        init;
    } = DefaultToggleAutoUpgradePlacedUnitsKey;

    public ResourceRefuelDebugSettings ResourceRefuelDebug { get; init; } = new();

    public static int ParseShiftLockKey(
        int virtualKey,
        int macroHotkeyVirtualKey,
        string? playMenuKey,
        string? unitMenuKey,
        string? areasMenuKey = null,
        string? cancelPlacementKey = null)
    {
        string displayName = KeyboardKey.GetDisplayName(virtualKey);
        if (!KeyboardKey.IsSupportedShiftLockKey(virtualKey))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard and choose Left/Right Shift, Left/Right Ctrl, or a supported letter, number, symbol, numpad, function, or common control key for Toggle Shift Lock. Keep it matched to Anime Expeditions.");
        }
        if (virtualKey == macroHotkeyVirtualKey)
        {
            throw new InvalidDataException(
                $"The Toggle Shift Lock key and macro start/stop hotkey cannot both be {displayName}." +
                ControlConflictGuidance);
        }

        foreach ((string Label, string? Value) binding in new[]
        {
            ("Toggle Play Menu", playMenuKey),
            ("Toggle Unit Inventory", unitMenuKey),
            ("Toggle Areas Menu", areasMenuKey),
            ("Toggle Cancel Unit Placement", cancelPlacementKey),
        })
        {
            string candidate = binding.Value?.Trim() ?? string.Empty;
            if (candidate.Length == 1 && char.ToUpperInvariant(candidate[0]) == virtualKey)
            {
                throw new InvalidDataException(
                    $"The Toggle Shift Lock key and {binding.Label} key cannot both be {displayName}." +
                    ControlConflictGuidance);
            }
        }

        return virtualKey;
    }

    public static char ParsePlayMenuKey(string? value)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length != 1 || !char.IsAsciiLetter(candidate[0]))
        {
            throw new InvalidDataException(PlayMenuKeySetupInstructions);
        }

        return char.ToUpperInvariant(candidate[0]);
    }

    public static char ParsePlayMenuKey(string? value, int macroHotkeyVirtualKey)
    {
        char key = ParsePlayMenuKey(value);
        if (macroHotkeyVirtualKey == key)
        {
            throw new InvalidDataException(
                $"The Toggle Play Menu key and macro start/stop hotkey cannot both be {key}." +
                ControlConflictGuidance);
        }

        return key;
    }

    public static char ParseUnitMenuKey(string? value, int macroHotkeyVirtualKey, string? playMenuKey)
        => ParseUnitMenuKey(
            value,
            macroHotkeyVirtualKey,
            playMenuKey,
            areasMenuKey: null);

    public static char ParseUnitMenuKey(
        string? value,
        int macroHotkeyVirtualKey,
        string? playMenuKey,
        string? areasMenuKey)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length != 1 || !char.IsAsciiLetter(candidate[0]))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Unit Inventory key to the same letter assigned in Anime Expeditions.");
        }

        char key = char.ToUpperInvariant(candidate[0]);
        if (macroHotkeyVirtualKey == key)
        {
            throw new InvalidDataException(
                $"The Toggle Unit Inventory key and macro start/stop hotkey cannot both be {key}." +
                ControlConflictGuidance);
        }

        string play = playMenuKey?.Trim() ?? string.Empty;
        if (play.Length == 1 && char.ToUpperInvariant(play[0]) == key)
        {
            throw new InvalidDataException(
                "The Toggle Unit Inventory key and Toggle Play Menu key must be different." +
                ControlConflictGuidance);
        }

        string areas = areasMenuKey?.Trim() ?? string.Empty;
        if (areas.Length == 1 &&
            char.ToUpperInvariant(areas[0]) == key)
        {
            throw new InvalidDataException(
                "The Toggle Unit Inventory key and Toggle Areas Menu key must be different." +
                ControlConflictGuidance);
        }

        return key;
    }

    public static char ParseAreasMenuKey(
        string? value,
        int macroHotkeyVirtualKey,
        string? playMenuKey,
        string? unitMenuKey)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length != 1 ||
            !char.IsAsciiLetter(candidate[0]))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Areas Menu key to the same letter assigned in Anime Expeditions.");
        }

        char key = char.ToUpperInvariant(candidate[0]);
        if (macroHotkeyVirtualKey == key)
        {
            throw new InvalidDataException(
                $"The Toggle Areas Menu key and macro start/stop hotkey cannot both be {key}." +
                ControlConflictGuidance);
        }

        foreach ((string Label, string? Value) binding in new[]
        {
            ("Toggle Play Menu", playMenuKey),
            ("Toggle Unit Inventory", unitMenuKey),
        })
        {
            string other = binding.Value?.Trim() ?? string.Empty;
            if (other.Length == 1 &&
                char.ToUpperInvariant(other[0]) == key)
            {
                throw new InvalidDataException(
                    $"The Toggle Areas Menu key and {binding.Label} key must be different." +
                    ControlConflictGuidance);
            }
        }

        return key;
    }

    public static char ParseCancelPlacementKey(
        string? value,
        int macroHotkeyVirtualKey,
        string? playMenuKey,
        string? unitMenuKey,
        string? areasMenuKey,
        int shiftLockVirtualKey)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length != 1 ||
            !char.IsAsciiLetter(candidate[0]))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Cancel Unit Placement key to the same letter assigned in Anime Expeditions.");
        }

        char key = char.ToUpperInvariant(candidate[0]);
        if (macroHotkeyVirtualKey == key)
        {
            throw new InvalidDataException(
                $"The Toggle Cancel Unit Placement key and macro start/stop hotkey cannot both be {key}." +
                ControlConflictGuidance);
        }
        if (shiftLockVirtualKey == key)
        {
            throw new InvalidDataException(
                $"The Toggle Cancel Unit Placement key and Toggle Shift Lock key cannot both be {key}." +
                ControlConflictGuidance);
        }

        foreach ((string Label, string? Value) binding in new[]
        {
            ("Toggle Play Menu", playMenuKey),
            ("Toggle Unit Inventory", unitMenuKey),
            ("Toggle Areas Menu", areasMenuKey),
        })
        {
            string other = binding.Value?.Trim() ??
                string.Empty;
            if (other.Length == 1 &&
                char.ToUpperInvariant(other[0]) == key)
            {
                throw new InvalidDataException(
                    $"The Toggle Cancel Unit Placement key and {binding.Label} key must be different." +
                    ControlConflictGuidance);
            }
        }

        return key;
    }

    public static UnitActionKeys ParseRequiredUnitActionKeys(
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateControlKeySet(
            settings,
            requireUnitActionKeys: true);
        return new UnitActionKeys(
            ParseRequiredLetter(
                settings.ChangeUnitTargetingKey,
                "Change Unit Targeting"),
            ParseRequiredLetter(
                settings.UpgradeUnitKey,
                "Upgrade Unit"),
            ParseRequiredLetter(
                settings.AutoUpgradeUnitKey,
                "Auto Upgrade Unit"),
            ParseRequiredLetter(
                settings.ToggleAutoUpgradePlacedUnitsKey,
                "Toggle Auto Upgrade Placed Units"));
    }

    public static void ValidateControlKeySet(
        AppSettings settings,
        bool requireUnitActionKeys)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<(string Label, char Key)> bindings = [];
        AddLetter(
            bindings,
            "Toggle Play Menu",
            settings.PlayMenuKey,
            required: false);
        AddLetter(
            bindings,
            "Toggle Unit Inventory",
            settings.UnitMenuKey,
            required: false);
        AddLetter(
            bindings,
            "Toggle Areas Menu",
            settings.AreasMenuKey,
            required: false);
        AddLetter(
            bindings,
            "Toggle Cancel Unit Placement",
            settings.CancelPlacementKey,
            required: true);
        AddLetter(
            bindings,
            "Change Unit Targeting",
            settings.ChangeUnitTargetingKey,
            requireUnitActionKeys);
        AddLetter(
            bindings,
            "Upgrade Unit",
            settings.UpgradeUnitKey,
            requireUnitActionKeys);
        AddLetter(
            bindings,
            "Auto Upgrade Unit",
            settings.AutoUpgradeUnitKey,
            requireUnitActionKeys);
        AddLetter(
            bindings,
            "Toggle Auto Upgrade Placed Units",
            settings.ToggleAutoUpgradePlacedUnitsKey,
            requireUnitActionKeys);

        foreach ((string label, char key) in bindings)
        {
            if (settings.MacroHotkeyVirtualKey == key)
            {
                throw new InvalidDataException(
                    $"{label} and the macro start/stop hotkey cannot both be {key}." +
                    ControlConflictGuidance);
            }
            if (settings.ShiftLockVirtualKey == key)
            {
                throw new InvalidDataException(
                    $"{label} and Toggle Shift Lock cannot both be {key}." +
                    ControlConflictGuidance);
            }
        }

        IGrouping<char, (string Label, char Key)>? duplicate =
            bindings
                .GroupBy(binding => binding.Key)
                .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            string names = string.Join(
                " and ",
                duplicate.Select(binding => binding.Label));
            throw new InvalidDataException(
                $"{names} cannot all use {duplicate.Key}." +
                ControlConflictGuidance);
        }
    }

    private static void AddLetter(
        ICollection<(string Label, char Key)> bindings,
        string label,
        string? value,
        bool required)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length == 0 && !required) return;
        bindings.Add(
            (label, ParseRequiredLetter(candidate, label)));
    }

    private static char ParseRequiredLetter(
        string? value,
        string label)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length != 1 ||
            !char.IsAsciiLetter(candidate[0]))
        {
            throw new InvalidDataException(
                $"Scroll down to Controls on the Dashboard, then set {label} key to the same letter assigned in Anime Expeditions.");
        }
        return char.ToUpperInvariant(candidate[0]);
    }
}

public readonly record struct UnitActionKeys(
    char ChangeTargeting,
    char Upgrade,
    char AutoUpgrade,
    char ToggleAutoUpgradePlacedUnits);
