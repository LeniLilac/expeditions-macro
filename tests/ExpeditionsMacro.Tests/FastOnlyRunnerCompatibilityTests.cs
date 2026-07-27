using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class FastOnlyRunnerCompatibilityTests
{
    [Fact]
    public async Task LegacyExpeditionPreset_StopsBeforeRobloxDiscovery()
    {
        NoInputAutomation automation = new();
        ExpeditionMacroRunner runner =
            new(automation, null!, null!, null!);
        ExpeditionPreset preset = new()
        {
            Id = "legacy-expedition",
            Name = "Legacy Expedition",
            CameraPreparationMode =
                CameraPreparationMode.CameraModel,
            CameraModelId = "camera-one",
            PlacementModelId = "placement-one",
        };

        InvalidDataException error =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => runner.RunAsync(
                    preset,
                    null!,
                    null!,
                    string.Empty,
                    'P'));

        AssertRetiredGuidance(error, automation);
    }

    [Fact]
    public async Task LegacyChallengePreset_StopsBeforeRobloxDiscovery()
    {
        NoInputAutomation automation = new();
        ChallengeMacroRunner runner =
            new(automation, null!, null!, null!);
        ChallengePreset preset = new()
        {
            Id = "legacy-challenge",
            Name = "Legacy Challenge",
            CameraPreparationMode =
                CameraPreparationMode.CameraModel,
            Maps = ChallengePreset.EmptyMapProfiles(),
        };

        InvalidDataException error =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => runner.RunAsync(
                    preset,
                    new Dictionary<
                        ChallengeMapId,
                        ChallengeMapRuntimeModels>(),
                    null!,
                    new ChallengeRotationState(),
                    string.Empty,
                    'P'));

        AssertRetiredGuidance(error, automation);
    }

    [Fact]
    public async Task LegacyStagePreset_StopsBeforeRobloxDiscovery()
    {
        NoInputAutomation automation = new();
        StageMacroRunner runner =
            new(automation, null!, null!, null!);
        StoryPreset preset = new()
        {
            Id = "legacy-story",
            Name = "Legacy Story",
            CameraPreparationMode =
                CameraPreparationMode.CameraModel,
            CameraModelId = "camera-one",
            PrestartPlacementModelId = "placement-one",
        };

        InvalidDataException error =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => runner.RunStoryAsync(
                    preset,
                    new StageRuntimeModels(null),
                    null!,
                    string.Empty,
                    'P',
                    unitMenuKey: null));

        AssertRetiredGuidance(error, automation);
    }

    private static void AssertRetiredGuidance(
        InvalidDataException error,
        NoInputAutomation automation)
    {
        Assert.Contains(
            "retired Camera Model workflow",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "Open Placement Setup",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(0, automation.CallCount);
    }

    private sealed class NoInputAutomation : IRobloxAutomation
    {
        public int CallCount { get; private set; }

        public RobloxWindow? FindWindow(
            string titleFragment = "Roblox") =>
            Fail<RobloxWindow?>();

        public RobloxWindow? ForegroundWindow() =>
            Fail<RobloxWindow?>();

        public ClientBounds GetClientBounds(
            RobloxWindow window) =>
            Fail<ClientBounds>();

        public WindowBounds GetWindowBounds(
            RobloxWindow window) =>
            Fail<WindowBounds>();

        public bool Focus(RobloxWindow window) =>
            Fail<bool>();

        public Task ResizeClientAsync(
            RobloxWindow window,
            int width,
            int height,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public void RestoreWindowBounds(
            RobloxWindow window,
            WindowBounds bounds) =>
            Fail();

        public ImageFrame CaptureScreen(
            ScreenRegion region) =>
            Fail<ImageFrame>();

        public ImageFrame CaptureClient(
            RobloxWindow window) =>
            Fail<ImageFrame>();

        public Task MoveCursorToClientCenterAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task ParkCursorAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task DragClientAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task ScrollClientAsync(
            RobloxWindow window,
            int notches,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task DragCameraAsync(
            RobloxWindow window,
            int deltaX,
            int deltaY,
            int chunkPixels,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task ZoomOutFullyAsync(
            RobloxWindow window,
            int ticks,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task TapShiftLockKeyAsync(
            RobloxWindow window,
            int virtualKey,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task TapLetterKeyAsync(
            RobloxWindow window,
            char key,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Fail<Task>();

        private T Fail<T>()
        {
            CallCount++;
            throw new InvalidOperationException(
                "Roblox automation must not be touched.");
        }

        private void Fail()
        {
            CallCount++;
            throw new InvalidOperationException(
                "Roblox automation must not be touched.");
        }
    }
}
