using System.Diagnostics;
using System.Net;
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

    [Theory]
    [InlineData("")]
    [InlineData("1528248119425368074")]
    [InlineData("12345678901234567")]
    public void DiscordUserIdValidation_AcceptsBlankOrNumericSnowflakes(string value) =>
        Assert.True(DiscordWebhookClient.ValidateDiscordUserId(value));

    [Theory]
    [InlineData("123")]
    [InlineData("not-a-user")]
    [InlineData("<@1528248119425368074>")]
    [InlineData("123456789012345678901")]
    public void DiscordUserIdValidation_RejectsMalformedValues(string value) =>
        Assert.False(DiscordWebhookClient.ValidateDiscordUserId(value));

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
        Assert.Empty(document.RootElement.GetProperty("allowed_mentions").GetProperty("parse").EnumerateArray());
        JsonElement container = document.RootElement.GetProperty("components")[0];
        Assert.Equal(17, container.GetProperty("type").GetInt32());
        Assert.False(container.TryGetProperty("accent_color", out _));
    }

    [Fact]
    public void ChallengeComponentsPayload_UsesChallengeContextForAdditionalEvents()
    {
        DiscordNotification notification = new()
        {
            WebhookUrl = "https://discord.com/api/webhooks/123/token",
            Event = "attempt",
            Runtime = TimeSpan.FromMinutes(4),
            Victories = 2,
            Defeats = 1,
            MapNumber = 4,
            Difficulty = 0,
            Detail = "Starting the selected Challenge.",
            MacroName = "Challenge Macro",
            Route = "Sprite · Fairy King Forest",
            AttachmentPrefix = "challenge",
        };

        string json = JsonSerializer.Serialize(DiscordWebhookClient.BuildComponentsPayload(notification, null));
        using JsonDocument document = JsonDocument.Parse(json);
        string content = string.Join(
            '\n',
            document.RootElement.GetProperty("components")[0].GetProperty("components")
                .EnumerateArray()
                .Where(component => component.TryGetProperty("content", out _))
                .Select(component => component.GetProperty("content").GetString()));

        Assert.Contains("Challenge Macro: Challenge started", content);
        Assert.Contains("Sprite · Fairy King Forest", content);
        Assert.DoesNotContain("Difficulty 0", content);
    }

    [Fact]
    public void SkippedComponentsPayload_ReportsRecoverableTaskSkip()
    {
        DiscordNotification notification = new()
        {
            WebhookUrl = "https://discord.com/api/webhooks/123/token",
            Event = "skipped",
            Runtime = TimeSpan.FromMinutes(3),
            Victories = 0,
            Defeats = 0,
            MapNumber = 2,
            Difficulty = 3,
            Detail = "Camera alignment stayed below its required confidence after three attempts.",
            MacroName = "Challenge Macro",
            Route = "Trait · Flower Forest",
        };

        string json = JsonSerializer.Serialize(DiscordWebhookClient.BuildComponentsPayload(notification, null));

        Assert.Contains("Challenge Macro: Task skipped", json, StringComparison.Ordinal);
        Assert.Contains("after three attempts", json, StringComparison.Ordinal);
        Assert.Contains("Flower Forest", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorPingPayload_AllowsOnlyTheConfiguredUserAndHasNoAccent()
    {
        const string userId = "1528248119425368074";
        Dictionary<string, object?> payload = DiscordWebhookClient.BuildErrorPingPayload(
            userId,
            "Challenge Macro",
            "Timed out waiting for Prestart. @everyone",
            alertIndex: 3);
        string json = JsonSerializer.Serialize(payload);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(1 << 15, document.RootElement.GetProperty("flags").GetInt32());
        JsonElement mentions = document.RootElement.GetProperty("allowed_mentions");
        Assert.Empty(mentions.GetProperty("parse").EnumerateArray());
        Assert.Equal(userId, mentions.GetProperty("users")[0].GetString());
        JsonElement container = document.RootElement.GetProperty("components")[0];
        Assert.Equal(17, container.GetProperty("type").GetInt32());
        Assert.False(container.TryGetProperty("accent_color", out _));
        string content = container.GetProperty("components")[0].GetProperty("content").GetString()!;
        Assert.Contains($"<@{userId}>", content, StringComparison.Ordinal);
        Assert.Contains("Error alert 3 of 5", content, StringComparison.Ordinal);
        Assert.DoesNotContain("@everyone", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorPings_SendFiveSeparateComponentsV2Messages()
    {
        RecordingHandler handler = new();
        using HttpClient http = new(handler);
        using DiscordWebhookClient client = new(http);

        await client.SendErrorPingsAsync(
            "https://discord.com/api/webhooks/123/test-token",
            "1528248119425368074",
            "Expeditions Macro",
            "Timed out waiting for Prestart.",
            CancellationToken.None);

        Assert.Equal(DiscordWebhookClient.ErrorPingCount, handler.Payloads.Count);
        Assert.All(handler.RequestUris, uri => Assert.Contains("with_components=true", uri.Query, StringComparison.OrdinalIgnoreCase));
        for (int index = 0; index < handler.Payloads.Count; index++)
        {
            Assert.Contains($"Error alert {index + 1} of {DiscordWebhookClient.ErrorPingCount}", handler.Payloads[index], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task WebhookTest_SendsOneRestrictedComponentsV2Message()
    {
        RecordingHandler handler = new();
        using HttpClient http = new(handler);
        using DiscordWebhookClient client = new(http);

        await client.SendTestAsync(
            "https://canary.discord.com/api/webhooks/123/test-token",
            CancellationToken.None);

        string payload = Assert.Single(handler.Payloads);
        Uri uri = Assert.Single(handler.RequestUris);
        Assert.Contains("with_components=true", uri.Query, StringComparison.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(payload);
        Assert.Equal(1 << 15, document.RootElement.GetProperty("flags").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("allowed_mentions").GetProperty("parse").EnumerateArray());
        JsonElement container = document.RootElement.GetProperty("components")[0];
        Assert.Equal(17, container.GetProperty("type").GetInt32());
        Assert.False(container.TryGetProperty("accent_color", out _));
        Assert.Contains("Webhook test", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseAnnouncementScript_UsesComponentsV2RolePingAndNoAccentColor()
    {
        string script = Path.Combine(TestPaths.RepositoryRoot, "scripts", "Send-DiscordReleaseAnnouncement.ps1");
        // 1.1.1 intentionally uses section names such as "Fixed" and "Setup guide"
        // rather than the older "Changes" heading. This guards the release bot
        // against silently dropping notes whenever release-note headings evolve.
        string notes = Path.Combine(TestPaths.RepositoryRoot, "docs", "release-notes", "1.1.1.md");
        ProcessStartInfo start = new(OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        if (OperatingSystem.IsWindows())
        {
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
        }
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-WebhookUrl");
        start.ArgumentList.Add("https://discord.com/api/webhooks/123/test-token");
        start.ArgumentList.Add("-Repository");
        start.ArgumentList.Add("LeniLilac/expeditions-macro");
        start.ArgumentList.Add("-Tag");
        start.ArgumentList.Add("v9.8.7");
        start.ArgumentList.Add("-RoleId");
        start.ArgumentList.Add("1528250880304873643");
        start.ArgumentList.Add("-ReleaseNotesPath");
        start.ArgumentList.Add(notes);
        start.ArgumentList.Add("-DryRun");

        using Process process = Assert.IsType<Process>(Process.Start(start));
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, error);
        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        Assert.Equal(1 << 15, root.GetProperty("flags").GetInt32());
        Assert.False(root.TryGetProperty("content", out _));
        Assert.False(root.TryGetProperty("embeds", out _));
        Assert.Empty(root.GetProperty("allowed_mentions").GetProperty("parse").EnumerateArray());
        Assert.Equal("1528250880304873643", root.GetProperty("allowed_mentions").GetProperty("roles")[0].GetString());

        JsonElement container = root.GetProperty("components")[0];
        Assert.Equal(17, container.GetProperty("type").GetInt32());
        Assert.False(container.TryGetProperty("accent_color", out _));
        string componentText = string.Join(
            '\n',
            container.GetProperty("components")
                .EnumerateArray()
                .Where(component => component.TryGetProperty("content", out _))
                .Select(component => component.GetProperty("content").GetString()));
        Assert.Contains("<@&1528250880304873643>", componentText, StringComparison.Ordinal);
        Assert.Contains("### Highlights", componentText, StringComparison.Ordinal);
        Assert.Contains("Challenge mode can now start the configured Expeditions preset", componentText, StringComparison.Ordinal);
        Assert.Contains("releases/tag/v9.8.7", componentText, StringComparison.Ordinal);
        Assert.Contains("ExpeditionsMacro-9.8.7-win-x64-setup.exe", componentText, StringComparison.Ordinal);
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

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];

        public List<Uri> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(Assert.IsType<Uri>(request.RequestUri));
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            };
        }
    }
}
