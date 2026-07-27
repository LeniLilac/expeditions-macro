using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class KeyBindingGuidanceTests
{
    [Fact]
    public void UnsetControlBindingsPointToDashboardAndGameMatch()
    {
        List<InvalidDataException> errors =
        [
            Assert.Throws<InvalidDataException>(
                () => AppSettings.ParseUnitMenuKey(
                    string.Empty,
                    AppSettings.DefaultMacroHotkeyVirtualKey,
                    "P")),
            Assert.Throws<InvalidDataException>(
                () => AppSettings.ParseAreasMenuKey(
                    string.Empty,
                    AppSettings.DefaultMacroHotkeyVirtualKey,
                    "P",
                    "H")),
            Assert.Throws<InvalidDataException>(
                () => AppSettings.ParseCancelPlacementKey(
                    string.Empty,
                    AppSettings.DefaultMacroHotkeyVirtualKey,
                    "P",
                    "H",
                    "U",
                    KeyboardKey.LeftControl)),
            Assert.Throws<InvalidDataException>(
                () => AppSettings.ParseRequiredUnitActionKeys(
                    new AppSettings())),
        ];

        foreach (InvalidDataException error in errors)
        {
            Assert.Contains(
                "Scroll down to Controls on the Dashboard",
                error.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "Anime Expeditions",
                error.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Settings > Controls",
                error.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "macro settings",
                error.Message,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PlayHotkeyConflictPointsToDashboardControls()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => AppSettings.ParsePlayMenuKey(
                    "P",
                    'P'));

        Assert.Contains(
            "Scroll down to Controls on the Dashboard",
            error.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Settings > Controls",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GeneralControlConflictPointsToDashboardControls()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => AppSettings.ValidateControlKeySet(
                    new AppSettings
                    {
                        PlayMenuKey = "P",
                        UnitMenuKey = "P",
                    },
                    requireUnitActionKeys: false));

        Assert.Contains(
            "Scroll down to Controls on the Dashboard",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "keep every game control matched to Anime Expeditions",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoredPlayBindingSeparatesGameAndMacroDirections()
    {
        PlayMenuBindingException error = new('P');

        Assert.Contains(
            "Anime Expeditions Settings > Keybinds",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "scroll down to Controls on the Expeditions Macro Dashboard",
            error.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Expeditions Macro Settings",
            error.Message,
            StringComparison.Ordinal);
    }
}
