using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.Tests;

public sealed class DeepDebugSessionTests
{
    [Fact]
    public async Task SuccessfulSessionArchivesFramesEventsSanitizedSettingsAndReferencedModels()
    {
        using TestDirectory directory = new();
        AppPaths paths = new(directory.Path);
        paths.EnsureCreated();
        Directory.CreateDirectory(Path.Combine(paths.CameraModels, "camera-one"));
        await File.WriteAllTextAsync(Path.Combine(paths.CameraModels, "camera-one", "manifest.json"), "camera-model");
        Directory.CreateDirectory(Path.Combine(paths.PlacementModels, "placement-one"));
        await File.WriteAllTextAsync(Path.Combine(paths.PlacementModels, "placement-one", "placement.json"), "placement-model");
        string log = Path.Combine(paths.Logs, "macro-run.log");
        const string webhook = "https://canary.discord.com/api/webhooks/123456789012345678/secret-token";
        const string userId = "123456789012345678";
        await File.WriteAllTextAsync(log, $"before {webhook} after {userId}");
        AppSettings settings = new()
        {
            DeepDebugEnabled = true,
            EncryptedWebhook = "protected-secret-value",
            DiscordErrorUserId = userId,
            PlayMenuKey = "P",
            UnitMenuKey = "U",
        };
        DeepDebugSessionService service = CreateService(paths, () => settings, () => log);

        await service.RunOperationAsync(
            "Test operation",
            new DeepDebugOperationContext
            {
                CameraModelIds = ["camera-one"],
                PlacementModelIds = ["placement-one"],
            },
            _ =>
            {
                service.RecordFrame(new ImageFrame(2, 2, PixelFormat.Rgb24, new byte[12]), "unit_test");
                service.RecordEvent("workflow", "next_action", new { State = "ready", Action = "click" });
                service.RecordWindowsTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "keyboard", "key_down", VirtualKey: 0x50));
                service.RecordPlacementInput(new PlacementInputTrace(DateTimeOffset.UtcNow, "observed_mouse_move", ScreenX: 10, ScreenY: 20));
                return Task.CompletedTask;
            },
            CancellationToken.None);

        string archivePath = Assert.Single(Directory.EnumerateFiles(paths.Diagnostics, "deep-debug-*.zip"));
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        Assert.NotNull(archive.GetEntry("frames/frame-000000001.png"));
        Assert.NotNull(archive.GetEntry("events.jsonl"));
        Assert.NotNull(archive.GetEntry("configuration/start/settings-sanitized.json"));
        Assert.NotNull(archive.GetEntry("models/start/camera/camera-one/manifest.json"));
        Assert.NotNull(archive.GetEntry("models/start/placement/placement-one/placement.json"));

        using JsonDocument manifest = JsonDocument.Parse(await ReadEntryAsync(archive, "manifest.json"));
        Assert.Equal("success", manifest.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("frames").GetInt32());
        Assert.Equal(2, manifest.RootElement.GetProperty("input_events").GetInt32());

        string allText = string.Join(
            '\n',
            await Task.WhenAll(archive.Entries
                .Where(entry => entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                entry.FullName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
                                entry.FullName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                .Select(entry => ReadEntryAsync(entry))));
        Assert.DoesNotContain(webhook, allText, StringComparison.Ordinal);
        Assert.DoesNotContain(userId, allText, StringComparison.Ordinal);
        Assert.DoesNotContain("protected-secret-value", allText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED DISCORD WEBHOOK]", allText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED DISCORD USER ID]", allText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "error")]
    [InlineData(true, "canceled")]
    public async Task FailedAndCanceledSessionsStillCreateArchives(bool cancel, string expectedOutcome)
    {
        using TestDirectory directory = new();
        AppPaths paths = new(directory.Path);
        paths.EnsureCreated();
        AppSettings settings = new() { DeepDebugEnabled = true };
        DeepDebugSessionService service = CreateService(paths, () => settings, () => null);
        using CancellationTokenSource cancellation = new();
        if (cancel) cancellation.Cancel();

        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunOperationAsync(
                "Canceled operation",
                null,
                token => Task.FromCanceled(token),
                cancellation.Token));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunOperationAsync(
                "Failed operation",
                null,
                _ => throw new InvalidOperationException("expected failure"),
                CancellationToken.None));
        }

        string archivePath = Assert.Single(Directory.EnumerateFiles(paths.Diagnostics, "deep-debug-*.zip"));
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        using JsonDocument manifest = JsonDocument.Parse(await ReadEntryAsync(archive, "manifest.json"));
        Assert.Equal(expectedOutcome, manifest.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task DisabledSettingRunsWithoutCreatingADeepDebugArchive()
    {
        using TestDirectory directory = new();
        AppPaths paths = new(directory.Path);
        paths.EnsureCreated();
        AppSettings settings = new() { DeepDebugEnabled = false };
        DeepDebugSessionService service = CreateService(paths, () => settings, () => null);
        bool ran = false;

        await service.RunOperationAsync(
            "Disabled operation",
            null,
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(ran);
        Assert.Empty(Directory.EnumerateFiles(paths.Diagnostics, "deep-debug-*.zip"));
    }

    [Fact]
    public async Task MacroPlanSnapshotResolvesPresetAndModelGraph()
    {
        using TestDirectory directory = new();
        AppPaths paths = new(directory.Path);
        paths.EnsureCreated();
        foreach (string camera in new[] { "camera-expedition", "camera-challenge" })
        {
            Directory.CreateDirectory(Path.Combine(paths.CameraModels, camera));
            await File.WriteAllTextAsync(Path.Combine(paths.CameraModels, camera, "manifest.json"), camera);
        }
        foreach (string placement in new[] { "placement-expedition", "placement-challenge" })
        {
            Directory.CreateDirectory(Path.Combine(paths.PlacementModels, placement));
            await File.WriteAllTextAsync(Path.Combine(paths.PlacementModels, placement, "placement.json"), placement);
        }
        Directory.CreateDirectory(Path.Combine(paths.DetectorPacks, "anime-expeditions-expeditions", "current"));
        await File.WriteAllTextAsync(
            Path.Combine(paths.DetectorPacks, "anime-expeditions-expeditions", "current", "manifest.json"),
            "detector");

        ExpeditionPreset expedition = new()
        {
            Id = "expedition-route",
            Name = "Expedition route",
            CameraModelId = "camera-expedition",
            PlacementModelId = "placement-expedition",
        };
        ChallengePreset challenge = new()
        {
            Id = "challenge-route",
            Name = "Challenge route",
            Maps = ChallengePreset.EmptyMapProfiles()
                .Select(profile => profile with
                {
                    CameraModelId = "camera-challenge",
                    PrestartPlacementModelId = "placement-challenge",
                })
                .ToArray(),
        };
        MacroPlan plan = new()
        {
            Id = "daily-plan",
            Name = "Daily plan",
            Tasks =
            [
                new MacroTaskDefinition { Id = "expedition-task", Kind = MacroTaskKind.Expedition, PresetId = expedition.Id },
                new MacroTaskDefinition { Id = "challenge-task", Kind = MacroTaskKind.Challenge, PresetId = challenge.Id, Priority = 2 },
            ],
        };
        await JsonFileStore.WriteAtomicAsync(Path.Combine(paths.Presets, $"{expedition.Id}.json"), expedition);
        await JsonFileStore.WriteAtomicAsync(Path.Combine(paths.ChallengePresets, $"{challenge.Id}.json"), challenge);
        await JsonFileStore.WriteAtomicAsync(Path.Combine(paths.MacroPlans, $"{plan.Id}.json"), plan);
        AppSettings settings = new() { DeepDebugEnabled = true };
        DeepDebugSessionService service = CreateService(paths, () => settings, () => null);

        await service.RunOperationAsync(
            "Macro plan",
            new DeepDebugOperationContext { MacroPlanId = plan.Id },
            _ => Task.CompletedTask,
            CancellationToken.None);

        string archivePath = Assert.Single(Directory.EnumerateFiles(paths.Diagnostics, "deep-debug-*.zip"));
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        Assert.NotNull(archive.GetEntry("configuration/start/macro-plan.json"));
        Assert.NotNull(archive.GetEntry($"configuration/start/presets/expeditions/{expedition.Id}.json"));
        Assert.NotNull(archive.GetEntry($"configuration/start/presets/challenges/{challenge.Id}.json"));
        Assert.NotNull(archive.GetEntry("models/start/camera/camera-expedition/manifest.json"));
        Assert.NotNull(archive.GetEntry("models/start/camera/camera-challenge/manifest.json"));
        Assert.NotNull(archive.GetEntry("models/start/placement/placement-expedition/placement.json"));
        Assert.NotNull(archive.GetEntry("models/start/placement/placement-challenge/placement.json"));
        Assert.NotNull(archive.GetEntry("models/start/detector-packs/anime-expeditions-expeditions/manifest.json"));
    }

    private static DeepDebugSessionService CreateService(
        AppPaths paths,
        Func<AppSettings> settings,
        Func<string?> logPath) =>
        new(paths, settings, logPath, _ => { }, _ => { });

    private static async Task<string> ReadEntryAsync(ZipArchive archive, string path) =>
        await ReadEntryAsync(archive.GetEntry(path) ?? throw new InvalidDataException($"Missing archive entry '{path}'."));

    private static async Task<string> ReadEntryAsync(ZipArchiveEntry entry)
    {
        await using Stream stream = entry.Open();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory() => Path = TestPaths.NewTemporaryDirectory();

        public string Path { get; }

        public void Dispose() => TestPaths.DeleteTemporaryDirectory(Path);
    }
}
