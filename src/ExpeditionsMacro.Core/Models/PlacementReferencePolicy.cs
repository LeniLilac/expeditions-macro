namespace ExpeditionsMacro.Core.Models;

public static class PlacementReferencePolicy
{
    public const int MaximumPlacementIdLength = 64;

    public static string CreatePlacementId() =>
        Guid.NewGuid().ToString("N");

    public static IReadOnlyList<PlacementStep> Normalize(
        IReadOnlyList<PlacementStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        HashSet<string> reservedIds =
            steps.Where(step =>
                    step.Kind == MatchStepKind.Placement &&
                    !string.IsNullOrWhiteSpace(
                        step.PlacementId))
                .Select(step => step.PlacementId)
                .ToHashSet(StringComparer.Ordinal);
        List<PlacementStep> normalized =
            new(steps.Count);
        List<PlacementStep> placements = [];

        for (int index = 0;
             index < steps.Count;
             index++)
        {
            PlacementStep step = steps[index];
            if (step.Kind == MatchStepKind.Placement)
            {
                string placementId =
                    string.IsNullOrWhiteSpace(
                        step.PlacementId)
                        ? AllocateLegacyId(
                            index,
                            reservedIds)
                        : step.PlacementId;
                PlacementStep placement = step with
                {
                    PlacementId = placementId,
                    TargetPlacementId = string.Empty,
                };
                normalized.Add(placement);
                placements.Add(placement);
                continue;
            }

            if (step.Kind is
                MatchStepKind.ReconfigureUnit or
                MatchStepKind.UpgradeUnit or
                MatchStepKind.SellUnit)
            {
                PlacementStep? target =
                    FindTarget(placements, step);
                normalized.Add(step with
                {
                    PlacementId = string.Empty,
                    TargetPlacementId =
                        target?.PlacementId ??
                        step.TargetPlacementId,
                    UnitKey =
                        target?.UnitKey ??
                        step.UnitKey,
                    X = 0,
                    Y = 0,
                });
                continue;
            }

            normalized.Add(step with
            {
                PlacementId = string.Empty,
                TargetPlacementId = string.Empty,
            });
        }

        return normalized;
    }

    public static void Validate(
        IReadOnlyList<PlacementStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        Dictionary<string, PlacementStep> placements =
            new(StringComparer.Ordinal);
        HashSet<string> soldPlacements =
            new(StringComparer.Ordinal);
        foreach (PlacementStep step in steps)
        {
            if (step.Kind == MatchStepKind.Placement)
            {
                if (!placements.TryAdd(
                        step.PlacementId,
                        step))
                {
                    throw new InvalidDataException(
                        "Two placement steps have the same internal identity.");
                }
                continue;
            }

            if (step.Kind is not
                (MatchStepKind.ReconfigureUnit or
                 MatchStepKind.UpgradeUnit or
                 MatchStepKind.SellUnit))
            {
                continue;
            }

            if (!placements.TryGetValue(
                    step.TargetPlacementId,
                    out PlacementStep? target))
            {
                throw new InvalidDataException(
                    $"The {step.Kind} step must target a placed unit that appears earlier in Match Steps.");
            }
            if (step.UnitKey != target.UnitKey)
            {
                throw new InvalidDataException(
                    "A unit action does not match its referenced placement.");
            }
            if (soldPlacements.Contains(
                    step.TargetPlacementId))
            {
                throw new InvalidDataException(
                    "A unit action cannot target a unit after its Sell Unit step.");
            }
            if (step.Kind == MatchStepKind.SellUnit)
            {
                soldPlacements.Add(
                    step.TargetPlacementId);
            }
        }
    }

    public static IReadOnlyDictionary<string, string>
        BuildDisplayLabels(
        IReadOnlyList<PlacementStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        PlacementStep[] placements =
            steps.Where(step =>
                    step.Kind ==
                    MatchStepKind.Placement)
                .ToArray();
        IReadOnlyDictionary<int, int> counts =
            placements.GroupBy(step => step.UnitKey)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count());
        Dictionary<int, int> offsets = [];
        Dictionary<string, string> labels =
            new(StringComparer.Ordinal);
        foreach (PlacementStep placement in placements)
        {
            int offset =
                offsets.GetValueOrDefault(
                    placement.UnitKey);
            offsets[placement.UnitKey] = offset + 1;
            string label =
                counts[placement.UnitKey] == 1
                    ? placement.UnitKey.ToString()
                    : $"{placement.UnitKey}{AlphabeticId(offset)}";
            labels.Add(placement.PlacementId, label);
        }
        return labels;
    }

    public static PlacementStep ResolveTarget(
        IReadOnlyList<PlacementStep> steps,
        PlacementStep action)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(action);
        if (action.Kind is not
            (MatchStepKind.ReconfigureUnit or
             MatchStepKind.UpgradeUnit or
             MatchStepKind.SellUnit))
        {
            throw new ArgumentException(
                "Only unit actions reference a placement.",
                nameof(action));
        }
        return steps.FirstOrDefault(step =>
                   step.Kind ==
                       MatchStepKind.Placement &&
                   string.Equals(
                       step.PlacementId,
                       action.TargetPlacementId,
                       StringComparison.Ordinal)) ??
            throw new InvalidDataException(
                "A unit action references a placement that is no longer available.");
    }

    public static IReadOnlyList<PlacementStep>
        RemovePlacementAndReferences(
            IReadOnlyList<PlacementStep> steps,
            string placementId)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            placementId);
        return steps.Where(step =>
                !(step.Kind ==
                      MatchStepKind.Placement &&
                  string.Equals(
                      step.PlacementId,
                      placementId,
                      StringComparison.Ordinal)) &&
                !(step.Kind is
                      MatchStepKind.ReconfigureUnit or
                      MatchStepKind.UpgradeUnit or
                      MatchStepKind.SellUnit &&
                  string.Equals(
                      step.TargetPlacementId,
                      placementId,
                      StringComparison.Ordinal)))
            .ToArray();
    }

    public static bool IsValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumPlacementIdLength &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_');

    private static PlacementStep? FindTarget(
        IReadOnlyList<PlacementStep> placements,
        PlacementStep action)
    {
        if (!string.IsNullOrWhiteSpace(
                action.TargetPlacementId))
        {
            return placements.LastOrDefault(
                placement =>
                    string.Equals(
                        placement.PlacementId,
                        action.TargetPlacementId,
                        StringComparison.Ordinal));
        }
        return placements.LastOrDefault(
            placement =>
                placement.UnitKey == action.UnitKey &&
                placement.X == action.X &&
                placement.Y == action.Y);
    }

    private static string AllocateLegacyId(
        int index,
        ISet<string> reservedIds)
    {
        string prefix = $"placement-{index + 1}";
        string candidate = prefix;
        int suffix = 2;
        while (!reservedIds.Add(candidate))
        {
            candidate = $"{prefix}-{suffix++}";
        }
        return candidate;
    }

    private static string AlphabeticId(
        int zeroBased)
    {
        int value = checked(zeroBased + 1);
        Span<char> buffer = stackalloc char[8];
        int position = buffer.Length;
        while (value > 0)
        {
            value--;
            buffer[--position] =
                (char)('a' + (value % 26));
            value /= 26;
        }
        return new string(buffer[position..]);
    }
}
