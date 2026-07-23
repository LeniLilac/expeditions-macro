namespace ExpeditionsMacro.Core.Models;

public enum AppTheme
{
    System,
    Dark,
    Light,
}

public sealed record AppSettings
{
    public const int DefaultMacroHotkeyVirtualKey = 0x75;

    public const int DefaultShiftLockVirtualKey = KeyboardKey.LeftControl;

    public const string DefaultPlayMenuKey = "";

    public const string DefaultUnitMenuKey = "";

    public const string PlayMenuKeySetupInstructions =
        "1. Go to the Settings menu in game\n" +
        "2. Go to the Keybinds section in settings\n" +
        "3. Find the Toggle Play Menu keybind\n" +
        "4. Set the keybind to a letter in game\n" +
        "5. Set fill in the keybind letter in the macro settings";

    public int SchemaVersion { get; init; } = 1;

    public AppTheme Theme { get; init; } = AppTheme.System;

    public string SelectedPresetId { get; init; } = string.Empty;

    public string SelectedChallengePresetId { get; init; } = string.Empty;

    public string SelectedStoryPresetId { get; init; } = string.Empty;

    public string SelectedRaidPresetId { get; init; } = string.Empty;

    public string SelectedMacroPlanId { get; init; } = string.Empty;

    public string EncryptedWebhook { get; init; } = string.Empty;

    public string EncryptedPrivateServerLink { get; init; } = string.Empty;

    public bool RestartRobloxWithPrivateServer { get; init; }

    public string DiscordErrorUserId { get; init; } = string.Empty;

    public bool AutoCaptureOnMacroError { get; init; } = true;

    public bool IncludeLogsInDiagnosticArchives { get; init; } = true;

    public bool DeepDebugEnabled { get; init; }

    public bool CheckDetectorUpdates { get; init; } = true;

    public DateTimeOffset? LastDetectorUpdateCheck { get; init; }

    public bool MinimizeDuringAutomation { get; init; }

    public int MacroHotkeyVirtualKey { get; init; } = DefaultMacroHotkeyVirtualKey;

    public int ShiftLockVirtualKey { get; init; } = DefaultShiftLockVirtualKey;

    public string PlayMenuKey { get; init; } = DefaultPlayMenuKey;

    public string UnitMenuKey { get; init; } = DefaultUnitMenuKey;

    public static int ParseShiftLockKey(
        int virtualKey,
        int macroHotkeyVirtualKey,
        string? playMenuKey,
        string? unitMenuKey)
    {
        string displayName = KeyboardKey.GetDisplayName(virtualKey);
        if (!KeyboardKey.IsSupportedShiftLockKey(virtualKey))
        {
            throw new InvalidDataException(
                "Choose Left/Right Shift, Left/Right Ctrl, or a supported letter, number, symbol, numpad, function, or common control key for Shift Lock.");
        }
        if (virtualKey == macroHotkeyVirtualKey)
        {
            throw new InvalidDataException($"The Shift Lock key and macro start/stop hotkey cannot both be {displayName}.");
        }

        foreach ((string Label, string? Value) binding in new[]
        {
            ("Play menu", playMenuKey),
            ("Unit menu", unitMenuKey),
        })
        {
            string candidate = binding.Value?.Trim() ?? string.Empty;
            if (candidate.Length == 1 && char.ToUpperInvariant(candidate[0]) == virtualKey)
            {
                throw new InvalidDataException($"The Shift Lock key and {binding.Label} key cannot both be {displayName}.");
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
                $"The Play menu key and macro start/stop hotkey cannot both be {key}. Choose different keys under Settings > Controls.");
        }

        return key;
    }

    public static char ParseUnitMenuKey(string? value, int macroHotkeyVirtualKey, string? playMenuKey)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length != 1 || !char.IsAsciiLetter(candidate[0]))
        {
            throw new InvalidDataException(
                "Set the Unit menu key under Settings > Controls to the same letter assigned to Toggle Units in Anime Expeditions.");
        }

        char key = char.ToUpperInvariant(candidate[0]);
        if (macroHotkeyVirtualKey == key)
        {
            throw new InvalidDataException($"The Unit menu key and macro start/stop hotkey cannot both be {key}.");
        }

        string play = playMenuKey?.Trim() ?? string.Empty;
        if (play.Length == 1 && char.ToUpperInvariant(play[0]) == key)
        {
            throw new InvalidDataException("The Unit menu key and Play menu key must be different.");
        }

        return key;
    }
}
