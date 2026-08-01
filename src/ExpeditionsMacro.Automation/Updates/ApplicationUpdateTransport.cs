using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace ExpeditionsMacro.Automation.Updates;

internal sealed class ApplicationUpdateTransport : IDisposable
{
    private const int MaximumRedirects = 5;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly string _version;

    public ApplicationUpdateTransport(
        string version,
        HttpClient? client)
    {
        _version = version;
        _ownsClient = client is null;
        _client = client ?? CreateClient();
    }

    public async Task<byte[]> DownloadBytesAsync(
        Uri uri,
        int maximumBytes,
        long? expectedSize,
        string? expectedSha256,
        bool metadataRequest,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await SendTrustedAsync(
                uri,
                metadataRequest,
                cancellationToken).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength is long length &&
            length > maximumBytes)
        {
            throw new InvalidDataException(
                "The update response exceeded its safe size limit.");
        }

        await using Stream stream =
            await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
        using MemoryStream output = new();
        byte[] buffer = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(
                   buffer,
                   cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (output.Length + read > maximumBytes)
            {
                throw new InvalidDataException(
                    "The update response exceeded its safe size limit.");
            }
            output.Write(buffer, 0, read);
        }
        byte[] bytes = output.ToArray();
        if (expectedSize is long size &&
            bytes.LongLength != size)
        {
            throw new InvalidDataException(
                "The update response did not match its declared size.");
        }
        if (expectedSha256 is not null)
        {
            string hash = Convert.ToHexString(
                SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(
                    hash,
                    expectedSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The update response did not match its release hash.");
            }
        }
        return bytes;
    }

    public async Task DownloadFileAsync(
        ApplicationUpdateAsset asset,
        string path,
        long maximumBytes,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (asset.Size <= 0 || asset.Size > maximumBytes)
        {
            throw new InvalidDataException(
                "The installer exceeds the safe download limit.");
        }
        using HttpResponseMessage response =
            await SendTrustedAsync(
                asset.DownloadUri,
                metadataRequest: false,
                cancellationToken).ConfigureAwait(false);
        await using Stream input =
            await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
        await using FileStream output = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);
        using IncrementalHash hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(
                   buffer,
                   cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > asset.Size)
            {
                throw new InvalidDataException(
                    "The installer exceeded its declared size.");
            }
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken).ConfigureAwait(false);
            progress?.Report((double)total / asset.Size);
        }
        await output.FlushAsync(cancellationToken)
            .ConfigureAwait(false);
        string actualHash = Convert.ToHexString(
            hash.GetHashAndReset()).ToLowerInvariant();
        if (total != asset.Size ||
            !string.Equals(
                actualHash,
                asset.Sha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The downloaded installer did not match its release size and hash.");
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    private async Task<HttpResponseMessage> SendTrustedAsync(
        Uri initialUri,
        bool metadataRequest,
        CancellationToken cancellationToken)
    {
        Uri current = initialUri;
        for (int redirect = 0; redirect <= MaximumRedirects; redirect++)
        {
            ValidateRequestUri(
                current,
                metadataRequest,
                redirect == 0);
            using HttpRequestMessage request =
                new(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd(
                $"ExpeditionsMacro/{_version}");
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    metadataRequest
                        ? "application/vnd.github+json"
                        : "application/octet-stream"));
            if (metadataRequest)
            {
                request.Headers.Add(
                    "X-GitHub-Api-Version",
                    "2026-03-10");
            }
            HttpResponseMessage response =
                await _client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
            if (!IsRedirect(response.StatusCode))
            {
                response.EnsureSuccessStatusCode();
                return response;
            }

            Uri? location = response.Headers.Location;
            response.Dispose();
            if (redirect == MaximumRedirects ||
                location is null ||
                !location.IsAbsoluteUri)
            {
                throw new HttpRequestException(
                    "GitHub returned an invalid update redirect.");
            }
            current = location;
        }
        throw new HttpRequestException(
            "GitHub returned too many update redirects.");
    }

    private static void ValidateRequestUri(
        Uri uri,
        bool metadata,
        bool initial)
    {
        bool trustedHost = metadata
            ? string.Equals(
                uri.Host,
                "api.github.com",
                StringComparison.OrdinalIgnoreCase)
            : string.Equals(
                  uri.Host,
                  "github.com",
                  StringComparison.OrdinalIgnoreCase) ||
              (!initial && string.Equals(
                  uri.Host,
                  "release-assets.githubusercontent.com",
                  StringComparison.OrdinalIgnoreCase));
        if (!trustedHost ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new HttpRequestException(
                "The update request left the trusted GitHub hosts.");
        }
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    private static HttpClient CreateClient()
    {
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression =
                DecompressionMethods.All,
        };
        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }
}
