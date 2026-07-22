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
    [InlineData("TeamEquipmentConfirm_01.png")]
    [InlineData("TeamEquipmentConfirm_Compact_01.png")]
    public async Task Select_LoadsTheRequestedTeamAndClosesBothUnitLayers(string equipmentFixture)
    {
        FakeAutomation automation = new(equipmentFixture);
        TeamSelectionService service = new(automation);

        await service.SelectAsync(automation.Window, teamSlot: 3, unitMenuKey: 'u');

        Assert.Equal(
            [
                "key:U",
                $"click:{TeamScreenDetector.TeamsTabAction.X},{TeamScreenDetector.TeamsTabAction.Y}",
                $"scroll:{TeamScreenDetector.ScrollNotchesForTeam(3)}",
                $"click:{TeamScreenDetector.LoadTeamAction(3).X},{TeamScreenDetector.LoadTeamAction(3).Y}",
                $"click:{automation.LoadConfirmAction.X},{automation.LoadConfirmAction.Y}",
                $"click:{automation.EquipmentAction.X},{automation.EquipmentAction.Y}",
                "park",
                "key:U",
                "key:U",
            ],
            automation.Actions);
        Assert.Equal(TeamScreenState.None, automation.State);
        Assert.True(automation.FocusCount > automation.Actions.Count);
    }

    [Fact]
    public async Task Select_StopsBeforeInputWhenTheClientSizeChanged()
    {
        FakeAutomation automation = new("TeamEquipmentConfirm_01.png") { Client = new ClientBounds(0, 0, 800, 600) };
        TeamSelectionService service = new(automation);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SelectAsync(automation.Window, teamSlot: 1, unitMenuKey: 'u'));

        Assert.Contains("808 by 611", error.Message, StringComparison.Ordinal);
        Assert.Equal(["key:U"], automation.Actions);
    }

    private sealed class FakeAutomation : IRobloxAutomation
    {
        private readonly IReadOnlyDictionary<TeamScreenState, ImageFrame> _frames;

        private int _unitKeyTaps;

        public FakeAutomation(string equipmentFixture)
        {
            _frames = new Dictionary<TeamScreenState, ImageFrame>
            {
                [TeamScreenState.None] = Load("GameModeNegative_01.png"),
                [TeamScreenState.Units] = Load("TeamUnits_01.png"),
                [TeamScreenState.Teams] = Load("TeamList_01.png"),
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

        public ImageFrame CaptureClient(RobloxWindow window) => _frames[State];

        public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken)
        {
            Actions.Add("park");
            return Task.CompletedTask;
        }

        public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken)
        {
            Actions.Add($"click:{x},{y}");
            State = State switch
            {
                TeamScreenState.Units when (x, y) == TeamScreenDetector.TeamsTabAction => TeamScreenState.Teams,
                TeamScreenState.Teams when (x, y) == TeamScreenDetector.LoadTeamAction(3) => TeamScreenState.LoadConfirm,
                TeamScreenState.LoadConfirm when (x, y) == LoadConfirmAction => TeamScreenState.EquipmentConfirm,
                TeamScreenState.EquipmentConfirm when (x, y) == EquipmentAction => TeamScreenState.Teams,
                _ => throw new InvalidOperationException($"Unexpected click ({x}, {y}) from {State}."),
            };
            return Task.CompletedTask;
        }

        public Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken)
        {
            Actions.Add($"scroll:{notches}");
            return Task.CompletedTask;
        }

        public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PulseCameraYawAsync(RobloxWindow window, CameraYawDirection direction, int holdMilliseconds, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task TapLeftControlAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken)
        {
            _unitKeyTaps++;
            Actions.Add($"key:{key}");
            State = _unitKeyTaps switch
            {
                1 => TeamScreenState.Units,
                2 => TeamScreenState.Units,
                _ => TeamScreenState.None,
            };
            return Task.CompletedTask;
        }

        public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken) => Task.CompletedTask;

        private static ImageFrame Load(string name) => ImageCodec.Load(Path.Combine(TestPaths.StageDatasets, name));
    }
}
