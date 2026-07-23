using System.Text.RegularExpressions;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Diagnostics;

internal static class DeepDebugSecretRedactor
{
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
        return ReplaceSecret(
            redacted,
            settings.EncryptedPrivateServerLink,
            "[REDACTED PROTECTED ROBLOX PRIVATE SERVER LINK]");
    }

    private static string ReplaceSecret(
        string text,
        string secret,
        string replacement) =>
        string.IsNullOrWhiteSpace(secret)
            ? text
            : text.Replace(secret, replacement, StringComparison.Ordinal);
}
