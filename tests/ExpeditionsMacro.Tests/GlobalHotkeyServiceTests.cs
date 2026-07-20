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
}
