using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class KeyboardKeyTests
{
    [Fact]
    public void ShiftLockKey_DefaultsToLeftControl()
    {
        AppSettings settings = new();

        Assert.Equal(KeyboardKey.LeftControl, settings.ShiftLockVirtualKey);
        Assert.Equal("Left Ctrl", KeyboardKey.GetDisplayName(settings.ShiftLockVirtualKey));
    }

    [Theory]
    [InlineData(KeyboardKey.LeftShift)]
    [InlineData(KeyboardKey.RightShift)]
    [InlineData(KeyboardKey.LeftControl)]
    [InlineData(KeyboardKey.RightControl)]
    [InlineData(0x41)]
    [InlineData(0x35)]
    [InlineData(0x20)]
    [InlineData(0x7B)]
    public void ShiftLockKey_AcceptsModifiersLettersNumbersAndOtherPhysicalKeys(int virtualKey)
    {
        int parsed = AppSettings.ParseShiftLockKey(virtualKey, AppSettings.DefaultMacroHotkeyVirtualKey, "P", "U");

        Assert.Equal(virtualKey, parsed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x5B)]
    [InlineData(0xA4)]
    [InlineData(0x25)]
    public void ShiftLockKey_RejectsUnsupportedSystemAndCameraKeys(int virtualKey)
    {
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseShiftLockKey(virtualKey, AppSettings.DefaultMacroHotkeyVirtualKey, "P", "U"));
    }

    [Theory]
    [InlineData(0x50, 0x50, "U")]
    [InlineData(0x50, 0x75, "P")]
    [InlineData(0x55, 0x75, "U")]
    public void ShiftLockKey_MustDifferFromOtherMacroBindings(int shiftLockKey, int macroKey, string menuKey)
    {
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseShiftLockKey(shiftLockKey, macroKey, menuKey, menuKey));
    }
}
