using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Automation.Discord;

public sealed class DiscordWebhookClient : IDiscordNotifier, IDisposable
{
    private const int ComponentsV2Flag = 1 << 15;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public DiscordWebhookClient(HttpClient? httpClient = null)
    {
        _ownsClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any()) _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ExpeditionsMacro/1.0");
    }

    public async Task SendAsync(DiscordNotification notification, CancellationToken cancellationToken)
    {
        if (!ValidateWebhookUrl(notification.WebhookUrl)) throw new ArgumentException("The Discord webhook URL is not valid.", nameof(notification));
        string attachmentPrefix = notification.AttachmentPrefix.Equals("challenge", StringComparison.OrdinalIgnoreCase)
            ? "challenge"
            : "expeditions";
        string? filename = notification.Screenshot is null
            ? null
            : $"{attachmentPrefix}_{notification.Event.ToLowerInvariant()}_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.png";
        Dictionary<string, object?> payload = BuildComponentsPayload(notification, filename);
        using HttpRequestMessage request = new(HttpMethod.Post, ComponentsUrl(notification.WebhookUrl));
        if (filename is null || notification.Screenshot is null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }
        else
        {
            MultipartFormDataContent multipart = new($"----ExpeditionsMacro{Guid.NewGuid():N}");
            StringContent payloadContent = new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            multipart.Add(payloadContent, "payload_json");
            ByteArrayContent imageContent = new(ImageCodec.EncodePng(notification.Screenshot));
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            multipart.Add(imageContent, "files[0]", filename);
            request.Content = multipart;
        }
        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Discord returned HTTP {(int)response.StatusCode}: {Redact(detail)}");
        }
    }

    public static bool ValidateWebhookUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        string host = uri.IdnHost.TrimEnd('.');
        bool official = host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".discord.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".discordapp.com", StringComparison.OrdinalIgnoreCase);
        if (!official) return false;
        string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4
            && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && parts[1].Equals("webhooks", StringComparison.OrdinalIgnoreCase)
            && parts[2].Length > 0
            && parts[3].Length > 0;
    }

    public static string FormatRuntime(TimeSpan runtime)
    {
        int total = Math.Max(0, (int)runtime.TotalSeconds);
        int hours = total / 3600;
        int minutes = total % 3600 / 60;
        int seconds = total % 60;
        return hours > 0
            ? $"{hours}h {minutes:00}m {seconds:00}s"
            : minutes > 0
                ? $"{minutes}m {seconds:00}s"
                : $"{seconds}s";
    }

    public static Dictionary<string, object?> BuildComponentsPayload(DiscordNotification notification, string? filename)
    {
        string eventName = notification.Event.ToLowerInvariant();
        string title = eventName switch
        {
            "started" => "Started",
            "attempt" => "Challenge started",
            "victory" => "Victory",
            "defeat" => "Defeat",
            "recovery" => "Rejoin needed",
            "waiting" => "Waiting",
            _ => throw new ArgumentException($"Unsupported Discord event '{notification.Event}'."),
        };
        string route = string.IsNullOrWhiteSpace(notification.Route)
            ? $"Map {notification.MapNumber}, Difficulty {notification.Difficulty}"
            : notification.Route;
        List<object> components =
        [
            new Dictionary<string, object?> { ["type"] = 10, ["content"] = $"## {notification.MacroName}: {title}\n{notification.Detail}" },
            new Dictionary<string, object?> { ["type"] = 14, ["divider"] = true, ["spacing"] = 1 },
            new Dictionary<string, object?>
            {
                ["type"] = 10,
                ["content"] = $"**Runtime:** {FormatRuntime(notification.Runtime)}\n**Victories:** {notification.Victories}    **Defeats:** {notification.Defeats}\n**Route:** {route}",
            },
        ];
        if (filename is not null)
        {
            components.Add(new Dictionary<string, object?>
            {
                ["type"] = 12,
                ["items"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["media"] = new Dictionary<string, object?> { ["url"] = $"attachment://{filename}" },
                        ["description"] = $"Roblox {title.ToLowerInvariant()} screen",
                    },
                },
            });
        }
        components.Add(new Dictionary<string, object?>
        {
            ["type"] = 10,
            ["content"] = $"-# Captured {DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}",
        });
        Dictionary<string, object?> payload = new()
        {
            ["flags"] = ComponentsV2Flag,
            ["allowed_mentions"] = new Dictionary<string, object?> { ["parse"] = Array.Empty<string>() },
            ["components"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = 17,
                    ["components"] = components,
                },
            },
        };
        if (filename is not null)
        {
            payload["attachments"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 0,
                    ["filename"] = filename,
                    ["description"] = $"Roblox {title.ToLowerInvariant()} screen",
                },
            };
        }
        return payload;
    }

    public void Dispose()
    {
        if (_ownsClient) _httpClient.Dispose();
    }

    private static Uri ComponentsUrl(string value)
    {
        UriBuilder builder = new(value);
        Dictionary<string, string> query = builder.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(part => Uri.UnescapeDataString(part[0]), part => part.Length > 1 ? Uri.UnescapeDataString(part[1]) : string.Empty, StringComparer.OrdinalIgnoreCase);
        query["wait"] = "true";
        query["with_components"] = "true";
        builder.Query = string.Join('&', query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return builder.Uri;
    }

    private static string Redact(string value) => value.Length <= 500 ? value : value[..500];
}
