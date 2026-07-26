using System.IO;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class AutomationKeyPressTests
{
    [Theory]
    [InlineData(0x57, "W")]
    [InlineData(0x20, "Space")]
    [InlineData(0x25, "Left Arrow")]
    [InlineData(KeyboardKey.LeftShift, "Left Shift")]
    public void Create_AcceptsSupportedPhysicalKeys(
        int virtualKey,
        string expectedName)
    {
        AutomationKeyPress keyPress =
            AutomationKeyPress.Create(
                virtualKey,
                holdMilliseconds: 2500,
                AppSettings.DefaultMacroHotkeyVirtualKey);

        Assert.Equal(virtualKey, keyPress.VirtualKey);
        Assert.Equal(2500, keyPress.HoldMilliseconds);
        Assert.Equal(expectedName, keyPress.KeyName);
    }

    [Fact]
    public void Create_RejectsTheMacroHotkey()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(() =>
                AutomationKeyPress.Create(
                    AppSettings.DefaultMacroHotkeyVirtualKey,
                    holdMilliseconds: 1000,
                    AppSettings.DefaultMacroHotkeyVirtualKey));

        Assert.Contains(
            "macro start and stop hotkey",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(120001)]
    public void Create_RejectsUnsafeHoldDurations(
        int holdMilliseconds)
    {
        Assert.Throws<InvalidDataException>(() =>
            AutomationKeyPress.Create(
                0x57,
                holdMilliseconds,
                AppSettings.DefaultMacroHotkeyVirtualKey));
    }

    [Theory]
    [InlineData(0x1B)]
    [InlineData(0x5B)]
    [InlineData(0xA4)]
    public void Create_RejectsUnsupportedSystemKeys(
        int virtualKey)
    {
        Assert.Throws<InvalidDataException>(() =>
            AutomationKeyPress.Create(
                virtualKey,
                holdMilliseconds: 1000,
                AppSettings.DefaultMacroHotkeyVirtualKey));
    }
}
