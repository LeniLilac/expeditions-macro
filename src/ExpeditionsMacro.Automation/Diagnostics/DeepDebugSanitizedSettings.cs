using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Diagnostics;

internal sealed record DeepDebugSanitizedSettings(
    int SchemaVersion,
    AppTheme Theme,
    string SelectedPresetId,
    string SelectedChallengePresetId,
    string SelectedStoryPresetId,
    string SelectedRaidPresetId,
    string SelectedMacroPlanId,
    bool AutoCaptureOnMacroError,
    bool IncludeLogsInDiagnosticArchives,
    bool DeepDebugEnabled,
    bool DebugModeEnabled,
    bool AutoCheckGameSettingsOnStart,
    bool FastNoAlignEnabled,
    bool CheckDetectorUpdates,
    DateTimeOffset? LastDetectorUpdateCheck,
    bool MinimizeDuringAutomation,
    bool RestartRobloxWithPrivateServer,
    bool PrivateServerLinkConfigured,
    int MacroHotkeyVirtualKey,
    int ShiftLockVirtualKey,
    string PlayMenuKey,
    string UnitMenuKey,
    string AreasMenuKey,
    ResourceRefuelDebugSettings ResourceRefuelDebug)
{
    public static DeepDebugSanitizedSettings From(
        AppSettings settings) => new(
        settings.SchemaVersion,
        settings.Theme,
        settings.SelectedPresetId,
        settings.SelectedChallengePresetId,
        settings.SelectedStoryPresetId,
        settings.SelectedRaidPresetId,
        settings.SelectedMacroPlanId,
        settings.AutoCaptureOnMacroError,
        settings.IncludeLogsInDiagnosticArchives,
        settings.DeepDebugEnabled,
        settings.DebugModeEnabled,
        settings.AutoCheckGameSettingsOnStart,
        settings.FastNoAlignEnabled,
        settings.CheckDetectorUpdates,
        settings.LastDetectorUpdateCheck,
        settings.MinimizeDuringAutomation,
        settings.RestartRobloxWithPrivateServer,
        !string.IsNullOrWhiteSpace(
            settings.EncryptedPrivateServerLink),
        settings.MacroHotkeyVirtualKey,
        settings.ShiftLockVirtualKey,
        settings.PlayMenuKey,
        settings.UnitMenuKey,
        settings.AreasMenuKey,
        settings.ResourceRefuelDebug);
}
