using System.Collections;
using System.Reflection;
using System.Text;
using ExpeditionsMacro.Core.Geometry;

namespace ExpeditionsMacro.Vision.Inspection;

internal sealed record ProductionNumericGate(
    string Name,
    double Value,
    bool IsMaximum);

internal static class ProductionDetectorMetadata
{
    private const BindingFlags StaticMembers =
        BindingFlags.Static |
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    public static IReadOnlyList<DetectorInspectionRegion>
        ReadRegions(IReadOnlyList<Type> owners)
    {
        List<DetectorInspectionRegion> regions = [];
        foreach (Type owner in owners.Distinct())
        {
            foreach (FieldInfo field in owner.GetFields(
                         StaticMembers))
            {
                AddRegions(
                    regions,
                    owner,
                    field.Name,
                    ReadField(field));
            }
            foreach (PropertyInfo property in owner.GetProperties(
                         StaticMembers)
                         .Where(property =>
                             property.GetMethod is
                             {
                                 IsStatic: true,
                             } &&
                             property.GetIndexParameters().Length == 0))
            {
                AddRegions(
                    regions,
                    owner,
                    property.Name,
                    ReadProperty(property));
            }
        }

        return regions
            .DistinctBy(region => region.Id)
            .OrderBy(region => region.Region.Y)
            .ThenBy(region => region.Region.X)
            .ThenBy(region => region.Label)
            .ToArray();
    }

    public static IReadOnlyList<ProductionNumericGate>
        ReadNumericGates(IReadOnlyList<Type> owners)
    {
        List<ProductionNumericGate> gates = [];
        foreach (Type owner in owners.Distinct())
        {
            foreach (FieldInfo field in owner.GetFields(
                         StaticMembers))
            {
                if (!field.IsLiteral ||
                    field.IsInitOnly ||
                    !LooksLikeGate(field.Name))
                {
                    continue;
                }
                object? value;
                try
                {
                    value = field.GetRawConstantValue();
                }
                catch (Exception error) when (
                    error is InvalidOperationException or
                        NotSupportedException)
                {
                    continue;
                }
                if (!TryNumber(value, out double number))
                {
                    continue;
                }
                gates.Add(new ProductionNumericGate(
                    field.Name,
                    number,
                    field.Name.StartsWith(
                        "Maximum",
                        StringComparison.OrdinalIgnoreCase)));
            }
        }
        return gates;
    }

    public static IReadOnlyList<string> FindRegions(
        string metric,
        IReadOnlyList<DetectorInspectionRegion> regions)
    {
        string[] metricTokens = Tokens(metric);
        if (metricTokens.Length == 0)
        {
            return [];
        }
        (DetectorInspectionRegion Region, int Score)[] ranked =
            regions
                .Select(region => (
                    region,
                    Tokens(region.Label)
                        .Intersect(
                            metricTokens,
                            StringComparer.OrdinalIgnoreCase)
                        .Count()))
                .Where(candidate =>
                    candidate.Item2 > 0)
                .OrderByDescending(candidate =>
                    candidate.Item2)
                .ToArray();
        if (ranked.Length == 0)
        {
            return [];
        }
        int best = ranked[0].Score;
        return ranked
            .Where(candidate =>
                candidate.Score == best)
            .Select(candidate =>
                candidate.Region.Id)
            .ToArray();
    }

    public static string FriendlyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Evidence";
        }
        StringBuilder result = new(value.Length + 8);
        char previous = '\0';
        foreach (char current in value
                     .Replace('_', ' ')
                     .Replace('.', ' '))
        {
            if (char.IsUpper(current) &&
                previous != '\0' &&
                !char.IsUpper(previous) &&
                previous != ' ')
            {
                result.Append(' ');
            }
            result.Append(current);
            previous = current;
        }
        string text = result
            .ToString()
            .Trim();
        return text.Length == 0
            ? "Evidence"
            : char.ToUpperInvariant(text[0]) +
              text[1..];
    }

    public static string ExpectedEvidence(string name)
    {
        string lower = name.ToLowerInvariant();
        string? color = new[]
            {
                "cyan",
                "green",
                "red",
                "orange",
                "blue",
                "gray",
                "neutral",
                "dark",
                "bright",
                "white",
            }
            .FirstOrDefault(lower.Contains);
        if (color is not null)
        {
            return $"{FriendlyName(color)} color evidence in the production-owned region.";
        }
        if (new[]
            {
                "line",
                "rail",
                "panel",
                "header",
                "footer",
                "edge",
                "body",
                "support",
                "separator",
                "dialog",
            }
            .Any(lower.Contains))
        {
            return "Production-owned structural evidence.";
        }
        return "Production-owned visual evidence.";
    }

    private static void AddRegions(
        ICollection<DetectorInspectionRegion> target,
        Type owner,
        string member,
        object? value)
    {
        int index = 0;
        foreach ((string Suffix, ScreenRegion Region) item in
                 EnumerateRegions(value))
        {
            string suffix =
                string.IsNullOrWhiteSpace(item.Suffix)
                    ? string.Empty
                    : $".{item.Suffix}";
            string id =
                $"{owner.FullName}.{member}{suffix}";
            string label = FriendlyName(
                $"{member}{(
                    string.IsNullOrWhiteSpace(item.Suffix)
                        ? string.Empty
                        : $" {item.Suffix}")}");
            target.Add(new DetectorInspectionRegion(
                id,
                label,
                item.Region,
                ExpectedEvidence(label)));
            index++;
        }
    }

    private static IEnumerable<(string Suffix, ScreenRegion Region)>
        EnumerateRegions(object? value)
    {
        if (value is ScreenRegion region)
        {
            yield return (string.Empty, region);
            yield break;
        }
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry item in dictionary)
            {
                if (item.Value is ScreenRegion mapped)
                {
                    yield return (
                        Convert.ToString(
                            item.Key,
                            System.Globalization
                                .CultureInfo.InvariantCulture) ??
                        "item",
                        mapped);
                }
            }
            yield break;
        }
        if (value is not IEnumerable sequence ||
            value is string)
        {
            yield break;
        }
        int index = 0;
        foreach (object? item in sequence)
        {
            if (item is ScreenRegion listed)
            {
                yield return (
                    (index + 1).ToString(
                        System.Globalization
                            .CultureInfo.InvariantCulture),
                    listed);
            }
            index++;
        }
    }

    private static object? ReadField(FieldInfo field)
    {
        try
        {
            return field.GetValue(null);
        }
        catch (Exception error) when (
            error is FieldAccessException or
                TargetInvocationException or
                TypeInitializationException)
        {
            return null;
        }
    }

    private static object? ReadProperty(
        PropertyInfo property)
    {
        try
        {
            return property.GetValue(null);
        }
        catch (Exception error) when (
            error is TargetInvocationException or
                MethodAccessException or
                TypeInitializationException)
        {
            return null;
        }
    }

    private static bool LooksLikeGate(string name) =>
        name.Contains(
            "Threshold",
            StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith(
            "Minimum",
            StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith(
            "Required",
            StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith(
            "Maximum",
            StringComparison.OrdinalIgnoreCase);

    private static bool TryNumber(
        object? value,
        out double number)
    {
        if (value is null ||
            value is bool ||
            value is char)
        {
            number = 0;
            return false;
        }
        try
        {
            number = Convert.ToDouble(
                value,
                System.Globalization
                    .CultureInfo.InvariantCulture);
            return double.IsFinite(number);
        }
        catch (Exception error) when (
            error is InvalidCastException or
                FormatException or
                OverflowException)
        {
            number = 0;
            return false;
        }
    }

    private static string[] Tokens(string value) =>
        FriendlyName(value)
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(token =>
                token.Length > 1 &&
                token is not "Score" and
                    not "Fraction" and
                    not "Minimum" and
                    not "Required" and
                    not "Maximum" and
                    not "Region" and
                    not "Control")
            .Select(token =>
                token.ToLowerInvariant())
            .Distinct()
            .ToArray();
}
