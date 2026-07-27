using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Tests;

public sealed class DetectorPackRepositoryTests
{
    [Fact]
    public async Task EnsureBundled_InstallsValidatedReleaseCopy()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            DetectorPackRepository repository =
                new(new AppPaths(root));

            Assert.True(
                await repository.EnsureBundledAsync(
                    TestPaths.DetectorPack));

            DetectorPackManifest installed =
                Assert.Single(await repository.ListAsync());
            Assert.Equal("1.0.2", installed.Version);
            Assert.True(
                (await repository.LoadAsync(
                    AnimeExpeditionsDetectorSpec.PackId))!
                .SupportsChallengeMaps);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureBundled_ReplacesAnOlderCachedCopy()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            SeedCurrent(paths, TestPaths.LegacyDetectorPack);
            DetectorPackRepository repository = new(paths);

            Assert.True(
                await repository.EnsureBundledAsync(
                    TestPaths.DetectorPack));

            Assert.Equal(
                "1.0.2",
                Assert.Single(await repository.ListAsync())
                    .Version);
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
            await CopyWithVersionAsync(
                TestPaths.LegacyDetectorPack,
                stale,
                "1.0.2");
            AppPaths paths = new(root);
            SeedCurrent(paths, stale);
            DetectorPackRepository repository = new(paths);

            Assert.True(
                await repository.EnsureBundledAsync(
                    TestPaths.DetectorPack));

            DetectorPackManifest installed =
                Assert.Single(await repository.ListAsync());
            Assert.Equal(34, installed.Files.Count);
            Assert.True(
                (await repository.LoadAsync(
                    AnimeExpeditionsDetectorSpec.PackId))!
                .SupportsChallengeMaps);
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
            string manifestPath =
                Path.Combine(stale, "manifest.json");
            DetectorPackManifest manifest =
                (await JsonFileStore.ReadAsync<
                    DetectorPackManifest>(manifestPath))!;
            await JsonFileStore.WriteAtomicAsync(
                manifestPath,
                manifest with
                {
                    MinimumAppVersion = "99.0.0",
                });
            AppPaths paths = new(root);
            SeedCurrent(paths, stale);
            DetectorPackRepository repository = new(paths);

            Assert.True(
                await repository.EnsureBundledAsync(
                    TestPaths.DetectorPack));

            Assert.Equal(
                "0.1.0",
                Assert.Single(await repository.ListAsync())
                    .MinimumAppVersion);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(stale);
        }
    }

    [Fact]
    public async Task EnsureBundled_ReplacesANewerCachedCopy()
    {
        string root = TestPaths.NewTemporaryDirectory();
        string newer = TestPaths.NewTemporaryDirectory();
        try
        {
            await CopyWithVersionAsync(
                TestPaths.LegacyDetectorPack,
                newer,
                "1.0.3");
            AppPaths paths = new(root);
            SeedCurrent(paths, newer);
            DetectorPackRepository repository = new(paths);

            Assert.True(
                await repository.EnsureBundledAsync(
                    TestPaths.DetectorPack));

            Assert.Equal(
                "1.0.2",
                Assert.Single(await repository.ListAsync())
                    .Version);
            Assert.True(
                (await repository.LoadAsync(
                    AnimeExpeditionsDetectorSpec.PackId))!
                .SupportsChallengeMaps);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(newer);
        }
    }

    [Fact]
    public async Task EnsureBundled_RepairsACorruptedCachedCopy()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            SeedCurrent(paths, TestPaths.DetectorPack);
            File.Delete(
                Path.Combine(
                    CurrentDirectory(paths),
                    "challenge-maps",
                    "fairy-king-forest.png"));
            DetectorPackRepository repository = new(paths);

            Assert.True(
                await repository.EnsureBundledAsync(
                    TestPaths.DetectorPack));

            Assert.True(
                (await repository.LoadAsync(
                    AnimeExpeditionsDetectorSpec.PackId))!
                .SupportsChallengeMaps);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureBundled_RemovesRetiredRollbackCopy()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            SeedCurrent(paths, TestPaths.DetectorPack);
            string previous = Path.Combine(
                paths.DetectorPacks,
                AnimeExpeditionsDetectorSpec.PackId,
                "previous");
            CopyDirectory(
                TestPaths.LegacyDetectorPack,
                previous);
            DetectorPackRepository repository = new(paths);

            Assert.False(
                await repository.EnsureBundledAsync(
                    TestPaths.DetectorPack));

            Assert.False(Directory.Exists(previous));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureBundled_ExplainsWhenReleaseCopyIsDamaged()
    {
        string root = TestPaths.NewTemporaryDirectory();
        string damaged = TestPaths.NewTemporaryDirectory();
        try
        {
            CopyDirectory(TestPaths.DetectorPack, damaged);
            File.Delete(
                Path.Combine(
                    damaged,
                    "challenge-maps",
                    "fairy-king-forest.png"));
            DetectorPackRepository repository =
                new(new AppPaths(root));

            InvalidDataException error =
                await Assert.ThrowsAsync<InvalidDataException>(
                    () => repository.EnsureBundledAsync(
                        damaged));

            Assert.Contains(
                "bundled with this copy",
                error.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "new empty folder",
                error.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "fairy-king-forest.png",
                error.InnerException!.Message,
                StringComparison.Ordinal);
            Assert.Empty(await repository.ListAsync());
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(damaged);
        }
    }

    [Fact]
    public async Task EnsureBundled_RejectsChangedCompiledReference()
    {
        string root = TestPaths.NewTemporaryDirectory();
        string corrupt = TestPaths.NewTemporaryDirectory();
        try
        {
            CopyDirectory(TestPaths.DetectorPack, corrupt);
            string file = Directory.EnumerateFiles(
                    Path.Combine(corrupt, "states"),
                    "*.png",
                    SearchOption.AllDirectories)
                .First();
            await File.AppendAllBytesAsync(file, [1, 2, 3]);
            DetectorPackRepository repository =
                new(new AppPaths(root));

            await Assert.ThrowsAsync<InvalidDataException>(
                () => repository.EnsureBundledAsync(corrupt));
            Assert.Empty(await repository.ListAsync());
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(corrupt);
        }
    }

    private static void SeedCurrent(
        AppPaths paths,
        string source) =>
        CopyDirectory(source, CurrentDirectory(paths));

    private static string CurrentDirectory(AppPaths paths) =>
        Path.Combine(
            paths.DetectorPacks,
            AnimeExpeditionsDetectorSpec.PackId,
            "current");

    private static void CopyDirectory(
        string source,
        string destination)
    {
        foreach (string directory in
            Directory.EnumerateDirectories(
                source,
                "*",
                SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    Path.GetRelativePath(source, directory)));
        }
        foreach (string file in
            Directory.EnumerateFiles(
                source,
                "*",
                SearchOption.AllDirectories))
        {
            string target = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(
                Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static async Task CopyWithVersionAsync(
        string source,
        string destination,
        string version)
    {
        CopyDirectory(source, destination);
        string manifestPath =
            Path.Combine(destination, "manifest.json");
        DetectorPackManifest manifest =
            (await JsonFileStore.ReadAsync<
                DetectorPackManifest>(manifestPath))!;
        await JsonFileStore.WriteAtomicAsync(
            manifestPath,
            manifest with { Version = version });
    }
}
