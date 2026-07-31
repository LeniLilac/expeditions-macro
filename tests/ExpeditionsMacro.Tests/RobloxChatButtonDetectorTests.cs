using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Navigation;

namespace ExpeditionsMacro.Tests;

public sealed class RobloxChatButtonDetectorTests
{
    [Theory]
    [InlineData(
        "ChatClosed.png",
        RobloxChatButtonState.Closed)]
    [InlineData(
        "ChatOpen.png",
        RobloxChatButtonState.Open)]
    public void ReviewedIndicators_ReportTheLiveChatState(
        string fileName,
        RobloxChatButtonState expected)
    {
        RobloxChatButtonMatch match =
            RobloxChatButtonDetector.Detect(
                Load(fileName));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.Confidence, 0.98, 1);
        Assert.Equal(
            RobloxChatButtonDetector.ActionX,
            match.ActionX);
        Assert.Equal(
            RobloxChatButtonDetector.ActionY,
            match.ActionY);
    }

    [Fact]
    public void MissingChatGlyph_IsNotActionable()
    {
        ImageFrame frame = new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3]);

        RobloxChatButtonMatch match =
            RobloxChatButtonDetector.Detect(frame);

        Assert.Equal(
            RobloxChatButtonState.None,
            match.State);
        Assert.False(match.Available);
        Assert.Equal(0, match.ActionX);
        Assert.Equal(0, match.ActionY);
    }

    [Fact]
    public void AdjacentOpaqueControls_AreNotChatEvidence()
    {
        ImageFrame source = Load("ChatClosed.png");
        byte[] pixels = source.Pixels.ToArray();
        for (int y = 10; y < 58; y++)
        {
            for (int x = 163; x < 300; x++)
            {
                int pixel =
                    (y * source.Width + x) * 3;
                pixels[pixel] = 255;
                pixels[pixel + 1] = 255;
                pixels[pixel + 2] = 255;
            }
        }
        ImageFrame changed = new(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);

        Assert.Equal(
            RobloxChatButtonState.Closed,
            RobloxChatButtonDetector
                .Detect(changed)
                .State);
    }

    [Theory]
    [InlineData(
        "challenges/ChallengeList/ChallengeList_12.png")]
    [InlineData(
        "events/Act1Detail.png")]
    [InlineData(
        "events/EventCatalog_BeginnerPathSelected.png")]
    [InlineData(
        "expeditions/Lobby_UI2/Lobby_UI2_001.png")]
    [InlineData(
        "expeditions/Expedition_Map_Select_Selection_Regression/Map1_GreenLightingVerticalPhase.png")]
    [InlineData(
        "stages/TeamLoadConfirm_Team1_BrightRoster_01.png")]
    public void ReviewedFullFrameOutlines_RemainClosed(
        string relativePath)
    {
        RobloxChatButtonMatch match =
            RobloxChatButtonDetector.Detect(
                LoadCorpus(relativePath));

        Assert.Equal(
            RobloxChatButtonState.Closed,
            match.State);
    }

    [Fact]
    public void MicrophoneAtTheChatPosition_IsNotChatEvidence()
    {
        ImageFrame image = ImageCodec.Load(
            Path.Combine(
                TestPaths.StageDatasets,
                "RaidDetail_Current_CustomFont_01.png"));

        RobloxChatButtonMatch match =
            RobloxChatButtonDetector.Detect(image);

        Assert.Equal(
            RobloxChatButtonState.None,
            match.State);
        Assert.False(match.Available);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void CrossStateCorpus_DoesNotInventAnOpenChat()
    {
        string openFixture = Path.GetFullPath(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                "ChatOpen.png"));
        string[] falseOpen = CorpusPngs()
            .Where(file =>
                !Path.GetFullPath(file).Equals(
                    openFixture,
                    StringComparison.OrdinalIgnoreCase))
            .Where(file =>
                RobloxChatButtonDetector
                    .Detect(ImageCodec.Load(file))
                    .State == RobloxChatButtonState.Open)
            .Select(file =>
                Path.GetRelativePath(
                    TestPaths.RepositoryRoot,
                    file))
            .ToArray();

        Assert.Empty(falseOpen);
    }

    private static ImageFrame LoadCorpus(
        string relativePath) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.RepositoryRoot,
                "datasets",
                "anime-expeditions",
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));

    private static IEnumerable<string> CorpusPngs()
        =>
        Directory.EnumerateFiles(
            Path.Combine(
                TestPaths.RepositoryRoot,
                "datasets",
                "anime-expeditions"),
            "*.png",
            SearchOption.AllDirectories);

    private static ImageFrame Load(
        string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                fileName));
}
