using System.IO;

namespace ExpeditionsMacro.Core.Models;

public readonly record struct AutomationKeyPress(
    int VirtualKey,
    int HoldMilliseconds)
{
    public const int DefaultVirtualKey = 0x57;
    public const int DefaultHoldMilliseconds = 1000;
    public const int MinimumHoldMilliseconds = 1;
    public const int MaximumHoldMilliseconds = 120000;

    public string KeyName =>
        KeyboardKey.GetDisplayName(VirtualKey);

    public static AutomationKeyPress Create(
        int virtualKey,
        int holdMilliseconds,
        int macroHotkeyVirtualKey)
    {
        if (!KeyboardKey.IsSupportedAutomationKey(virtualKey))
        {
            throw new InvalidDataException(
                "Choose a supported letter, number, navigation, punctuation, numpad, function, modifier, or common control key.");
        }
        if (virtualKey == macroHotkeyVirtualKey)
        {
            throw new InvalidDataException(
                "Choose a key other than the macro start and stop hotkey.");
        }
        if (holdMilliseconds is <
                MinimumHoldMilliseconds or >
                MaximumHoldMilliseconds)
        {
            throw new InvalidDataException(
                $"Keypress time must be between {MinimumHoldMilliseconds} and {MaximumHoldMilliseconds} ms.");
        }
        return new AutomationKeyPress(
            virtualKey,
            holdMilliseconds);
    }
}
