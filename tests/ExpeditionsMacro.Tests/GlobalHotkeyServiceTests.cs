using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.Tests;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void DefaultsToF6()
    {
        using GlobalHotkeyService hotkey = new();

        Assert.Equal(AppSettings.DefaultMacroHotkeyVirtualKey, hotkey.VirtualKey);
        Assert.Equal("F6", hotkey.DisplayName);
    }

    [Fact]
    public void Configure_ChangesSupportedFunctionKeyAndRaisesBindingChanged()
    {
        using GlobalHotkeyService hotkey = new();
        int changes = 0;
        hotkey.BindingChanged += (_, _) => changes++;

        hotkey.Configure(0x77);

        Assert.Equal(0x77, hotkey.VirtualKey);
        Assert.Equal("F8", hotkey.DisplayName);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Configure_RejectsWindowsReservedF12()
    {
        using GlobalHotkeyService hotkey = new();

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => hotkey.Configure(0x7B));

        Assert.Contains("F12 is reserved", error.Message, StringComparison.Ordinal);
        Assert.Equal("F6", hotkey.DisplayName);
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x30, "0")]
    [InlineData(0x39, "9")]
    [InlineData(0x62, "Num 2")]
    [InlineData(0x6B, "Num +")]
    [InlineData(0xBA, ";")]
    [InlineData(0xBF, "/")]
    [InlineData(0xDB, "[")]
    [InlineData(0xDE, "'")]
    public void Configure_AcceptsLettersNumbersAndSymbols(int virtualKey, string expectedName)
    {
        using GlobalHotkeyService hotkey = new();

        hotkey.Configure(virtualKey);

        Assert.Equal(virtualKey, hotkey.VirtualKey);
        Assert.Equal(expectedName, hotkey.DisplayName);
    }

    [Theory]
    [InlineData(0x0D)] // Enter.
    [InlineData(0x10)] // Shift.
    [InlineData(0x1B)] // Escape.
    [InlineData(0x5B)] // Windows.
    public void Configure_RejectsControlKeys(int virtualKey)
    {
        using GlobalHotkeyService hotkey = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => hotkey.Configure(virtualKey));
    }
}
