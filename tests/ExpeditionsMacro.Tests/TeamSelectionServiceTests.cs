using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
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
            $"click:{TeamScreenDetector.TeamsTabAction.X},{TeamScreenDetector.TeamsTabAction.Y}",
        ];
        TeamScrollbarThumb initialThumb = TeamScreenDetector.FindScrollbarThumb(automation.InitialTeamFrame)!.Value;
        if (teamSlot != 1)
        {
            expected.Add(
                $"drag:{initialThumb.X},{initialThumb.CenterY}->{initialThumb.X},{TeamScreenDetector.ScrollThumbTargetCenterY(teamSlot, initialThumb.CenterY)}");
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

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SelectAsync(automation.Window, teamSlot: 1, unitMenuKey: 'u'));

        Assert.Contains("808 by 611", error.Message, StringComparison.Ordinal);
        Assert.Equal(["key:U"], automation.Actions);
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

    private sealed class FakeAutomation : IRobloxAutomation
    {
        private readonly IReadOnlyDictionary<TeamScreenState, ImageFrame> _frames;
        private readonly int _teamSlot;

        private ImageFrame _teamFrame;

        public FakeAutomation(int teamSlot, string equipmentFixture)
        {
            _teamSlot = teamSlot;
            InitialTeamFrame = Load("TeamList_Aligned_Team1_Current_01.png");
            AlignedTeamFrame = Load(TeamFixture(teamSlot));
            _teamFrame = InitialTeamFrame;
            _frames = new Dictionary<TeamScreenState, ImageFrame>
            {
                [TeamScreenState.None] = Load("GameModeNegative_01.png"),
                [TeamScreenState.Units] = Load("TeamUnits_01.png"),
                [TeamScreenState.LoadConfirm] = Load("TeamLoadConfirm_01.png"),
                [TeamScreenState.EquipmentConfirm] = Load(equipmentFixture),
            };
            TeamScreenMatch match = TeamScreenDetector.Detect(_frames[TeamScreenState.EquipmentConfirm]);
            EquipmentAction = (match.ActionX!.Value, match.ActionY!.Value);
            match = TeamScreenDetector.Detect(_frames[TeamScreenState.LoadConfirm]);
            LoadConfirmAction = (match.ActionX!.Value, match.ActionY!.Value);
        }

        public RobloxWindow Window { get; } = new((nint)42, "Roblox");

        public ClientBounds Client { get; set; } = new(0, 0, TeamScreenDetector.ClientWidth, TeamScreenDetector.ClientHeight);

        public TeamScreenState State { get; private set; }

        public ImageFrame InitialTeamFrame { get; }

        public ImageFrame AlignedTeamFrame { get; }

        public (int X, int Y) EquipmentAction { get; }

        public (int X, int Y) LoadConfirmAction { get; }

        public List<string> Actions { get; } = [];

        public int FocusCount { get; private set; }

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

        public ImageFrame CaptureClient(RobloxWindow window) =>
            State == TeamScreenState.Teams ? _teamFrame : _frames[State];

        public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken)
        {
            Actions.Add("park");
            return Task.CompletedTask;
        }

        public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken)
        {
            Actions.Add($"click:{x},{y}");
            if (State == TeamScreenState.Units && (x, y) == TeamScreenDetector.TeamsTabAction)
            {
                _teamFrame = InitialTeamFrame;
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
            Assert.Equal(TargetCenterY, endY);
            _teamFrame = AlignedTeamFrame;
            return Task.CompletedTask;
        }

        public Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Team selection must not wheel-scroll over unit cards.");

        public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PulseCameraYawAsync(RobloxWindow window, CameraYawDirection direction, int holdMilliseconds, CancellationToken cancellationToken) => Task.CompletedTask;

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
                TeamScrollbarThumb top = TeamScreenDetector.FindScrollbarThumb(InitialTeamFrame)!.Value;
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

        private static ImageFrame Load(string name) => ImageCodec.Load(Path.Combine(TestPaths.StageDatasets, name));
    }
}
