using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

internal sealed class TestFrames
{
    public ImageFrame Lobby { get; } =
        Load("LobbyClosed.png");
    public ImageFrame NonLobby { get; } =
        Load("LobbyClosed.png");
    public ImageFrame EventThemeLobby { get; } =
        Load("LobbyEventTheme.png");
    public ImageFrame Scale080 { get; } =
        Load("SettingsScale080.png");
    public ImageFrame Scale120 { get; } =
        Load("SettingsScale120.png");
    public ImageFrame Gameplay { get; } =
        Load("GameplayPage.png");
    public ImageFrame Graphics { get; } =
        Load("GraphicsPageCurrent.png");
    public ImageFrame UnitsTop { get; } =
        Load("UnitsTop.png");
    public ImageFrame UnitsBottom { get; } =
        Load("UnitsBottom.png");
    public ImageFrame Miscellaneous { get; } =
        Load("MiscellaneousPageCurrent.png");

    private static ImageFrame Load(string name) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.SettingsDatasets,
                name));
}

internal sealed class LobbyDetector(
    ImageFrame lobby,
    bool alwaysLobby = false) : IDetectorPack
{
    public DetectorPackManifest Manifest => null!;

    public IReadOnlyDictionary<string, double> ScoreStates(
        ImageFrame clientImage) =>
        new Dictionary<string, double>();

    public string? Classify(
        IReadOnlyDictionary<string, double> scores) =>
        null;

    public string? RecoveryState(
        ImageFrame clientImage) =>
        alwaysLobby ||
        ReferenceEquals(clientImage, lobby)
            ? "lobby"
            : null;

    public string? CurrentNodeType(
        ImageFrame clientImage) =>
        null;

    public int? SelectedMap(
        ImageFrame clientImage) =>
        null;

    public int? SelectedDifficulty(
        ImageFrame clientImage) =>
        null;

    public IReadOnlyList<int> RemainingUnitKeys(
        ImageFrame clientImage,
        IReadOnlySet<int> unitKeys) =>
        [];

    public (int X, int Y) ActionFor(
        string state,
        ImageFrame? clientImage = null) =>
        throw new NotSupportedException();
}
