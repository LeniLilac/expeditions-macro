using System.Text.Json;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.Tests;

public sealed class DiscordAndWindowsTests
{
    [Theory]
    [InlineData("https://discord.com/api/webhooks/123/token")]
    [InlineData("https://canary.discord.com/api/webhooks/123/token")]
    [InlineData("https://ptb.discord.com/api/webhooks/123/token")]
    [InlineData("https://discordapp.com/api/webhooks/123/token")]
    public void WebhookValidation_AcceptsOfficialDiscordHosts(string url) =>
        Assert.True(DiscordWebhookClient.ValidateWebhookUrl(url));

    [Theory]
    [InlineData("http://discord.com/api/webhooks/123/token")]
    [InlineData("https://discord.example/api/webhooks/123/token")]
    [InlineData("https://evil-discord.com/api/webhooks/123/token")]
    [InlineData("https://discord.com/channels/123/token")]
    public void WebhookValidation_RejectsUnsafeOrMalformedUrls(string url) =>
        Assert.False(DiscordWebhookClient.ValidateWebhookUrl(url));

    [Fact]
    public void ComponentsPayload_UsesDiscordComponentsV2AndRuntimeTotals()
    {
        DiscordNotification notification = new()
        {
            WebhookUrl = "https://canary.discord.com/api/webhooks/123/token",
            Event = "victory",
            Runtime = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(3),
            Victories = 7,
            Defeats = 2,
            MapNumber = 3,
            Difficulty = 2,
            Detail = "Route completed.",
        };

        Dictionary<string, object?> payload = DiscordWebhookClient.BuildComponentsPayload(notification, "victory.png");
        string json = JsonSerializer.Serialize(payload);

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(1 << 15, document.RootElement.GetProperty("flags").GetInt32());
        Assert.Contains("1h 02m 03s", json);
        Assert.Contains("Victories:** 7", json);
        Assert.Contains("Map 3, Difficulty 2", json);
        Assert.Contains("attachment://victory.png", json);
        Assert.Equal(17, document.RootElement.GetProperty("components")[0].GetProperty("type").GetInt32());
    }

    [Fact]
    public void Dpapi_RoundTripsWithoutPersistingPlaintext()
    {
        DpapiSecretProtector protector = new();
        const string webhook = "https://canary.discord.com/api/webhooks/123/a-secret-token";

        string protectedValue = protector.Protect(webhook);

        Assert.NotEqual(webhook, protectedValue);
        Assert.DoesNotContain("a-secret-token", protectedValue, StringComparison.Ordinal);
        Assert.Equal(webhook, protector.Unprotect(protectedValue));
        Assert.Equal(string.Empty, protector.Protect(string.Empty));
    }
}
