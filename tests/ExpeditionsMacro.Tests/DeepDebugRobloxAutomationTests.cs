using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class DeepDebugRobloxAutomationTests
{
    [Fact]
    public async Task EnabledSessionCapturesBeforeAndAfterEveryAutomationAction()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            AppSettings settings = new()
            {
                DeepDebugEnabled = true,
            };
            DeepDebugSessionService debug = CreateService(
                paths,
                () => settings);
            ActionAutomation inner = new();
            DeepDebugRobloxAutomation automation =
                new(inner, debug);

            await debug.RunOperationAsync(
                "Action frames",
                null,
                async token =>
                {
                    await automation.TapKeyboardKeyAsync(
                        inner.Window,
                        RobloxKeyboardKey.Enter,
                        token);
                    await automation.ClickClientAsync(
                        inner.Window,
                        120,
                        240,
                        token);
                    await automation.MoveCursorBetweenClientPointsAsync(
                        inner.Window,
                        70,
                        240,
                        120,
                        240,
                        200,
                        token);
                },
                CancellationToken.None);

            Assert.Equal(6, inner.CaptureCount);
            string archivePath = Assert.Single(
                Directory.EnumerateFiles(
                    paths.Diagnostics,
                    "deep-debug-*.zip"));
            using ZipArchive archive =
                ZipFile.OpenRead(archivePath);
            Assert.Equal(
                6,
                archive.Entries.Count(entry =>
                    entry.FullName.StartsWith(
                        "frames/",
                        StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(
                [
                    ("tap_keyboard_key", "before"),
                    ("tap_keyboard_key", "after"),
                    ("click_client", "before"),
                    ("click_client", "after"),
                    ("move_cursor_between_client_points", "before"),
                    ("move_cursor_between_client_points", "after"),
                ],
                await ReadActionFramesAsync(archive));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task DisabledSessionDoesNotCaptureActionBoundaryFrames()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            AppSettings settings = new()
            {
                DeepDebugEnabled = false,
            };
            DeepDebugSessionService debug = CreateService(
                paths,
                () => settings);
            ActionAutomation inner = new();
            DeepDebugRobloxAutomation automation =
                new(inner, debug);

            await debug.RunOperationAsync(
                "No action frames",
                null,
                token => automation.ClickClientAsync(
                    inner.Window,
                    120,
                    240,
                    token),
                CancellationToken.None);

            Assert.Equal(0, inner.CaptureCount);
            Assert.Empty(
                Directory.EnumerateFiles(
                    paths.Diagnostics,
                    "deep-debug-*.zip"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static DeepDebugSessionService CreateService(
        AppPaths paths,
        Func<AppSettings> settings) =>
        new(
            paths,
            settings,
            () => null,
            _ => { },
            _ => { });

    private static async Task<
        IReadOnlyList<(string Action, string Phase)>>
        ReadActionFramesAsync(ZipArchive archive)
    {
        ZipArchiveEntry entry =
            archive.GetEntry("events.jsonl")
            ?? throw new InvalidDataException(
                "The Deep Debug archive has no event stream.");
        List<(string Action, string Phase)> result = [];
        await using Stream stream = entry.Open();
        using StreamReader reader =
            new(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync() is { } line)
        {
            using JsonDocument document =
                JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.GetProperty("category").GetString() !=
                    "frame" ||
                root.GetProperty("action").GetString() !=
                    "automation_action")
            {
                continue;
            }

            JsonElement data = root.GetProperty("data");
            result.Add((
                data.GetProperty("action").GetString()!,
                data.GetProperty("phase").GetString()!));
        }
        return result;
    }

    private sealed class ActionAutomation :
        IRobloxAutomation
    {
        private readonly ImageFrame _frame =
            new(
                808,
                611,
                PixelFormat.Rgb24,
                new byte[808 * 611 * 3]);

        public RobloxWindow Window { get; } =
            new((nint)42, "Roblox");

        public int CaptureCount { get; private set; }

        public RobloxWindow? FindWindow(
            string titleFragment = "Roblox") =>
            Window;

        public RobloxWindow? ForegroundWindow() =>
            Window;

        public ClientBounds GetClientBounds(
            RobloxWindow window) =>
            new(0, 0, 808, 611);

        public WindowBounds GetWindowBounds(
            RobloxWindow window) =>
            new(0, 0, 824, 650);

        public bool Focus(RobloxWindow window) =>
            true;

        public Task ResizeClientAsync(
            RobloxWindow window,
            int width,
            int height,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void RestoreWindowBounds(
            RobloxWindow window,
            WindowBounds bounds)
        {
        }

        public ImageFrame CaptureScreen(
            ScreenRegion region) =>
            _frame.Clone();

        public ImageFrame CaptureClient(
            RobloxWindow window)
        {
            CaptureCount++;
            return _frame.Clone();
        }

        public Task MoveCursorToClientCenterAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task MoveCursorBetweenClientPointsAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            int durationMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ParkCursorAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DragClientAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ScrollClientAsync(
            RobloxWindow window,
            int notches,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DragCameraAsync(
            RobloxWindow window,
            int deltaX,
            int deltaY,
            int chunkPixels,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PulseCameraYawAsync(
            RobloxWindow window,
            CameraYawDirection direction,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ZoomOutFullyAsync(
            RobloxWindow window,
            int ticks,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapShiftLockKeyAsync(
            RobloxWindow window,
            int virtualKey,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapLetterKeyAsync(
            RobloxWindow window,
            char key,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapKeyboardKeyAsync(
            RobloxWindow window,
            RobloxKeyboardKey key,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
