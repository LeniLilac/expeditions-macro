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
            await JsonFileStore.WriteAtomicAsync(manifestPath, manifest with { Version = "1.0.1" });

            await repository.InstallDirectoryAsync(modified);
            Assert.Equal("1.0.1", Assert.Single(await repository.ListAsync()).Version);

            await repository.RollbackAsync(AnimeExpeditionsDetectorSpec.PackId);

            Assert.Equal("1.0.0", Assert.Single(await repository.ListAsync()).Version);
            Assert.NotNull(await repository.LoadAsync(AnimeExpeditionsDetectorSpec.PackId));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
            TestPaths.DeleteTemporaryDirectory(modified);
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
}
