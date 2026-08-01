namespace ExpeditionsMacro.Automation.Updates;

public sealed record ApplicationSemanticVersion :
    IComparable<ApplicationSemanticVersion>
{
    private ApplicationSemanticVersion(
        int major,
        int minor,
        int patch,
        IReadOnlyList<string> prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public IReadOnlyList<string> Prerelease { get; }

    public bool IsPrerelease => Prerelease.Count > 0;

    public static ApplicationSemanticVersion Parse(
        string value) =>
        TryParse(value, out ApplicationSemanticVersion? version)
            ? version!
            : throw new FormatException(
                $"'{value}' is not a supported semantic version.");

    public static bool TryParse(
        string? value,
        out ApplicationSemanticVersion? version)
    {
        version = null;
        string candidate = value?.Trim() ?? string.Empty;
        int buildIndex = candidate.IndexOf('+');
        if (buildIndex >= 0)
        {
            candidate = candidate[..buildIndex];
        }

        string core = candidate;
        string? prereleaseText = null;
        int prereleaseIndex = candidate.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            core = candidate[..prereleaseIndex];
            prereleaseText = candidate[(prereleaseIndex + 1)..];
        }

        string[] coreParts = core.Split('.');
        if (coreParts.Length != 3 ||
            !TryParseNumeric(coreParts[0], out int major) ||
            !TryParseNumeric(coreParts[1], out int minor) ||
            !TryParseNumeric(coreParts[2], out int patch))
        {
            return false;
        }

        string[] prerelease = [];
        if (prereleaseText is not null)
        {
            prerelease = prereleaseText.Split('.');
            if (prerelease.Length == 0 ||
                prerelease.Any(identifier =>
                    !IsValidPrereleaseIdentifier(identifier)))
            {
                return false;
            }
        }

        version = new ApplicationSemanticVersion(
            major,
            minor,
            patch,
            prerelease);
        return true;
    }

    public int CompareTo(ApplicationSemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int core = Major.CompareTo(other.Major);
        if (core == 0)
        {
            core = Minor.CompareTo(other.Minor);
        }
        if (core == 0)
        {
            core = Patch.CompareTo(other.Patch);
        }
        if (core != 0)
        {
            return core;
        }

        if (!IsPrerelease || !other.IsPrerelease)
        {
            return IsPrerelease == other.IsPrerelease
                ? 0
                : IsPrerelease
                    ? -1
                    : 1;
        }

        int length = Math.Min(
            Prerelease.Count,
            other.Prerelease.Count);
        for (int index = 0; index < length; index++)
        {
            int identifier = CompareIdentifier(
                Prerelease[index],
                other.Prerelease[index]);
            if (identifier != 0)
            {
                return identifier;
            }
        }

        return Prerelease.Count.CompareTo(
            other.Prerelease.Count);
    }

    public override string ToString()
    {
        string version = $"{Major}.{Minor}.{Patch}";
        return IsPrerelease
            ? $"{version}-{string.Join('.', Prerelease)}"
            : version;
    }

    private static int CompareIdentifier(
        string left,
        string right)
    {
        bool leftNumeric = left.All(char.IsAsciiDigit);
        bool rightNumeric = right.All(char.IsAsciiDigit);
        if (leftNumeric && rightNumeric)
        {
            int length = left.Length.CompareTo(right.Length);
            return length != 0
                ? length
                : string.CompareOrdinal(left, right);
        }
        if (leftNumeric != rightNumeric)
        {
            return leftNumeric ? -1 : 1;
        }
        return string.CompareOrdinal(left, right);
    }

    private static bool TryParseNumeric(
        string value,
        out int number)
    {
        number = 0;
        return value.Length > 0 &&
            (value.Length == 1 || value[0] != '0') &&
            value.All(char.IsAsciiDigit) &&
            int.TryParse(value, out number);
    }

    private static bool IsValidPrereleaseIdentifier(
        string value)
    {
        if (value.Length == 0 ||
            value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character == '-')))
        {
            return false;
        }

        return !value.All(char.IsAsciiDigit) ||
            value.Length == 1 ||
            value[0] != '0';
    }
}
