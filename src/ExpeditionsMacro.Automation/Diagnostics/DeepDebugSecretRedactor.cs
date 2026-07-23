using System.Text.RegularExpressions;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Diagnostics;

internal static class DeepDebugSecretRedactor
{
    private const string RedactedWindowsUser = "[REDACTED WINDOWS USER]";

    private static readonly Regex DiscordWebhookPattern = new(
        "https://(?:[a-z0-9-]+\\.)?(?:discord(?:app)?\\.com)/api(?:/v\\d+)?/webhooks/[^\\s\\\"'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex RobloxPrivateServerPattern = new(
        "(?:https://(?:[a-z0-9-]+\\.)?roblox\\.com/[^\\s\\\"'<>]*(?:privateServerLinkCode|linkCode|[?&]code=)[^\\s\\\"'<>]*|roblox://[^\\s\\\"'<>]+)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex WindowsProfilePathPattern = new(
        "(?<prefix>[a-z]:[\\\\/]+(?:users|documents and settings)[\\\\/]+)(?<user>[^\\\\/\\s\\\"'<>]+)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex? WindowsUserNamePattern =
        CreateWindowsUserNamePattern();

    public static string Redact(string text, AppSettings settings)
    {
        string redacted = DiscordWebhookPattern.Replace(
            text,
            "[REDACTED DISCORD WEBHOOK]");
        redacted = RobloxPrivateServerPattern.Replace(
            redacted,
            "[REDACTED ROBLOX PRIVATE SERVER LINK]");
        redacted = ReplaceSecret(
            redacted,
            settings.DiscordErrorUserId,
            "[REDACTED DISCORD USER ID]");
        redacted = ReplaceSecret(
            redacted,
            settings.EncryptedWebhook,
            "[REDACTED PROTECTED WEBHOOK]");
        redacted = ReplaceSecret(
            redacted,
            settings.EncryptedPrivateServerLink,
            "[REDACTED PROTECTED ROBLOX PRIVATE SERVER LINK]");
        redacted = WindowsProfilePathPattern.Replace(
            redacted,
            match => $"{match.Groups["prefix"].Value}{RedactedWindowsUser}");
        return WindowsUserNamePattern?.Replace(
            redacted,
            RedactedWindowsUser) ?? redacted;
    }

    private static string ReplaceSecret(
        string text,
        string secret,
        string replacement) =>
        string.IsNullOrWhiteSpace(secret)
            ? text
            : text.Replace(secret, replacement, StringComparison.Ordinal);

    private static Regex? CreateWindowsUserNamePattern()
    {
        string userName = Environment.UserName;
        if (string.IsNullOrWhiteSpace(userName)) return null;
        return new Regex(
            $"(?<![a-z0-9_.-]){Regex.Escape(userName)}(?![a-z0-9_.-])",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));
    }
}
