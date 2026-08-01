using System.Net;
using System.Security.Cryptography;
using System.Text;
using ExpeditionsMacro.Automation.Updates;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class ApplicationUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_UsesVersionedUnauthenticatedGitHubApi()
    {
        RequestRecord? observed = null;
        using RoutingHandler handler = new(request =>
        {
            observed = RequestRecord.From(request);
            return Bytes(HttpStatusCode.OK, "[]"u8.ToArray());
        });
        using HttpClient client = new(handler);
        using ApplicationUpdateService service = new(
            new AppPaths(NewRoot()),
            "1.3.0-beta.53",
            client);

        ApplicationUpdateRelease? result =
            await service.CheckAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.NotNull(observed);
        Assert.Equal(
            "https://api.github.com/repos/LeniLilac/expeditions-macro/releases?per_page=50",
            observed.Uri);
        Assert.Equal(
            "2026-03-10",
            observed.ApiVersion);
        Assert.Contains(
            "ExpeditionsMacro/1.3.0-beta.53",
            observed.UserAgent,
            StringComparison.Ordinal);
        Assert.Null(observed.Authorization);
    }

    [Fact]
    public async Task DownloadInstallerAsync_VerifiesBothDigestsAndRecoversStage()
    {
        byte[] installer =
            Encoding.UTF8.GetBytes("verified installer bytes");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        byte[] checksums = ChecksumBytes(release.Installer);
        release = release with
        {
            Checksums = Asset(
                "SHA256SUMS.txt",
                checksums),
        };
        using RoutingHandler handler = new(request =>
            RouteAssets(request, release, installer, checksums));
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            using ApplicationUpdateService service = new(
                paths,
                "1.3.0-beta.53",
                client);

            string path = await service.DownloadInstallerAsync(
                release,
                progress: null,
                CancellationToken.None);
            (StagedApplicationUpdate Stage, string Path)? recovered =
                await service.RecoverStagedAsync(
                    CancellationToken.None);

            Assert.True(File.Exists(path));
            Assert.NotNull(recovered);
            Assert.Equal(path, recovered.Value.Path);
            Assert.Equal(
                "1.3.0-beta.54",
                recovered.Value.Stage.Version);
            Assert.Equal(
                release.Installer.Sha256,
                recovered.Value.Stage.InstallerSha256);
            Assert.False(File.Exists(path + ".partial"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task RecoverStagedAsync_DeletesTamperedInstaller()
    {
        byte[] installer =
            Encoding.UTF8.GetBytes("verified installer bytes");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        byte[] checksums = ChecksumBytes(release.Installer);
        release = release with
        {
            Checksums = Asset(
                "SHA256SUMS.txt",
                checksums),
        };
        using RoutingHandler handler = new(request =>
            RouteAssets(request, release, installer, checksums));
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            using ApplicationUpdateService service = new(
                paths,
                "1.3.0-beta.53",
                client);
            string path = await service.DownloadInstallerAsync(
                release,
                progress: null,
                CancellationToken.None);
            await File.AppendAllTextAsync(path, "tampered");

            (StagedApplicationUpdate Stage, string Path)? recovered =
                await service.RecoverStagedAsync(
                    CancellationToken.None);

            Assert.Null(recovered);
            Assert.False(File.Exists(path));
            Assert.False(File.Exists(paths.UpdateStageFile));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task RecoverStagedAsync_RemovesAlreadyInstalledVersion()
    {
        byte[] installer = Encoding.UTF8.GetBytes("installer");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        byte[] checksums = ChecksumBytes(release.Installer);
        release = release with
        {
            Checksums = Asset("SHA256SUMS.txt", checksums),
        };
        using RoutingHandler handler = new(request =>
            RouteAssets(request, release, installer, checksums));
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            using (ApplicationUpdateService oldVersion = new(
                       paths,
                       "1.3.0-beta.53",
                       client))
            {
                await oldVersion.DownloadInstallerAsync(
                    release,
                    progress: null,
                    CancellationToken.None);
            }
            using ApplicationUpdateService installed = new(
                paths,
                "1.3.0-beta.54",
                client);

            Assert.Null(await installed.RecoverStagedAsync(
                CancellationToken.None));
            Assert.False(File.Exists(paths.UpdateStageFile));
            Assert.False(File.Exists(Path.Combine(
                paths.Updates,
                release.Installer.Name)));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_RejectsUntrustedRedirect()
    {
        byte[] installer = Encoding.UTF8.GetBytes("installer");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        byte[] checksums = ChecksumBytes(release.Installer);
        release = release with
        {
            Checksums = Asset("SHA256SUMS.txt", checksums),
        };
        using RoutingHandler handler = new(_ => new HttpResponseMessage(
            HttpStatusCode.Redirect)
        {
            Headers =
            {
                Location = new Uri(
                    "https://evil.example/update.exe"),
            },
        });
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            using ApplicationUpdateService service = new(
                new AppPaths(root),
                "1.3.0-beta.53",
                client);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                service.DownloadInstallerAsync(
                    release,
                    progress: null,
                    CancellationToken.None));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_HashMismatchLeavesNoStage()
    {
        byte[] installer = Encoding.UTF8.GetBytes("installer");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        string wrongHash = new('b', 64);
        release = release with
        {
            Installer = release.Installer with
            {
                Sha256 = wrongHash,
            },
        };
        byte[] checksums = ChecksumBytes(release.Installer);
        release = release with
        {
            Checksums = Asset("SHA256SUMS.txt", checksums),
        };
        using RoutingHandler handler = new(request =>
            RouteAssets(request, release, installer, checksums));
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            using ApplicationUpdateService service = new(
                paths,
                "1.3.0-beta.53",
                client);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.DownloadInstallerAsync(
                    release,
                    progress: null,
                    CancellationToken.None));
            Assert.False(File.Exists(paths.UpdateStageFile));
            Assert.Empty(Directory.EnumerateFiles(paths.Updates));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_CancellationDeletesPartialFile()
    {
        byte[] installer = Encoding.UTF8.GetBytes("installer");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        byte[] checksums = ChecksumBytes(release.Installer);
        release = release with
        {
            Checksums = Asset("SHA256SUMS.txt", checksums),
        };
        using RoutingHandler handler = new(request =>
        {
            if (request.RequestUri == release.Checksums.DownloadUri)
            {
                return Bytes(HttpStatusCode.OK, checksums);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(
                    new CancellationReadStream()),
            };
        });
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            using ApplicationUpdateService service = new(
                paths,
                "1.3.0-beta.53",
                client);
            using CancellationTokenSource cancellation =
                new(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.DownloadInstallerAsync(
                    release,
                    progress: null,
                    cancellation.Token));
            Assert.False(File.Exists(paths.UpdateStageFile));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(paths.Updates),
                path => path.EndsWith(
                    ".partial",
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CheckAsync_RejectsMetadataBeyondBoundedSize()
    {
        byte[] oversized = new byte[(4 * 1024 * 1024) + 1];
        using RoutingHandler handler = new(_ =>
            Bytes(HttpStatusCode.OK, oversized));
        using HttpClient client = new(handler);
        using ApplicationUpdateService service = new(
            new AppPaths(NewRoot()),
            "1.3.0-beta.53",
            client);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DownloadInstallerAsync_RejectsOversizedInventoryBeforeNetwork()
    {
        byte[] installer = Encoding.UTF8.GetBytes("installer");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        release = release with
        {
            Installer = release.Installer with
            {
                Size = (512L * 1024 * 1024) + 1,
            },
        };
        using RoutingHandler handler = new(_ =>
            throw new InvalidOperationException(
                "An invalid inventory must not reach the network."));
        using HttpClient client = new(handler);
        using ApplicationUpdateService service = new(
            new AppPaths(NewRoot()),
            "1.3.0-beta.53",
            client);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadInstallerAsync(
                release,
                progress: null,
                CancellationToken.None));
    }

    [Fact]
    public async Task DownloadInstallerAsync_RejectsChecksumInventoryDisagreement()
    {
        byte[] installer = Encoding.UTF8.GetBytes("installer");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        byte[] checksums = Encoding.UTF8.GetBytes(
            $"{new string('b', 64)}  {release.Installer.Name}\r\n");
        release = release with
        {
            Checksums = Asset("SHA256SUMS.txt", checksums),
        };
        using RoutingHandler handler = new(request =>
            RouteAssets(request, release, installer, checksums));
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            using ApplicationUpdateService service = new(
                paths,
                "1.3.0-beta.53",
                client);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.DownloadInstallerAsync(
                    release,
                    progress: null,
                    CancellationToken.None));
            Assert.False(File.Exists(paths.UpdateStageFile));
            Assert.Empty(Directory.EnumerateFiles(paths.Updates));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_RejectsExcessiveTrustedRedirects()
    {
        byte[] installer = Encoding.UTF8.GetBytes("installer");
        ApplicationUpdateRelease release =
            CreateRelease(installer);
        int requests = 0;
        using RoutingHandler handler = new(request =>
        {
            requests++;
            return new HttpResponseMessage(
                HttpStatusCode.Redirect)
            {
                Headers =
                {
                    Location = request.RequestUri,
                },
            };
        });
        using HttpClient client = new(handler);
        string root = NewRoot();
        try
        {
            using ApplicationUpdateService service = new(
                new AppPaths(root),
                "1.3.0-beta.53",
                client);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                service.DownloadInstallerAsync(
                    release,
                    progress: null,
                    CancellationToken.None));
            Assert.Equal(6, requests);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static HttpResponseMessage RouteAssets(
        HttpRequestMessage request,
        ApplicationUpdateRelease release,
        byte[] installer,
        byte[] checksums)
    {
        if (request.RequestUri == release.Checksums.DownloadUri)
        {
            return Bytes(HttpStatusCode.OK, checksums);
        }
        if (request.RequestUri == release.Installer.DownloadUri)
        {
            return Bytes(HttpStatusCode.OK, installer);
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static ApplicationUpdateRelease CreateRelease(
        byte[] installer)
    {
        ApplicationSemanticVersion version =
            ApplicationSemanticVersion.Parse(
                "1.3.0-beta.54");
        return new ApplicationUpdateRelease(
            version,
            IsPrerelease: true,
            "Expeditions Macro v1.3.0-beta.54",
            new Uri(
                "https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.3.0-beta.54"),
            Asset(
                "ExpeditionsMacro-1.3.0-beta.54-win-x64-setup.exe",
                installer),
            Asset("SHA256SUMS.txt", [0]));
    }

    private static ApplicationUpdateAsset Asset(
        string name,
        byte[] bytes) =>
        new(
            name,
            bytes.LongLength,
            Sha256(bytes),
            new Uri(
                $"https://github.com/LeniLilac/expeditions-macro/releases/download/v1.3.0-beta.54/{name}"));

    private static byte[] ChecksumBytes(
        ApplicationUpdateAsset installer) =>
        Encoding.UTF8.GetBytes(
            $"{installer.Sha256}  {installer.Name}\r\n");

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();

    private static HttpResponseMessage Bytes(
        HttpStatusCode status,
        byte[] bytes) =>
        new(status)
        {
            Content = new ByteArrayContent(bytes),
        };

    private static string NewRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            $"expeditions-update-{Guid.NewGuid():N}");

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RoutingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> route) :
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(route(request));
    }

    private sealed record RequestRecord(
        string Uri,
        string UserAgent,
        string? ApiVersion,
        string? Authorization)
    {
        public static RequestRecord From(
            HttpRequestMessage request) =>
            new(
                request.RequestUri!.AbsoluteUri,
                request.Headers.UserAgent.ToString(),
                request.Headers.TryGetValues(
                    "X-GitHub-Api-Version",
                    out IEnumerable<string>? versions)
                    ? versions.Single()
                    : null,
                request.Headers.Authorization?.ToString());
    }

    private sealed class CancellationReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
            return 0;
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) => throw new NotSupportedException();
    }
}
