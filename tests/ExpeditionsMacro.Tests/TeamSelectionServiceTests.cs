using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Tests;

public sealed class TeamSelectionServiceTests
{
    [Theory]
    [InlineData(1, "TeamEquipmentConfirm_01.png")]
    [InlineData(2, "TeamEquipmentConfirm_01.png")]
    [InlineData(3, "TeamEquipmentConfirm_01.png")]
    [InlineData(3, "TeamEquipmentConfirm_Compact_01.png")]
    [InlineData(4, "TeamEquipmentConfirm_01.png")]
    [InlineData(5, "TeamEquipmentConfirm_01.png")]
    [InlineData(6, "TeamEquipmentConfirm_01.png")]
    [InlineData(7, "TeamEquipmentConfirm_01.png")]
    [InlineData(8, "TeamEquipmentConfirm_01.png")]
    public async Task Select_AlignsAndLoadsEveryTeamWithoutWheelScrolling(
        int teamSlot,
        string equipmentFixture)
    {
        FakeAutomation automation = new(teamSlot, equipmentFixture);
        TeamSelectionService service = new(automation);

        await service.SelectAsync(automation.Window, teamSlot, unitMenuKey: 'u');

        List<string> expected =
        [
            "key:U",
            $"click:{automation.UnitsTeamsAction.X},{automation.UnitsTeamsAction.Y}",
        ];
        TeamScrollbarThumb initialThumb = TeamScreenDetector.FindScrollbarThumb(automation.InitialTeamFrame)!.Value;
        if (teamSlot != 1)
        {
            int dragEndY = teamSlot >= 7
                ? TeamScreenDetector.BottomScrollbarDragLimitY
                : TeamScreenDetector.ScrollThumbTargetCenterY(teamSlot, initialThumb.CenterY);
            expected.Add(
                $"drag:{initialThumb.X},{initialThumb.CenterY}->{initialThumb.X},{dragEndY}");
        }
        int targetCenterY =
            TeamScreenDetector.ScrollThumbTargetCenterY(teamSlot, initialThumb.CenterY);
        (int X, int Y) loadAction =
            TeamScreenDetector.AlignedLoadTeamAction(
                automation.AlignedTeamFrame,
                teamSlot,
                targetCenterY)!.Value;
        expected.AddRange(
            [
                $"click:{loadAction.X},{loadAction.Y}",
                $"click:{automation.LoadConfirmAction.X},{automation.LoadConfirmAction.Y}",
                $"click:{automation.EquipmentAction.X},{automation.EquipmentAction.Y}",
                "park",
                "key:U",
                "key:U",
            ]);

        Assert.Equal(expected, automation.Actions);
        Assert.Equal(TeamScreenState.None, automation.State);
        Assert.True(automation.FocusCount > automation.Actions.Count);
    }

    [Fact]
    public async Task Select_StopsBeforeInputWhenTheClientSizeChanged()
    {
        FakeAutomation automation = new(teamSlot: 1, "TeamEquipmentConfirm_01.png")
        {
            Client = new ClientBounds(0, 0, 800, 600),
        };
        TeamSelectionService service = new(automation);

        RobloxSessionUnavailableException error = await Assert.ThrowsAsync<RobloxSessionUnavailableException>(
            () => service.SelectAsync(automation.Window, teamSlot: 1, unitMenuKey: 'u'));

        Assert.Contains("808 by 611", error.Message, StringComparison.Ordinal);
        Assert.Equal(["key:U"], automation.Actions);
    }

    [Fact]
    public async Task Select_RetriesWhenVerifiedUnitInventoryRemainsOpen()
    {
        FakeAutomation automation = new(
            teamSlot: 1,
            equipmentFixture: "TeamEquipmentConfirm_01.png",
            ignoredTeamsTabClicks: 1);
        TeamSelectionService service = new(automation);

        await service.SelectAsync(
            automation.Window,
            teamSlot: 1,
            unitMenuKey: 'u');

        Assert.Equal(
            2,
            automation.Actions.Count(action =>
                action ==
                    $"click:{automation.UnitsTeamsAction.X},{automation.UnitsTeamsAction.Y}"));
        Assert.Equal(TeamScreenState.None, automation.State);
    }

    [Fact]
    public async Task Select_StopsAfterTwoIgnoredVerifiedTeamsActions()
    {
        FakeAutomation automation = new(
            teamSlot: 1,
            equipmentFixture: "TeamEquipmentConfirm_01.png",
            ignoredTeamsTabClicks: 2);
        TeamSelectionService service = new(automation);

        RobloxUiUnavailableException error =
            await Assert.ThrowsAsync<RobloxUiUnavailableException>(
                () => service.SelectAsync(
                    automation.Window,
                    teamSlot: 1,
                    unitMenuKey: 'u'));

        Assert.Contains(
            "after 2 verified click attempts",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            automation.Actions.Count(action =>
                action ==
                    $"click:{automation.UnitsTeamsAction.X},{automation.UnitsTeamsAction.Y}"));
    }

    [Fact]
    public async Task Select_FieldUnitInventoryUsesItsLiveTeamsAction()
    {
        FakeAutomation automation = new(
            teamSlot: 1,
            equipmentFixture: "TeamEquipmentConfirm_01.png",
            unitsFixture: "TeamUnits_CurrentGreenDecoys_01.png");
        TeamSelectionService service = new(automation);

        await service.SelectAsync(
            automation.Window,
            teamSlot: 1,
            unitMenuKey: 'u');

        Assert.Equal((305, 452), automation.UnitsTeamsAction);
        Assert.Contains(
            "click:305,452",
            automation.Actions);
        Assert.Equal(TeamScreenState.None, automation.State);
    }

    [Fact]
    public async Task Select_SlowOwnerObservationsCompleteTheVerifiedTransition()
    {
        FakeAutomation automation = new(
            teamSlot: 1,
            equipmentFixture: "TeamEquipmentConfirm_01.png");
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        automation.CaptureObserved =
            () => now += TimeSpan.FromSeconds(8);
        TeamSelectionService service = new(
            automation,
            () => now,
            static (_, _) => Task.CompletedTask);

        await service.SelectAsync(
            automation.Window,
            teamSlot: 1,
            unitMenuKey: 'u');

        Assert.Contains(
            $"click:{automation.UnitsTeamsAction.X},{automation.UnitsTeamsAction.Y}",
            automation.Actions);
        Assert.Equal(TeamScreenState.None, automation.State);
    }

    [Fact]
    public async Task Select_ReopensAtTopAndRealignsTheScrollbarForEveryLoad()
    {
        FakeAutomation automation = new(teamSlot: 6, equipmentFixture: "TeamEquipmentConfirm_01.png");
        TeamSelectionService service = new(automation);

        await service.SelectAsync(automation.Window, teamSlot: 6, unitMenuKey: 'u');
        await service.SelectAsync(automation.Window, teamSlot: 6, unitMenuKey: 'u');

        Assert.Equal(2, automation.Actions.Count(action => action.StartsWith("drag:", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public async Task Select_LowerTeamsDragPastTheTrackSoRobloxClampsAtBottom(
        int teamSlot)
    {
        FakeAutomation automation = new(
            teamSlot,
            equipmentFixture: "TeamEquipmentConfirm_01.png");
        TeamSelectionService service = new(automation);

        await service.SelectAsync(
            automation.Window,
            teamSlot,
            unitMenuKey: 'u');

        Assert.Contains(
            automation.Actions,
            action => action.EndsWith(
                $",{TeamScreenDetector.BottomScrollbarDragLimitY}",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Select_WaitsForTheOpeningAnimationAndUsesTheRealTopThumb()
    {
        FakeAutomation automation = new(
            teamSlot: 1,
            equipmentFixture: "TeamEquipmentConfirm_01.png",
            openingFixtures:
            [
                "TeamList_Opening_01.png",
                "TeamList_Opening_02.png",
                "TeamList_Opening_03.png",
                "TeamList_Opening_04.png",
            ]);
        TeamSelectionService service = new(automation);

        await service.SelectAsync(
            automation.Window,
            teamSlot: 1,
            unitMenuKey: 'u');

        Assert.DoesNotContain(
            automation.Actions,
            action => action.StartsWith("drag:", StringComparison.Ordinal));
        Assert.Contains(
            automation.Actions,
            action => action.StartsWith("click:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Select_NormalizesAReopenedScrolledListBeforeLoading()
    {
        FakeAutomation automation = new(
            teamSlot: 1,
            equipmentFixture: "TeamEquipmentConfirm_01.png",
            initialTeamFixture: "TeamList_Aligned_Team2_01.png");
        TeamSelectionService service = new(automation);

        await service.SelectAsync(
            automation.Window,
            teamSlot: 1,
            unitMenuKey: 'u');

        TeamScrollbarThumb scrolled =
            TeamScreenDetector.FindScrollbarThumb(
                automation.InitialTeamFrame)!.Value;
        Assert.Contains(
            $"drag:{scrolled.X},{scrolled.CenterY}->{scrolled.X},{TeamScreenDetector.TopScrollbarDragLimitY}",
            automation.Actions);
    }

    [Fact]
    public async Task Select_AcceptsAStableNearTargetScrollbarUndershoot()
    {
        FakeAutomation automation = new(
            teamSlot: 3,
            equipmentFixture:
                "TeamEquipmentConfirm_01.png",
            settledDragFixtures:
            [
                "TeamList_Aligned_Team2_01.png",
                "TeamList_Aligned_Team3_Undershoot_01.png",
            ]);
        TeamSelectionService service = new(automation);

        await service.SelectAsync(
            automation.Window,
            teamSlot: 3,
            unitMenuKey: 'u');

        Assert.Equal(
            2,
            automation.Actions.Count(action =>
                action.StartsWith(
                    "drag:",
                    StringComparison.Ordinal)));
        Assert.Contains(
            "click:579,288",
            automation.Actions);
    }

    [Fact]
    public async Task Select_SlowTopObservationsDoNotConsumeTheNextBoundedDrag()
    {
        FakeAutomation automation = new(
            teamSlot: 1,
            equipmentFixture: "TeamEquipmentConfirm_01.png",
            initialTeamFixture: "TeamList_Aligned_Team2_01.png",
            topDragFixtures:
            [
                "TeamList_Aligned_Team2_01.png",
                "TeamList_Aligned_Team1_Current_01.png",
            ]);
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        automation.CaptureObserved =
            () => now += TimeSpan.FromSeconds(8);
        TeamSelectionService service = new(
            automation,
            () => now,
            static (_, _) => Task.CompletedTask);

        await service.SelectAsync(
            automation.Window,
            teamSlot: 1,
            unitMenuKey: 'u');

        Assert.Equal(
            2,
            automation.Actions.Count(action =>
                action.EndsWith(
                    $",{TeamScreenDetector.TopScrollbarDragLimitY}",
                    StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Select_SlowObservationsDoNotConsumeTheNextTeamAlignmentDrag()
    {
        FakeAutomation automation = new(
            teamSlot: 3,
            equipmentFixture: "TeamEquipmentConfirm_01.png",
            settledDragFixtures:
            [
                "TeamList_Aligned_Team2_01.png",
                "TeamList_Aligned_Team3_01.png",
            ]);
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        automation.CaptureObserved =
            () => now += TimeSpan.FromSeconds(8);
        TeamSelectionService service = new(
            automation,
            () => now,
            static (_, _) => Task.CompletedTask);

        await service.SelectAsync(
            automation.Window,
            teamSlot: 3,
            unitMenuKey: 'u');

        Assert.Equal(
            2,
            automation.Actions.Count(action =>
                action.StartsWith(
                    "drag:",
                    StringComparison.Ordinal)));
        TeamScrollbarThumb thumb =
            TeamScreenDetector.FindScrollbarThumb(
                automation.AlignedTeamFrame)!.Value;
        (int X, int Y) action =
            TeamScreenDetector.AlignedLoadTeamAction(
                automation.AlignedTeamFrame,
                teamSlot: 3,
                thumb.CenterY)!.Value;
        Assert.Contains(
            $"click:{action.X},{action.Y}",
            automation.Actions);
    }

    [Fact]
    public async Task Select_TopNormalizationStopsAfterThreeBoundedDrags()
    {
        FakeAutomation automation = new(
            teamSlot: 1,
            equipmentFixture: "TeamEquipmentConfirm_01.png",
            initialTeamFixture: "TeamList_Aligned_Team2_01.png",
            topDragFixtures:
            [
                "TeamList_Aligned_Team2_01.png",
                "TeamList_Aligned_Team2_01.png",
                "TeamList_Aligned_Team2_01.png",
            ]);
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        TeamSelectionService service = new(
            automation,
            () => now,
            static (_, _) => Task.CompletedTask);

        RobloxUiUnavailableException error =
            await Assert.ThrowsAsync<RobloxUiUnavailableException>(
                () => service.SelectAsync(
                    automation.Window,
                    teamSlot: 1,
                    unitMenuKey: 'u'));

        Assert.Contains(
            "after 3 bounded drag attempts",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            3,
            automation.Actions.Count(action =>
                action.StartsWith(
                    "drag:",
                    StringComparison.Ordinal)));
    }

    private sealed class FakeAutomation : IRobloxAutomation
    {
        private readonly IReadOnlyDictionary<TeamScreenState, ImageFrame> _frames;
        private readonly IReadOnlyList<ImageFrame> _openingFrames;
        private readonly int _teamSlot;
        private readonly ImageFrame _topTeamFrame;
        private readonly Queue<ImageFrame>
            _settledDragFrames;
        private readonly Queue<ImageFrame>
            _topDragFrames;
        private Queue<ImageFrame> _pendingOpeningFrames = [];
        private int _ignoredTeamsTabClicks;

        private ImageFrame _teamFrame;

        public FakeAutomation(
            int teamSlot,
            string equipmentFixture,
            IReadOnlyList<string>? openingFixtures = null,
            string initialTeamFixture =
                "TeamList_Aligned_Team1_Current_01.png",
            IReadOnlyList<string>?
                settledDragFixtures = null,
            IReadOnlyList<string>?
                topDragFixtures = null,
            int ignoredTeamsTabClicks = 0,
            string unitsFixture =
                "TeamUnits_01.png")
        {
            _teamSlot = teamSlot;
            _openingFrames = openingFixtures?.Select(Load).ToArray() ?? [];
            _settledDragFrames = new Queue<ImageFrame>(
                settledDragFixtures?
                    .Select(Load) ??
                []);
            _topDragFrames = new Queue<ImageFrame>(
                topDragFixtures?
                    .Select(Load) ??
                []);
            _topTeamFrame =
                Load("TeamList_Aligned_Team1_Current_01.png");
            InitialTeamFrame = Load(initialTeamFixture);
            AlignedTeamFrame = Load(TeamFixture(teamSlot));
            _teamFrame = InitialTeamFrame;
            _ignoredTeamsTabClicks =
                ignoredTeamsTabClicks;
            _frames = new Dictionary<TeamScreenState, ImageFrame>
            {
                [TeamScreenState.None] = Load("GameModeNegative_01.png"),
                [TeamScreenState.Units] = Load(unitsFixture),
                [TeamScreenState.LoadConfirm] = Load(LoadConfirmFixture(teamSlot)),
                [TeamScreenState.EquipmentConfirm] = Load(equipmentFixture),
            };
            TeamScreenMatch match = TeamScreenDetector.Detect(_frames[TeamScreenState.EquipmentConfirm]);
            EquipmentAction = (match.ActionX!.Value, match.ActionY!.Value);
            match = TeamScreenDetector.Detect(_frames[TeamScreenState.LoadConfirm]);
            LoadConfirmAction = (match.ActionX!.Value, match.ActionY!.Value);
            match = TeamScreenDetector.Detect(_frames[TeamScreenState.Units]);
            UnitsTeamsAction =
                (match.ActionX!.Value, match.ActionY!.Value);
        }

        public RobloxWindow Window { get; } = new((nint)42, "Roblox");

        public ClientBounds Client { get; set; } = new(0, 0, TeamScreenDetector.ClientWidth, TeamScreenDetector.ClientHeight);

        public TeamScreenState State { get; private set; }

        public ImageFrame InitialTeamFrame { get; }

        public ImageFrame AlignedTeamFrame { get; }

        public (int X, int Y) EquipmentAction { get; }

        public (int X, int Y) LoadConfirmAction { get; }

        public (int X, int Y) UnitsTeamsAction { get; }

        public List<string> Actions { get; } = [];

        public int FocusCount { get; private set; }

        public Action? CaptureObserved { get; set; }

        public RobloxWindow? FindWindow(string titleFragment = "Roblox") => Window;

        public RobloxWindow? ForegroundWindow() => Window;

        public ClientBounds GetClientBounds(RobloxWindow window) => Client;

        public WindowBounds GetWindowBounds(RobloxWindow window) => new(0, 0, Client.Width, Client.Height);

        public bool Focus(RobloxWindow window)
        {
            FocusCount++;
            return true;
        }

        public Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds) => throw new NotSupportedException();

        public ImageFrame CaptureScreen(ScreenRegion region) => throw new NotSupportedException();

        public ImageFrame CaptureClient(RobloxWindow window)
        {
            CaptureObserved?.Invoke();
            if (State != TeamScreenState.Teams)
            {
                return _frames[State];
            }

            return _pendingOpeningFrames.Count > 0
                ? _pendingOpeningFrames.Dequeue()
                : _teamFrame;
        }

        public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken)
        {
            Actions.Add("park");
            return Task.CompletedTask;
        }

        public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken)
        {
            Actions.Add($"click:{x},{y}");
            if (State == TeamScreenState.Units &&
                (x, y) == UnitsTeamsAction)
            {
                if (_ignoredTeamsTabClicks > 0)
                {
                    _ignoredTeamsTabClicks--;
                    return Task.CompletedTask;
                }

                _teamFrame = InitialTeamFrame;
                _pendingOpeningFrames = new Queue<ImageFrame>(
                    _openingFrames);
                State = TeamScreenState.Teams;
                return Task.CompletedTask;
            }

            (int X, int Y)? alignedAction =
                State == TeamScreenState.Teams
                    ? TeamScreenDetector.AlignedLoadTeamAction(
                        _teamFrame,
                        _teamSlot,
                        TargetCenterY)
                    : null;
            State = State switch
            {
                TeamScreenState.Teams when alignedAction == (x, y) => TeamScreenState.LoadConfirm,
                TeamScreenState.LoadConfirm when (x, y) == LoadConfirmAction => TeamScreenState.EquipmentConfirm,
                TeamScreenState.EquipmentConfirm when (x, y) == EquipmentAction => TeamScreenState.Teams,
                _ => throw new InvalidOperationException($"Unexpected click ({x}, {y}) from {State}."),
            };
            return Task.CompletedTask;
        }

        public Task DragClientAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            CancellationToken cancellationToken)
        {
            Actions.Add($"drag:{startX},{startY}->{endX},{endY}");
            TeamScrollbarThumb thumb = TeamScreenDetector.FindScrollbarThumb(_teamFrame)!.Value;
            Assert.Equal((thumb.X, thumb.CenterY), (startX, startY));
            Assert.Equal(thumb.X, endX);
            if (endY == TeamScreenDetector.TopScrollbarDragLimitY)
            {
                _teamFrame = _topDragFrames.Count > 0
                    ? _topDragFrames.Dequeue()
                    : _topTeamFrame;
            }
            else if (_settledDragFrames.Count > 0)
            {
                _teamFrame =
                    _settledDragFrames.Dequeue();
            }
            else if (_teamSlot >= 7)
            {
                Assert.Equal(
                    TeamScreenDetector.BottomScrollbarDragLimitY,
                    endY);
                _teamFrame = AlignedTeamFrame;
            }
            else
            {
                Assert.Equal(TargetCenterY, endY);
                _teamFrame = AlignedTeamFrame;
            }
            return Task.CompletedTask;
        }

        public Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Team selection must not wheel-scroll over unit cards.");

        public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task TapShiftLockKeyAsync(RobloxWindow window, int virtualKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken)
        {
            Actions.Add($"key:{key}");
            State = State switch
            {
                TeamScreenState.None => TeamScreenState.Units,
                TeamScreenState.Teams => TeamScreenState.Units,
                TeamScreenState.Units => TeamScreenState.None,
                _ => throw new InvalidOperationException($"Unexpected Unit key from {State}."),
            };
            return Task.CompletedTask;
        }

        public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken) => Task.CompletedTask;

        private int TargetCenterY
        {
            get
            {
                TeamScrollbarThumb top =
                    TeamScreenDetector.FindScrollbarThumb(
                        _topTeamFrame)!.Value;
                return TeamScreenDetector.ScrollThumbTargetCenterY(_teamSlot, top.CenterY);
            }
        }

        private static string TeamFixture(int teamSlot) => teamSlot switch
        {
            1 => "TeamList_Aligned_Team1_Current_01.png",
            2 => "TeamList_Aligned_Team2_01.png",
            3 => "TeamList_Aligned_Team3_01.png",
            4 => "TeamList_Aligned_Team4_01.png",
            5 => "TeamList_Aligned_Team5_01.png",
            6 => "TeamList_Aligned_Team6_01.png",
            7 or 8 => "TeamList_Aligned_Bottom_01.png",
            _ => throw new ArgumentOutOfRangeException(nameof(teamSlot)),
        };

        private static string LoadConfirmFixture(int teamSlot) => teamSlot switch
        {
            1 => "TeamLoadConfirm_Team1_BrightRoster_01.png",
            4 => "TeamLoadConfirm_Team4_TwoRows_01.png",
            7 => "TeamLoadConfirm_Bottom_Team7_01.png",
            8 => "TeamLoadConfirm_Bottom_Team8_01.png",
            _ => "TeamLoadConfirm_01.png",
        };

        private static ImageFrame Load(string name) => ImageCodec.Load(Path.Combine(TestPaths.StageDatasets, name));
    }
}
