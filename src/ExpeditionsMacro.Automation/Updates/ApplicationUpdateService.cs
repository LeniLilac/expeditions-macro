using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Automation.Updates;

public sealed class ApplicationUpdateService : IDisposable
{
    private const int MaximumMetadataBytes = 4 * 1024 * 1024;
    private const int MaximumChecksumBytes = 128 * 1024;
    private const long MaximumInstallerBytes = 512L * 1024 * 1024;
    private static readonly Uri ReleasesApi = new(
        "https://api.github.com/repos/LeniLilac/expeditions-macro/releases?per_page=50");
    private readonly AppPaths _paths;
    private readonly ApplicationUpdateTransport _transport;

    public ApplicationUpdateService(
        AppPaths paths,
        string currentVersion,
        HttpClient? client = null)
    {
        _paths = paths;
        CurrentVersion =
            ApplicationSemanticVersion.Parse(currentVersion);
        _transport = new ApplicationUpdateTransport(
            CurrentVersion.ToString(),
            client);
    }

    public ApplicationSemanticVersion CurrentVersion { get; }

    public string ChannelDescription =>
        CurrentVersion.IsPrerelease
            ? "Prerelease channel"
            : "Stable channel";

    public async Task<ApplicationUpdateRelease?> CheckAsync(
        CancellationToken cancellationToken)
    {
        byte[] json = await _transport.DownloadBytesAsync(
            ReleasesApi,
            MaximumMetadataBytes,
            expectedSize: null,
            expectedSha256: null,
            metadataRequest: true,
            cancellationToken).ConfigureAwait(false);
        return ApplicationUpdateReleaseParser.ParseLatest(
            json,
            CurrentVersion);
    }

    public async Task<string> DownloadInstallerAsync(
        ApplicationUpdateRelease release,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ValidateReleaseForDownload(release);
        _paths.EnsureCreated();
        await RemoveStagedAsync().ConfigureAwait(false);

        byte[] checksumBytes = await _transport.DownloadBytesAsync(
            release.Checksums.DownloadUri,
            MaximumChecksumBytes,
            release.Checksums.Size,
            release.Checksums.Sha256,
            metadataRequest: false,
            cancellationToken).ConfigureAwait(false);
        string checksumHash = ParseInstallerChecksum(
            checksumBytes,
            release.Installer.Name);
        if (!string.Equals(
                checksumHash,
                release.Installer.Sha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The installer hash does not agree with the release inventory.");
        }

        string destination = SafeUpdatePath(
            release.Installer.Name);
        string partial = destination + ".partial";
        try
        {
            await _transport.DownloadFileAsync(
                release.Installer,
                partial,
                MaximumInstallerBytes,
                progress,
                cancellationToken).ConfigureAwait(false);
            File.Move(partial, destination, overwrite: true);
            StagedApplicationUpdate stage = new(
                release.Version.ToString(),
                release.Installer.Name,
                release.Installer.Size,
                release.Installer.Sha256,
                release.ReleaseUri.AbsoluteUri);
            await JsonFileStore.WriteAtomicAsync(
                _paths.UpdateStageFile,
                stage,
                cancellationToken).ConfigureAwait(false);
            return destination;
        }
        finally
        {
            TryDelete(partial);
        }
    }

    public async Task<(StagedApplicationUpdate Stage, string Path)?>
        RecoverStagedAsync(
            CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        StagedApplicationUpdate? stage;
        try
        {
            stage = await JsonFileStore
                .ReadAsync<StagedApplicationUpdate>(
                    _paths.UpdateStageFile,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await RemoveStagedAsync().ConfigureAwait(false);
            return null;
        }

        if (stage is null ||
            !TryValidateStage(stage, out string? installerPath))
        {
            await RemoveStagedAsync().ConfigureAwait(false);
            return null;
        }

        ApplicationSemanticVersion version =
            ApplicationSemanticVersion.Parse(stage.Version);
        if (version.CompareTo(CurrentVersion) <= 0)
        {
            await RemoveStagedAsync().ConfigureAwait(false);
            return null;
        }

        FileInfo installer = new(installerPath);
        if (!installer.Exists ||
            installer.Length != stage.InstallerSize ||
            !string.Equals(
                await ComputeSha256Async(
                    installerPath,
                    cancellationToken).ConfigureAwait(false),
                stage.InstallerSha256,
                StringComparison.Ordinal))
        {
            await RemoveStagedAsync().ConfigureAwait(false);
            return null;
        }

        return (stage, installerPath);
    }

    public async Task RemoveStagedAsync()
    {
        if (File.Exists(_paths.UpdateStageFile))
        {
            try
            {
                StagedApplicationUpdate? stage =
                    await JsonFileStore
                        .ReadAsync<StagedApplicationUpdate>(
                            _paths.UpdateStageFile)
                        .ConfigureAwait(false);
                if (stage is not null &&
                    IsSafeFileName(stage.InstallerFileName))
                {
                    TryDelete(SafeUpdatePath(
                        stage.InstallerFileName));
                }
            }
            catch (JsonException)
            {
            }
        }
        TryDelete(_paths.UpdateStageFile);
        if (!Directory.Exists(_paths.Updates))
        {
            return;
        }
        foreach (string partial in
                 Directory.EnumerateFiles(
                     _paths.Updates,
                     "*.partial",
                     SearchOption.TopDirectoryOnly))
        {
            TryDelete(partial);
        }
        foreach (string installer in
                 Directory.EnumerateFiles(
                     _paths.Updates,
                     "ExpeditionsMacro-*-win-x64-setup.exe",
                     SearchOption.TopDirectoryOnly))
        {
            TryDelete(installer);
        }
    }

    public void Dispose() => _transport.Dispose();

    private bool TryValidateStage(
        StagedApplicationUpdate stage,
        out string installerPath)
    {
        installerPath = string.Empty;
        if (!ApplicationSemanticVersion.TryParse(
                stage.Version,
                out ApplicationSemanticVersion? version) ||
            !IsSafeFileName(stage.InstallerFileName) ||
            stage.InstallerFileName !=
                $"ExpeditionsMacro-{version}-win-x64-setup.exe" ||
            stage.InstallerSize <= 0 ||
            stage.InstallerSize > MaximumInstallerBytes ||
            !IsSha256(stage.InstallerSha256) ||
            stage.ReleaseUri !=
                $"https://github.com/LeniLilac/expeditions-macro/releases/tag/v{version}")
        {
            return false;
        }
        installerPath = SafeUpdatePath(
            stage.InstallerFileName);
        return true;
    }

    private void ValidateReleaseForDownload(
        ApplicationUpdateRelease release)
    {
        string version = release.Version.ToString();
        string installerName =
            $"ExpeditionsMacro-{version}-win-x64-setup.exe";
        if (release.Version.CompareTo(CurrentVersion) <= 0 ||
            (!CurrentVersion.IsPrerelease && release.IsPrerelease) ||
            release.IsPrerelease != release.Version.IsPrerelease ||
            release.ReleaseUri.AbsoluteUri !=
                $"https://github.com/LeniLilac/expeditions-macro/releases/tag/v{version}" ||
            release.Installer.Name != installerName ||
            release.Installer.DownloadUri.AbsoluteUri !=
                $"https://github.com/LeniLilac/expeditions-macro/releases/download/v{version}/{installerName}" ||
            release.Checksums.Name != "SHA256SUMS.txt" ||
            release.Checksums.DownloadUri.AbsoluteUri !=
                $"https://github.com/LeniLilac/expeditions-macro/releases/download/v{version}/SHA256SUMS.txt" ||
            release.Installer.Size <= 0 ||
            release.Installer.Size > MaximumInstallerBytes ||
            release.Checksums.Size <= 0 ||
            release.Checksums.Size > MaximumChecksumBytes ||
            !IsSha256(release.Installer.Sha256) ||
            !IsSha256(release.Checksums.Sha256))
        {
            throw new InvalidDataException(
                "The selected release is not an eligible application update.");
        }
    }

    private static string ParseInstallerChecksum(
        byte[] bytes,
        string installerName)
    {
        string? found = null;
        foreach (string line in Encoding.UTF8
                     .GetString(bytes)
                     .Split(['\r', '\n'],
                         StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 67 ||
                line[64] != ' ' ||
                line[65] != ' ' ||
                !line[..64].All(char.IsAsciiHexDigit) ||
                !string.Equals(
                    line[66..],
                    installerName,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (found is not null)
            {
                throw new InvalidDataException(
                    "The checksum inventory contains a duplicate installer entry.");
            }
            found = line[..64].ToLowerInvariant();
        }
        return found ?? throw new InvalidDataException(
            "The checksum inventory does not contain the installer.");
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(
            stream,
            cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string SafeUpdatePath(string fileName)
    {
        if (!IsSafeFileName(fileName))
        {
            throw new InvalidDataException(
                "The update file name is unsafe.");
        }
        string root = Path.GetFullPath(_paths.Updates) +
            Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(
            Path.Combine(_paths.Updates, fileName));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The update path escaped its staging directory.");
        }
        return path;
    }

    private static bool IsSafeFileName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        string.Equals(
            name,
            Path.GetFileName(name),
            StringComparison.Ordinal) &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character => char.IsAsciiHexDigit(character));

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
