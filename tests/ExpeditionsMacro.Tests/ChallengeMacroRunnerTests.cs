using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeMacroRunnerTests
{
    [Fact]
    public void PrestartPlacements_HiddenByTheStartDialogAreDeferredInOriginalOrder()
    {
        ScreenRegion dialog = new(314, 94, 180, 104);
        PlacementStep[] steps =
        [
            Step(354, 129),
            Step(300, 137),
            Step(384, 184),
            Step(363, 246),
            Step(485, 182),
            Step(578, 190),
        ];

        ChallengePlacementPartition partition = ChallengeRunPolicy.PartitionPrestartPlacements(steps, dialog);

        Assert.Equal([(300, 137), (363, 246), (578, 190)], partition.BeforeStart.Select(step => (step.X, step.Y)));
        Assert.Equal([(354, 129), (384, 184), (485, 182)], partition.AfterStart.Select(step => (step.X, step.Y)));

        static PlacementStep Step(int x, int y) => new()
        {
            UnitKey = 1,
            X = x,
            Y = y,
            DelayAfterMilliseconds = 900,
        };
    }

    [Fact]
    public async Task MapRecognition_ParksCursorBeforeDiscardingHighlightedSelectorFrame()
    {
        bool parked = false;
        int captures = 0;
        ImageFrame frame = new(1, 1, PixelFormat.Rgb24, new byte[3], takeOwnership: true);

        ChallengeMapId? map = await ChallengeMacroRunner.RecognizeMapAfterParkingAsync(
            _ =>
            {
                parked = true;
                return Task.CompletedTask;
            },
            () =>
            {
                Assert.True(parked);
                captures++;
                return frame;
            },
            _ => parked ? ChallengeMapId.FairyKingForest : null,
            retryMilliseconds: 0,
            maximumAttempts: 2,
            CancellationToken.None);

        Assert.Equal(ChallengeMapId.FairyKingForest, map);
        Assert.Equal(1, captures);
    }

    [Fact]
    public async Task PrestartAction_ReparksAndRecapturesWhenUnitHoverCoversButton()
    {
        int parks = 0;
        int captures = 0;
        ImageFrame frame = new(1, 1, PixelFormat.Rgb24, new byte[3], takeOwnership: true);

        (int X, int Y)? action = await ChallengeMacroRunner.LocateActionAfterParkingAsync(
            _ =>
            {
                parks++;
                return Task.CompletedTask;
            },
            () =>
            {
                captures++;
                return frame;
            },
            _ => captures >= 2 ? (404, 177) : null,
            retryMilliseconds: 0,
            maximumAttempts: 3,
            CancellationToken.None);

        Assert.NotNull(action);
        Assert.Equal((404, 177), action.Value);
        Assert.Equal(2, parks);
        Assert.Equal(2, captures);
    }

    [Fact]
    public void TeleportingScreen_ExtendsThePrestartDeadlineToThreeMinutes()
    {
        DateTimeOffset startedAt = new(2026, 7, 22, 12, 37, 13, TimeSpan.Zero);
        DateTimeOffset initialDeadline = startedAt + ChallengeMacroRunner.InitialPrestartTimeout;

        DateTimeOffset unchanged = ChallengeMacroRunner.ExtendPrestartDeadline(
            startedAt,
            initialDeadline,
            ChallengeScreenState.PreviewReady);
        DateTimeOffset extended = ChallengeMacroRunner.ExtendPrestartDeadline(
            startedAt,
            initialDeadline,
            ChallengeScreenState.Teleporting);

        Assert.Equal(initialDeadline, unchanged);
        Assert.Equal(startedAt + TimeSpan.FromMinutes(3), extended);
    }

    [Fact]
    public async Task PlayMenuKey_FirstIgnoredPress_IsRetried()
    {
        ImageFrame terminal = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "Defeat",
            "Defeat_04.png"));
        ImageFrame preview = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "PostMatchPreview",
            "PostMatchPreview_03.png"));
        List<char> presses = [];
        int captures = 0;
        int waits = 0;

        ImageFrame result = await PlayMenuNavigator.OpenWithRetriesAsync(
            playMenuKey: 'p',
            capture: () =>
            {
                captures++;
                return terminal.Clone();
            },
            pressKey: (key, _) =>
            {
                presses.Add(key);
                return Task.CompletedTask;
            },
            waitForPreview: (_, _) => Task.FromResult<ImageFrame?>(++waits == 1 ? null : preview),
            attemptStarted: null,
            attemptMissed: null,
            CancellationToken.None);

        Assert.Same(preview, result);
        Assert.Equal(2, captures);
        Assert.Equal(2, waits);
        Assert.Equal(['P', 'P'], presses);
    }

    [Fact]
    public async Task PlayMenuKey_AlreadyVisiblePreviewStillUsesStableWaiter()
    {
        ImageFrame preview = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "PostMatchPreview",
            "PostMatchPreview_03.png"));
        int waits = 0;
        int presses = 0;

        ImageFrame result = await PlayMenuNavigator.OpenWithRetriesAsync(
            playMenuKey: 'P',
            capture: () => preview,
            pressKey: (_, _) =>
            {
                presses++;
                return Task.CompletedTask;
            },
            waitForPreview: (_, _) =>
            {
                waits++;
                return Task.FromResult<ImageFrame?>(preview);
            },
            attemptStarted: null,
            attemptMissed: null,
            CancellationToken.None);

        Assert.Same(preview, result);
        Assert.Equal(1, waits);
        Assert.Equal(0, presses);
    }

    [Fact]
    public async Task PlayMenuKey_LateTransitionBeforeRetry_IsAcceptedWithoutAnotherPress()
    {
        ImageFrame terminal = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "Defeat",
            "Defeat_04.png"));
        ImageFrame preview = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "PostMatchPreview",
            "PostMatchPreview_03.png"));
        Queue<ImageFrame> captures = new([terminal, preview]);
        int presses = 0;
        int waits = 0;

        ImageFrame result = await PlayMenuNavigator.OpenWithRetriesAsync(
            playMenuKey: 'P',
            capture: () => captures.Dequeue().Clone(),
            pressKey: (_, _) =>
            {
                presses++;
                return Task.CompletedTask;
            },
            waitForPreview: (_, _) =>
            {
                waits++;
                return Task.FromResult<ImageFrame?>(
                    waits == 1 ? null : preview);
            },
            attemptStarted: null,
            attemptMissed: null,
            CancellationToken.None);

        Assert.Equal(ChallengeScreenState.PostMatchPreview, ChallengeScreenDetector.Detect(result).State);
        Assert.Equal(1, presses);
        Assert.Equal(2, waits);
        Assert.Empty(captures);
    }

    [Fact]
    public async Task LobbyPlayKey_IgnoredPress_StopsWithBindingInstructions()
    {
        ImageFrame lobby = ImageCodec.Load(Path.Combine(
            TestPaths.Datasets,
            "Lobby_UI",
            "Lobby_UI_001.png"));
        List<char> presses = [];
        List<int> keyMisses = [];

        PlayMenuBindingException error = await Assert.ThrowsAsync<PlayMenuBindingException>(() => LobbyPlayNavigator.OpenWithVerificationAsync(
            playMenuKey: 'p',
            capture: () => lobby,
            isLobby: frame => ReferenceEquals(frame, lobby),
            isOpen: _ => false,
            pressKey: (key, _) =>
            {
                presses.Add(key);
                return Task.CompletedTask;
            },
            waitForOpen: (_, _) => Task.FromResult(false),
            keyAttemptStarted: null,
            keyAttemptMissed: keyMisses.Add,
            CancellationToken.None));

        Assert.Equal(['P', 'P', 'P'], presses);
        Assert.Equal([1, 2, 3], keyMisses);
        Assert.Contains("Settings > Keybinds", error.Message, StringComparison.Ordinal);
        Assert.Contains("Toggle Play Menu", error.Message, StringComparison.Ordinal);
        Assert.Contains("set Toggle Play Menu to P", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LobbyPlayKey_AlreadyVisibleSelectorStillUsesStableWaiter()
    {
        ImageFrame modes = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "GameModeSelector",
            "GameModeSelector_01.png"));
        int waits = 0;
        int presses = 0;

        await LobbyPlayNavigator.OpenWithVerificationAsync(
            playMenuKey: 'P',
            capture: () => modes,
            isLobby: _ => false,
            isOpen: frame => ReferenceEquals(frame, modes),
            pressKey: (_, _) =>
            {
                presses++;
                return Task.CompletedTask;
            },
            waitForOpen: (_, _) =>
            {
                waits++;
                return Task.FromResult(true);
            },
            keyAttemptStarted: null,
            keyAttemptMissed: null,
            CancellationToken.None);

        Assert.Equal(1, waits);
        Assert.Equal(0, presses);
    }

    [Fact]
    public async Task LobbyPlayKey_LateKeyTransition_IsAcceptedWithoutAnotherPress()
    {
        ImageFrame lobby = ImageCodec.Load(Path.Combine(
            TestPaths.Datasets,
            "Lobby_UI",
            "Lobby_UI_001.png"));
        ImageFrame modes = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "GameModeSelector",
            "GameModeSelector_01.png"));
        bool transitioned = false;
        int presses = 0;
        int waits = 0;

        await LobbyPlayNavigator.OpenWithVerificationAsync(
            playMenuKey: 'P',
            capture: () => transitioned ? modes : lobby,
            isLobby: frame => ReferenceEquals(frame, lobby),
            isOpen: frame => ReferenceEquals(frame, modes),
            pressKey: (_, _) =>
            {
                presses++;
                return Task.CompletedTask;
            },
            waitForOpen: (_, _) =>
            {
                waits++;
                transitioned = true;
                return Task.FromResult(waits >= 2);
            },
            keyAttemptStarted: null,
            keyAttemptMissed: null,
            CancellationToken.None);

        Assert.Equal(1, presses);
        Assert.Equal(2, waits);
    }

    [Fact]
    public async Task CooldownSchedulerHandoff_RetriesUntilTheSharedGameModeSelectorIsVerified()
    {
        ImageFrame cooldown = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "ChallengeListUnavailable",
            "ChallengeListUnavailable_02.png"));
        ChallengeScreenMatch selector = ChallengeScreenDetector.Detect(cooldown);
        List<(int X, int Y)> clicks = [];
        List<int> started = [];
        List<int> missed = [];
        int verifications = 0;

        double confidence = await ChallengeMacroRunner.CloseChallengeSelectorForHandoffAsync(
            _ => Task.FromResult(selector),
            (x, y, _) =>
            {
                clicks.Add((x, y));
                return Task.CompletedTask;
            },
            _ => Task.FromResult<double?>(++verifications == 2 ? 0.97 : null),
            (attempt, _) => started.Add(attempt),
            (attempt, _) => missed.Add(attempt),
            CancellationToken.None);

        Assert.Equal(ChallengeScreenState.ChallengeListUnavailable, selector.State);
        Assert.Equal(0.97, confidence);
        Assert.Equal([1, 2], started);
        Assert.Equal([1], missed);
        Assert.Equal(2, clicks.Count);
        Assert.All(clicks, click => Assert.Equal((selector.ActionX, selector.ActionY), ((int?)click.X, (int?)click.Y)));
    }

    [Fact]
    public async Task CooldownSchedulerHandoff_NeverReturnsWhileTheOwnerPanelRemainsOpen()
    {
        ImageFrame cooldown = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "ChallengeListUnavailable",
            "ChallengeListUnavailable_01.png"));
        ChallengeScreenMatch selector = ChallengeScreenDetector.Detect(cooldown);
        int clicks = 0;

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ChallengeMacroRunner.CloseChallengeSelectorForHandoffAsync(
                _ => Task.FromResult(selector),
                (_, _, _) =>
                {
                    clicks++;
                    return Task.CompletedTask;
                },
                _ => Task.FromResult<double?>(null),
                closeAttemptStarted: null,
                closeAttemptMissed: null,
                CancellationToken.None));

        Assert.Equal(ChallengeMacroRunner.SchedulerHandoffMaximumAttempts, clicks);
        Assert.Contains("control was not returned to the task scheduler", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChallengeDetailBack_WaitsThroughStaleFramesBeforeAnotherClick()
    {
        ImageFrame detailFrame = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "ChallengeAvailable",
            "ChallengeAvailable_01.png"));
        ImageFrame selectorFrame = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "ChallengeList",
            "ChallengeList_01.png"));
        ChallengeScreenMatch detail = ChallengeScreenDetector.Detect(detailFrame);
        ChallengeScreenMatch selector = ChallengeScreenDetector.Detect(selectorFrame);
        Queue<ChallengeScreenMatch> observations = new([detail, detail, selector, selector]);
        List<int> attempts = [];
        List<int> misses = [];
        int clicks = 0;

        ChallengeScreenMatch result = await ChallengeMacroRunner.ReturnToChallengeSelectorWithVerificationAsync(
            stableDetections: 2,
            clickBack: _ =>
            {
                clicks++;
                return Task.CompletedTask;
            },
            observe: observations.Dequeue,
            pollMilliseconds: 0,
            verificationTimeout: TimeSpan.FromSeconds(1),
            maximumAttempts: ChallengeMacroRunner.SelectorBackMaximumAttempts,
            attemptStarted: attempts.Add,
            attemptMissed: (attempt, _) => misses.Add(attempt),
            CancellationToken.None);

        Assert.Equal(ChallengeScreenState.ChallengeAvailable, detail.State);
        Assert.Equal(ChallengeScreenState.ChallengeList, result.State);
        Assert.Equal(1, clicks);
        Assert.Equal([1], attempts);
        Assert.Empty(misses);
        Assert.Empty(observations);
    }

}
