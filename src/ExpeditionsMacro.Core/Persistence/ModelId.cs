using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ExpeditionsMacro.Core.Persistence;

public static partial class ModelId
{
    public static string FromName(string name)
    {
        string normalized = name.Trim();
        if (normalized.Length == 0) throw new ArgumentException("Enter a name.", nameof(name));
        string slug = InvalidSlugCharacters().Replace(normalized.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length == 0) slug = "model";
        slug = slug[..Math.Min(slug.Length, 40)];
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..10];
        return $"{slug}-{digest}";
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidSlugCharacters();
}
