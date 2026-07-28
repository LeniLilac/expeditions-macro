namespace ExpeditionsMacro.Core.Models;

internal static class AppControlKeyPolicy
{
    private const string ConflictGuidance =
        " Scroll down to Controls on the Dashboard to choose different keys, and keep every game control matched to Anime Expeditions.";

    public static int ParseQuickPlacementKey(
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        int virtualKey =
            settings.QuickPlacementVirtualKey;
        if (!KeyboardKey.IsSupportedQuickPlacementKey(
                virtualKey))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Quick Placement key to the same physical key assigned in Anime Expeditions.");
        }
        ValidateControlKeySet(
            settings,
            requireUnitActionKeys: false);
        return virtualKey;
    }

    public static int? ParseOptionalQuickPlacementKey(
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.QuickPlacementVirtualKey == 0
            ? null
            : ParseQuickPlacementKey(settings);
    }

    public static void ValidateControlKeySet(
        AppSettings settings,
        bool requireUnitActionKeys)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<(string Label, char Key)> bindings = [];
        AddLetter(
            bindings,
            "Toggle Play Menu",
            settings.PlayMenuKey,
            required: false);
        AddLetter(
            bindings,
            "Toggle Unit Inventory",
            settings.UnitMenuKey,
            required: false);
        AddLetter(
            bindings,
            "Toggle Areas Menu",
            settings.AreasMenuKey,
            required: false);
        AddLetter(
            bindings,
            "Toggle Cancel Unit Placement",
            settings.CancelPlacementKey,
            required: false);
        AddLetter(
            bindings,
            "Change Unit Targeting",
            settings.ChangeUnitTargetingKey,
            requireUnitActionKeys);
        AddLetter(
            bindings,
            "Upgrade Unit",
            settings.UpgradeUnitKey,
            requireUnitActionKeys);
        AddLetter(
            bindings,
            "Auto Upgrade Unit",
            settings.AutoUpgradeUnitKey,
            requireUnitActionKeys);
        AddLetter(
            bindings,
            "Toggle Auto Upgrade Placed Units",
            settings
                .ToggleAutoUpgradePlacedUnitsKey,
            requireUnitActionKeys);

        ValidateQuickPlacement(
            settings,
            bindings);
        foreach ((string label, char key) in
                 bindings)
        {
            if (settings.MacroHotkeyVirtualKey == key)
            {
                throw new InvalidDataException(
                    $"{label} and the macro start/stop hotkey cannot both be {key}." +
                    ConflictGuidance);
            }
            if (settings.ShiftLockVirtualKey == key)
            {
                throw new InvalidDataException(
                    $"{label} and Toggle Shift Lock cannot both be {key}." +
                    ConflictGuidance);
            }
        }

        IGrouping<
            char,
            (string Label, char Key)>? duplicate =
            bindings
                .GroupBy(binding => binding.Key)
                .FirstOrDefault(group =>
                    group.Count() > 1);
        if (duplicate is null) return;

        string names = string.Join(
            " and ",
            duplicate.Select(binding =>
                binding.Label));
        throw new InvalidDataException(
            $"{names} cannot all use {duplicate.Key}." +
            ConflictGuidance);
    }

    public static char ParseRequiredLetter(
        string? value,
        string label)
    {
        string candidate =
            value?.Trim() ?? string.Empty;
        if (candidate.Length != 1 ||
            !char.IsAsciiLetter(candidate[0]))
        {
            throw new InvalidDataException(
                $"Scroll down to Controls on the Dashboard, then set {label} key to the same letter assigned in Anime Expeditions.");
        }
        return char.ToUpperInvariant(
            candidate[0]);
    }

    private static void ValidateQuickPlacement(
        AppSettings settings,
        IReadOnlyList<(
            string Label,
            char Key)> bindings)
    {
        int virtualKey =
            settings.QuickPlacementVirtualKey;
        if (virtualKey == 0) return;

        string display =
            KeyboardKey.GetDisplayName(virtualKey);
        if (!KeyboardKey.IsSupportedQuickPlacementKey(
                virtualKey))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then choose a supported physical key for Quick Placement and keep it matched to Anime Expeditions.");
        }
        if (settings.MacroHotkeyVirtualKey ==
            virtualKey)
        {
            throw new InvalidDataException(
                $"Quick Placement and the macro start/stop hotkey cannot both be {display}." +
                ConflictGuidance);
        }
        if (settings.ShiftLockVirtualKey ==
            virtualKey)
        {
            throw new InvalidDataException(
                $"Quick Placement and Toggle Shift Lock cannot both be {display}." +
                ConflictGuidance);
        }
        foreach ((string label, char key) in
                 bindings.Where(binding =>
                     binding.Key == virtualKey))
        {
            throw new InvalidDataException(
                $"Quick Placement and {label} cannot both use {display}." +
                ConflictGuidance);
        }
    }

    private static void AddLetter(
        ICollection<(string Label, char Key)> bindings,
        string label,
        string? value,
        bool required)
    {
        string candidate =
            value?.Trim() ?? string.Empty;
        if (candidate.Length == 0 && !required)
        {
            return;
        }
        bindings.Add(
            (
                label,
                ParseRequiredLetter(
                    candidate,
                    label)
            ));
    }
}
