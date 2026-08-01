using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Automation.Updates;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class ApplicationUpdateSessionTests
{
    [Fact]
    public async Task AutomaticFailure_IsNonDisruptiveAndRetryable()
    {
        using DelegateHandler handler = new(_ =>
            throw new HttpRequestException(
                "simulated network failure"));
        using HttpClient client = new(handler);
        using ApplicationUpdateSession session = new(
            new ApplicationUpdateService(
                new AppPaths(NewRoot()),
                "1.3.0-beta.53",
                client));

        await session.InitializeAsync(autoCheck: true);

        Assert.Equal(
            ApplicationUpdatePhase.Error,
            session.State.Phase);
        Assert.Contains(
            "Use Check now to retry",
            session.State.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "simulated network failure",
            session.State.Message,
            StringComparison.Ordinal);

        await session.CheckAsync();

        Assert.Equal(
            "GitHub could not be reached or returned an unsuccessful response.",
            session.State.Message);
        Assert.DoesNotContain(
            "simulated network failure",
            session.State.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckDownloadAndRestart_RecoversReadyInstaller()
    {
        byte[] installer =
            Encoding.UTF8.GetBytes("installer content");
        string installerName =
            "ExpeditionsMacro-1.3.0-beta.54-win-x64-setup.exe";
        string installerHash = Sha256(installer);
        byte[] checksums = Encoding.UTF8.GetBytes(
            $"{installerHash}  {installerName}\r\n");
        string checksumHash = Sha256(checksums);
        byte[] metadata = Metadata(
            installerName,
            installer.LongLength,
            installerHash,
            checksums.LongLength,
            checksumHash);
        using DelegateHandler handler = new(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            byte[] bytes = path.EndsWith(
                    "SHA256SUMS.txt",
                    StringComparison.Ordinal)
                ? checksums
                : path.EndsWith(
                    installerName,
                    StringComparison.Ordinal)
                    ? installer
                    : metadata;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes),
            };
        });
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            using (ApplicationUpdateSession first = new(
                       new ApplicationUpdateService(
                           paths,
                           "1.3.0-beta.53",
                           client)))
            {
                await first.CheckAsync();
                Assert.Equal(
                    ApplicationUpdatePhase.Available,
                    first.State.Phase);
                await first.DownloadAsync();
                Assert.Equal(
                    ApplicationUpdatePhase.Ready,
                    first.State.Phase);
            }

            using ApplicationUpdateSession restarted = new(
                new ApplicationUpdateService(
                    paths,
                    "1.3.0-beta.53",
                    client));
            await restarted.InitializeAsync(autoCheck: false);

            Assert.Equal(
                ApplicationUpdatePhase.Ready,
                restarted.State.Phase);
            Assert.True(File.Exists(
                await restarted.VerifyReadyInstallerAsync()));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static byte[] Metadata(
        string installerName,
        long installerSize,
        string installerHash,
        long checksumSize,
        string checksumHash)
    {
        const string version = "1.3.0-beta.54";
        object Asset(string name, long size, string hash) => new
        {
            name,
            size,
            digest = $"sha256:{hash}",
            browser_download_url =
                $"https://github.com/LeniLilac/expeditions-macro/releases/download/v{version}/{name}",
        };
        return JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            new
            {
                draft = false,
                prerelease = true,
                tag_name = $"v{version}",
                name = $"Expeditions Macro v{version}",
                html_url =
                    $"https://github.com/LeniLilac/expeditions-macro/releases/tag/v{version}",
                assets = new[]
                {
                    Asset(
                        installerName,
                        installerSize,
                        installerHash),
                    Asset(
                        $"ExpeditionsMacro-{version}-win-x64.zip",
                        1,
                        new string('a', 64)),
                    Asset(
                        "dependencies.json",
                        1,
                        new string('b', 64)),
                    Asset(
                        "SHA256SUMS.txt",
                        checksumSize,
                        checksumHash),
                },
            },
        });
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();

    private static string NewRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            $"expeditions-update-session-{Guid.NewGuid():N}");

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, HttpResponseMessage> response) :
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
