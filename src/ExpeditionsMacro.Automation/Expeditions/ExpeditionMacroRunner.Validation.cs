using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private static readonly HashSet<string>
        RecoveryStates = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "afk",
            "disconnect",
            "lobby",
            "play",
            "map_select",
            "map_preview",
            "post_match_party",
        };

    private static string Label(
        string value) =>
        value.Equals(
            "afk",
            StringComparison.OrdinalIgnoreCase)
            ? "AFK Chamber"
            : System.Globalization.CultureInfo
                .InvariantCulture.TextInfo
                .ToTitleCase(
                    value.Replace('_', ' '));

    private void Focus(
        RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }

    private static char ValidatePlayMenuKey(
        char value)
    {
        char normalized =
            char.ToUpperInvariant(value);
        if (!char.IsAsciiLetter(normalized))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Play Menu key to match Anime Expeditions' Toggle Play Menu binding.");
        }
        return normalized;
    }

    private static void ValidateTeamKey(
        bool required,
        char? value)
    {
        if (!required)
        {
            return;
        }
        if (value is null ||
            !char.IsAsciiLetter(value.Value))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Unit Inventory key to match Anime Expeditions' Toggle Unit Inventory binding before using a saved team.");
        }
    }

    private sealed record RunTerminal(
        string State,
        ImageFrame Frame);

    private sealed class RecoveryNeededException :
        Exception
    {
        public RecoveryNeededException(
            string state)
            : base(
                $"Recovery screen recognized: {state}.")
        {
            State = state;
        }

        public string State { get; }
    }
}
