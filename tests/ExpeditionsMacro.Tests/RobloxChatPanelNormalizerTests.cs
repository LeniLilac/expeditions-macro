using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Navigation;

namespace ExpeditionsMacro.Tests;

public sealed class RobloxChatPanelNormalizerTests
{
    [Fact]
    public async Task ClosedIndicator_SendsNoInput()
    {
        ChatAutomation automation = new(
            Closed(),
            Closed());

        bool changed =
            await CreateNormalizer(automation)
                .EnsureClosedAsync(
                    automation.Window,
                    CancellationToken.None);

        Assert.False(changed);
        Assert.Empty(automation.Clicks);
    }

    [Fact]
    public async Task StableOpenIndicator_ClicksAndProvesClosed()
    {
        ChatAutomation automation = new(
            Open(),
            Open(),
            Closed(),
            Closed());

        bool changed =
            await CreateNormalizer(automation)
                .EnsureClosedAsync(
                    automation.Window,
                    CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(
            [
                (
                    RobloxChatButtonDetector.ActionX,
                    RobloxChatButtonDetector.ActionY),
            ],
            automation.Clicks);
    }

    [Fact]
    public async Task IgnoredFirstClick_RedetectsBeforeOneRetry()
    {
        ChatAutomation automation = new(
            Open(),
            Open(),
            Open(),
            Open(),
            Closed(),
            Closed());

        bool changed =
            await CreateNormalizer(automation)
                .EnsureClosedAsync(
                    automation.Window,
                    CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(2, automation.Clicks.Count);
    }

    [Fact]
    public async Task SlowStableObservations_StillCloseWithinHardBound()
    {
        ChatAutomation automation = new(
            Open(),
            Open(),
            Closed(),
            Closed());
        DateTimeOffset now =
            new(
                2026,
                7,
                29,
                12,
                0,
                0,
                TimeSpan.Zero);
        RobloxChatPanelNormalizer normalizer = new(
            automation,
            () => now,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                now += TimeSpan.FromSeconds(10);
                return Task.CompletedTask;
            });

        bool changed =
            await normalizer.EnsureClosedAsync(
                automation.Window,
                CancellationToken.None);

        Assert.True(changed);
        Assert.Single(automation.Clicks);
    }

    [Fact]
    public async Task UnknownIndicator_NeverAuthorizesInput()
    {
        ImageFrame unknown = new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3]);
        ChatAutomation automation = new(
            unknown);

        await Assert.ThrowsAsync<
            RobloxUiUnavailableException>(
            () => CreateNormalizer(
                    automation,
                    delayMilliseconds: 6_000)
                .EnsureClosedAsync(
                    automation.Window,
                    CancellationToken.None));

        Assert.Empty(automation.Clicks);
    }

    [Fact]
    public async Task CancellationBeforeStableOpen_SendsNoInput()
    {
        ChatAutomation automation = new(
            Open(),
            Open());
        using CancellationTokenSource cancellation =
            new();
        DateTimeOffset now =
            new(
                2026,
                7,
                29,
                12,
                0,
                0,
                TimeSpan.Zero);
        RobloxChatPanelNormalizer normalizer = new(
            automation,
            () => now,
            (duration, token) =>
            {
                now += duration;
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            () => normalizer.EnsureClosedAsync(
                automation.Window,
                cancellation.Token));

        Assert.Empty(automation.Clicks);
    }

    private static RobloxChatPanelNormalizer
        CreateNormalizer(
        ChatAutomation automation,
        int delayMilliseconds = 180)
    {
        DateTimeOffset now =
            new(
                2026,
                7,
                29,
                12,
                0,
                0,
                TimeSpan.Zero);
        return new RobloxChatPanelNormalizer(
            automation,
            () => now,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                now += TimeSpan.FromMilliseconds(
                    delayMilliseconds);
                return Task.CompletedTask;
            });
    }

    private static ImageFrame Closed() =>
        Load("ChatClosed.png");

    private static ImageFrame Open() =>
        Load("ChatOpen.png");

    private static ImageFrame Load(
        string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                fileName));

    private sealed class ChatAutomation :
        IRobloxAutomation
    {
        private readonly IReadOnlyList<ImageFrame>
            _frames;
        private int _captureIndex;

        public ChatAutomation(
            params ImageFrame[] frames)
        {
            _frames = frames;
        }

        public RobloxWindow Window { get; } =
            new((nint)42, "Roblox");

        public List<(int X, int Y)> Clicks { get; } =
            [];

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
            CaptureClient(Window);

        public ImageFrame CaptureClient(
            RobloxWindow window)
        {
            ImageFrame frame = _frames[
                Math.Min(
                    _captureIndex,
                    _frames.Count - 1)];
            _captureIndex++;
            return frame.Clone();
        }

        public Task MoveCursorToClientCenterAsync(
            RobloxWindow window,
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
            CancellationToken cancellationToken)
        {
            Clicks.Add((x, y));
            return Task.CompletedTask;
        }

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

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
