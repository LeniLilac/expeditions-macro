using System.Text.Json;

namespace ExpeditionsMacro.Automation.Updates;

public static class ApplicationUpdateReleaseParser
{
    private const string Repository =
        "LeniLilac/expeditions-macro";

    public static ApplicationUpdateRelease? ParseLatest(
        ReadOnlySpan<byte> json,
        ApplicationSemanticVersion current)
    {
        ArgumentNullException.ThrowIfNull(current);
        using JsonDocument document =
            JsonDocument.Parse(json.ToArray());
        if (document.RootElement.ValueKind !=
            JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "GitHub returned an invalid release list.");
        }

        List<ApplicationUpdateRelease> candidates = [];
        foreach (JsonElement release in
                 document.RootElement.EnumerateArray())
        {
            ApplicationUpdateRelease? candidate =
                ParseRelease(release);
            if (candidate is null ||
                candidate.Version.CompareTo(current) <= 0 ||
                (!current.IsPrerelease &&
                 candidate.IsPrerelease))
            {
                continue;
            }
            candidates.Add(candidate);
        }

        return candidates
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefault();
    }

    private static ApplicationUpdateRelease? ParseRelease(
        JsonElement release)
    {
        if (release.ValueKind != JsonValueKind.Object ||
            !TryReadBoolean(
                release,
                "draft",
                out bool draft) ||
            draft ||
            !TryReadBoolean(
                release,
                "prerelease",
                out bool prerelease) ||
            !TryReadString(
                release,
                "tag_name",
                out string tag) ||
            !tag.StartsWith('v') ||
            !ApplicationSemanticVersion.TryParse(
                tag[1..],
                out ApplicationSemanticVersion? version))
        {
            return null;
        }
        ApplicationSemanticVersion parsedVersion = version!;
        if (prerelease != parsedVersion.IsPrerelease ||
            !TryReadString(
                release,
                "html_url",
                out string releaseUrl) ||
            !TryExactReleaseUri(
                releaseUrl,
                parsedVersion,
                out Uri? releaseUri))
        {
            return null;
        }

        string versionText = parsedVersion.ToString();
        string installerName =
            $"ExpeditionsMacro-{versionText}-win-x64-setup.exe";
        string zipName =
            $"ExpeditionsMacro-{versionText}-win-x64.zip";
        string[] expectedAssets =
        [
            installerName,
            zipName,
            "dependencies.json",
            "SHA256SUMS.txt",
        ];
        if (!release.TryGetProperty(
                "assets",
                out JsonElement assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        Dictionary<string, ApplicationUpdateAsset> parsed =
            new(StringComparer.Ordinal);
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            ApplicationUpdateAsset? value =
                ParseAsset(asset, parsedVersion);
            if (value is null ||
                !parsed.TryAdd(value.Name, value))
            {
                return null;
            }
        }

        if (parsed.Count != expectedAssets.Length ||
            expectedAssets.Any(name =>
                !parsed.ContainsKey(name)))
        {
            return null;
        }

        string displayName =
            TryReadString(release, "name", out string name) &&
            !string.IsNullOrWhiteSpace(name)
                ? name.Trim()
                : $"Expeditions Macro {tag}";
        return new ApplicationUpdateRelease(
            parsedVersion,
            prerelease,
            displayName,
            releaseUri!,
            parsed[installerName],
            parsed["SHA256SUMS.txt"]);
    }

    private static ApplicationUpdateAsset? ParseAsset(
        JsonElement asset,
        ApplicationSemanticVersion version)
    {
        if (!TryReadString(asset, "name", out string name) ||
            !asset.TryGetProperty("size", out JsonElement sizeValue) ||
            !sizeValue.TryGetInt64(out long size) ||
            size <= 0 ||
            !TryReadString(asset, "digest", out string digest) ||
            !TrySha256(digest, out string sha256) ||
            !TryReadString(
                asset,
                "browser_download_url",
                out string downloadUrl))
        {
            return null;
        }

        string expected =
            $"https://github.com/{Repository}/releases/download/v{version}/{Uri.EscapeDataString(name)}";
        if (!string.Equals(
                downloadUrl,
                expected,
                StringComparison.Ordinal) ||
            !Uri.TryCreate(
                downloadUrl,
                UriKind.Absolute,
                out Uri? downloadUri))
        {
            return null;
        }

        return new ApplicationUpdateAsset(
            name,
            size,
            sha256,
            downloadUri);
    }

    private static bool TryExactReleaseUri(
        string value,
        ApplicationSemanticVersion version,
        out Uri? uri)
    {
        uri = null;
        string expected =
            $"https://github.com/{Repository}/releases/tag/v{version}";
        Uri? parsed = null;
        bool valid = string.Equals(
            value,
            expected,
            StringComparison.Ordinal) &&
            Uri.TryCreate(value, UriKind.Absolute, out parsed);
        uri = valid ? parsed : null;
        return valid;
    }

    private static bool TrySha256(
        string value,
        out string sha256)
    {
        const string prefix = "sha256:";
        sha256 = value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].ToLowerInvariant()
            : string.Empty;
        return sha256.Length == 64 &&
            sha256.All(character =>
                char.IsAsciiHexDigit(character));
    }

    private static bool TryReadBoolean(
        JsonElement element,
        string name,
        out bool result)
    {
        result = false;
        if (!element.TryGetProperty(
                name,
                out JsonElement value) ||
            value.ValueKind is not (
                JsonValueKind.True or
                JsonValueKind.False))
        {
            return false;
        }
        result = value.GetBoolean();
        return true;
    }

    private static bool TryReadString(
        JsonElement element,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }
}
