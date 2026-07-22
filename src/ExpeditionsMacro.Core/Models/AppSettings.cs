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

    public const string DefaultPlayMenuKey = "";

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

    public string EncryptedWebhook { get; init; } = string.Empty;

    public string DiscordErrorUserId { get; init; } = string.Empty;

    public bool AutoCaptureOnMacroError { get; init; } = true;

    public bool IncludeLogsInDiagnosticArchives { get; init; } = true;

    public bool CheckDetectorUpdates { get; init; } = true;

    public DateTimeOffset? LastDetectorUpdateCheck { get; init; }

    public bool MinimizeDuringAutomation { get; init; }

    public int MacroHotkeyVirtualKey { get; init; } = DefaultMacroHotkeyVirtualKey;

    public string PlayMenuKey { get; init; } = DefaultPlayMenuKey;

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
}
