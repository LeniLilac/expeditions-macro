using System.IO.Compression;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Tests;

public sealed class DetectorPackRepositoryTests
{
    [Fact]
    public async Task InstallAndRollback_RetainThePreviousValidatedPack()
    {
        string root = TestPaths.NewTemporaryDirectory();
        string modified = TestPaths.NewTemporaryDirectory();
        try
        {
            DetectorPackRepository repository = new(new AppPaths(root));
            await repository.InstallDirectoryAsync(TestPaths.DetectorPack);
            CopyDirectory(TestPaths.DetectorPack, modified);
            string manifestPath = Path.Combine(modified, "manifest.json");
            DetectorPackManifest manifest = (await JsonFileStore.ReadAsync<DetectorPackManifest>(manifestPath))!;
            await JsonFileStore.WriteAtomicAsync(manifestPath, manifest with { Version = "1.0.3" });

            await repository.InstallDirectoryAsync(modified);
            Assert.Equal("1.0.3", Assert.Single(await repository.ListAsync()).Version);

            await repository.RollbackAsync(AnimeExpeditionsDetectorSpec.PackId);

            Assert.Equal("1.0.2", Assert.Single(await repository.ListAsync()).Version);
            Assert.NotNull(await repository.LoadAsync(AnimeExpeditionsDetectorSpec.PackId));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(modified);
        }
    }

    [Fact]
    public async Task EnsureBundled_ReplacesAnOlderInstalledPack()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            DetectorPackRepository repository = new(new AppPaths(root));
            await repository.InstallDirectoryAsync(TestPaths.LegacyDetectorPack);

            Assert.True(await repository.EnsureBundledAsync(TestPaths.DetectorPack));

            Assert.Equal("1.0.2", Assert.Single(await repository.ListAsync()).Version);
            Assert.True((await repository.LoadAsync(AnimeExpeditionsDetectorSpec.PackId))!.SupportsChallengeMaps);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureBundled_ReplacesAStaleSameVersionPayload()
    {
        string root = TestPaths.NewTemporaryDirectory();
        string stale = TestPaths.NewTemporaryDirectory();
        try
        {
            await CopyWithVersionAsync(TestPaths.LegacyDetectorPack, stale, "1.0.2");
            DetectorPackRepository repository = new(new AppPaths(root));
            await repository.InstallDirectoryAsync(stale);

            Assert.True(await repository.EnsureBundledAsync(TestPaths.DetectorPack));

            DetectorPackManifest installed = Assert.Single(await repository.ListAsync());
            Assert.Equal("1.0.2", installed.Version);
            Assert.Equal(34, installed.Files.Count);
            Assert.True((await repository.LoadAsync(AnimeExpeditionsDetectorSpec.PackId))!.SupportsChallengeMaps);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(stale);
        }
    }

    [Fact]
    public async Task EnsureBundled_ReplacesASameVersionManifestMismatch()
    {
        string root = TestPaths.NewTemporaryDirectory();
        string stale = TestPaths.NewTemporaryDirectory();
        try
        {
            CopyDirectory(TestPaths.DetectorPack, stale);
            string manifestPath = Path.Combine(stale, "manifest.json");
            DetectorPackManifest manifest = (await JsonFileStore.ReadAsync<DetectorPackManifest>(manifestPath))!;
            await JsonFileStore.WriteAtomicAsync(manifestPath, manifest with { MinimumAppVersion = "99.0.0" });
            DetectorPackRepository repository = new(new AppPaths(root));
            await repository.InstallDirectoryAsync(stale);

            Assert.True(await repository.EnsureBundledAsync(TestPaths.DetectorPack));

            DetectorPackManifest installed = Assert.Single(await repository.ListAsync());
            Assert.Equal("0.1.0", installed.MinimumAppVersion);
            Assert.True((await repository.LoadAsync(AnimeExpeditionsDetectorSpec.PackId))!.SupportsChallengeMaps);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(stale);
        }
    }

    [Fact]
    public async Task EnsureBundled_PreservesANewerInstalledPack()
    {
        string root = TestPaths.NewTemporaryDirectory();
        string newer = TestPaths.NewTemporaryDirectory();
        try
        {
            await CopyWithVersionAsync(TestPaths.LegacyDetectorPack, newer, "1.0.3");
            DetectorPackRepository repository = new(new AppPaths(root));
            await repository.InstallDirectoryAsync(newer);

            Assert.False(await repository.EnsureBundledAsync(TestPaths.DetectorPack));

            Assert.Equal("1.0.3", Assert.Single(await repository.ListAsync()).Version);
            Assert.False((await repository.LoadAsync(AnimeExpeditionsDetectorSpec.PackId))!.SupportsChallengeMaps);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(newer);
        }
    }

    [Fact]
    public async Task InstallDirectory_RejectsAChangedCompiledReference()
    {
        string root = TestPaths.NewTemporaryDirectory();
        string corrupt = TestPaths.NewTemporaryDirectory();
        try
        {
            CopyDirectory(TestPaths.DetectorPack, corrupt);
            string file = Directory.EnumerateFiles(Path.Combine(corrupt, "states"), "*.png", SearchOption.AllDirectories).First();
            await File.AppendAllBytesAsync(file, [1, 2, 3]);
            DetectorPackRepository repository = new(new AppPaths(root));

            await Assert.ThrowsAsync<InvalidDataException>(() => repository.InstallDirectoryAsync(corrupt));
            Assert.Empty(await repository.ListAsync());
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(corrupt);
        }
    }

    [Fact]
    public async Task ZipInstall_RejectsPathTraversal()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            await using MemoryStream package = new();
            using (ZipArchive archive = new(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry entry = archive.CreateEntry("../outside.txt");
                await using StreamWriter writer = new(entry.Open());
                await writer.WriteAsync("unsafe");
            }
            package.Position = 0;
            DetectorPackRepository repository = new(new AppPaths(root));

            await Assert.ThrowsAsync<InvalidDataException>(() => repository.InstallAsync(package));
            Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static async Task CopyWithVersionAsync(string source, string destination, string version)
    {
        CopyDirectory(source, destination);
        string manifestPath = Path.Combine(destination, "manifest.json");
        DetectorPackManifest manifest = (await JsonFileStore.ReadAsync<DetectorPackManifest>(manifestPath))!;
        await JsonFileStore.WriteAtomicAsync(manifestPath, manifest with { Version = version });
    }
}
