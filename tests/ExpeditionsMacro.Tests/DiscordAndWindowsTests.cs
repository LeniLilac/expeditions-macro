using System.Diagnostics;
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
    public void ReleaseAnnouncementScript_UsesComponentsV2RolePingAndNoAccentColor()
    {
        string script = Path.Combine(TestPaths.RepositoryRoot, "scripts", "Send-DiscordReleaseAnnouncement.ps1");
        string notes = Path.Combine(TestPaths.RepositoryRoot, "docs", "release-notes", "1.0.8.md");
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
}
