using ExpeditionsMacro.Core.Abstractions;

namespace ExpeditionsMacro.Windows;

public sealed partial class WindowsRobloxAutomation
{
    public bool Focus(RobloxWindow window)
    {
        nint handle = ResolveHandle(window);
        WindowsRobloxDisplayScale.EnsureOneHundredPercent(handle);
        if (TryFocus(handle)) return true;

        RobloxWindow? refreshed = FindWindow();
        if (refreshed is null) return false;
        RegisterAlias(window.Handle, refreshed.Value.Handle);
        if (handle != refreshed.Value.Handle)
        {
            DiagnosticMessage?.Invoke(
                "Roblox window refreshed after a focus failure: " +
                $"{refreshed.Value.ProcessDescription}.");
        }
        WindowsRobloxDisplayScale.EnsureOneHundredPercent(
            refreshed.Value.Handle);
        RevalidateTrackedClientSize(refreshed.Value.Handle);
        return TryFocus(refreshed.Value.Handle);
    }
}
