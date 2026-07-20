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

    public int SchemaVersion { get; init; } = 1;

    public AppTheme Theme { get; init; } = AppTheme.System;

    public string SelectedPresetId { get; init; } = string.Empty;

    public string SelectedChallengePresetId { get; init; } = string.Empty;

    public string EncryptedWebhook { get; init; } = string.Empty;

    public string DiscordErrorUserId { get; init; } = string.Empty;

    public bool AutoCaptureOnMacroError { get; init; }

    public bool CheckDetectorUpdates { get; init; } = true;

    public DateTimeOffset? LastDetectorUpdateCheck { get; init; }

    public bool MinimizeDuringAutomation { get; init; }

    public int MacroHotkeyVirtualKey { get; init; } = DefaultMacroHotkeyVirtualKey;
}
