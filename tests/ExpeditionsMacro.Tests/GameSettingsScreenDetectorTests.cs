using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Refuel;
using ExpeditionsMacro.Vision.Settings;
using ExpeditionsMacro.Vision.Stages;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Tests;

public sealed class GameSettingsScreenDetectorTests
{
    [Theory]
    [InlineData("SettingsScale080.png", 0.78, 0.82)]
    [InlineData("SettingsScale100.png", 0.98, 1.02)]
    [InlineData("SettingsScale120.png", 1.17, 1.22)]
    public void StableSettingsPanels_ReportTheirUiScale(
        string fileName,
        double minimum,
        double maximum)
    {
        GameSettingsPanelMatch match =
            GameSettingsScreenDetector.DetectPanel(
                Load(fileName));

        Assert.True(match.Visible);
        Assert.True(match.Settled);
        Assert.InRange(match.Confidence, 0.72, 1);
        Assert.InRange(match.UiScale, minimum, maximum);
    }

    [Theory]
    [InlineData(
        "GameplayPage.png",
        GameSettingsPage.Gameplay)]
    [InlineData(
        "GraphicsPage.png",
        GameSettingsPage.Graphics)]
    [InlineData(
        "UnitsTop.png",
        GameSettingsPage.Units)]
    [InlineData(
        "UnitsBottom.png",
        GameSettingsPage.Units)]
    [InlineData(
        "MiscellaneousPage.png",
        GameSettingsPage.Miscellaneous)]
    public void CanonicalPages_ReportTheirSelectedTab(
        string fileName,
        GameSettingsPage expected)
    {
        GameSettingsPageMatch match =
            GameSettingsScreenDetector.DetectPage(
                Load(fileName));

        Assert.Equal(expected, match.Page);
        Assert.InRange(match.Confidence, 0.65, 1);
    }

    [Fact]
    public void OpeningAnimation_IsNotTreatedAsAStablePanel()
    {
        GameSettingsPanelMatch match =
            GameSettingsScreenDetector.DetectPanel(
                Load("SettingsOpeningTransition.png"));

        Assert.False(match.Visible && match.Settled);
        Assert.Equal(
            GameSettingsPage.None,
            GameSettingsScreenDetector.DetectPage(
                Load("SettingsOpeningTransition.png")).Page);
    }

    [Fact]
    public void Lobby_DoesNotMatchTheSettingsPanel()
    {
        GameSettingsPanelMatch match =
            GameSettingsScreenDetector.DetectPanel(
                Load("LobbyClosed.png"));

        Assert.False(match.Visible);
        Assert.Equal(
            GameSettingsPage.None,
            GameSettingsScreenDetector.DetectPage(
                Load("LobbyClosed.png")).Page);
        Assert.Equal(
            StageScreenState.None,
            StageScreenDetector.Detect(
                Load("LobbyClosed.png")).State);
        Assert.Equal(
            TeamScreenState.None,
            TeamScreenDetector.Detect(
                Load("LobbyClosed.png")).State);
        Assert.Equal(
            AreasScreenState.None,
            AreasScreenDetector.Detect(
                Load("LobbyClosed.png")).State);
    }

    [Fact]
    public void ReviewedProfileFixtures_ExposeEveryRequiredState()
    {
        Dictionary<GameSettingsPage, ImageFramePair> pages =
            new()
            {
                [GameSettingsPage.Gameplay] =
                    new("GameplayPage.png"),
                [GameSettingsPage.Graphics] =
                    new("GraphicsPage.png"),
                [GameSettingsPage.Miscellaneous] =
                    new("MiscellaneousPage.png"),
            };
        ImageFramePair unitsTop = new("UnitsTop.png");
        ImageFramePair unitsBottom = new("UnitsBottom.png");

        foreach (RequiredGameSettingState required in
                 RequiredGameSettings.Profile)
        {
            GameSettingsPage page =
                GameSettingsScreenDetector.PageFor(
                    required.Setting);
            ImageFramePair fixture =
                page == GameSettingsPage.Units
                    ? GameSettingsScreenDetector
                        .RequiresUnitsBottom(required.Setting)
                        ? unitsBottom
                        : unitsTop
                    : pages[page];
            GameSettingToggleMatch match =
                GameSettingsScreenDetector.DetectToggle(
                    fixture.Frame,
                    required.Setting);

            Assert.True(
                match.State ==
                (required.Enabled
                    ? GameSettingToggleState.Enabled
                    : GameSettingToggleState.Disabled),
                $"{required.Setting} was {match.State} at {match.Confidence:P0}.");
            Assert.InRange(match.Confidence, 0.72, 1);
        }
    }

    [Fact]
    public void UnitsScrollbar_IdentifiesTopAndBottomClamps()
    {
        GameSettingsScrollbarThumb top =
            GameSettingsScreenDetector
                .FindUnitsScrollbarThumb(
                    Load("UnitsTop.png"))!.Value;
        GameSettingsScrollbarThumb bottom =
            GameSettingsScreenDetector
                .FindUnitsScrollbarThumb(
                    Load("UnitsBottom.png"))!.Value;

        Assert.True(top.IsAtTop);
        Assert.False(top.IsAtBottom);
        Assert.False(bottom.IsAtTop);
        Assert.True(bottom.IsAtBottom);
        Assert.InRange(
            Math.Abs(
                (top.EndY - top.StartY) -
                (bottom.EndY - bottom.StartY)),
            0,
            4);
    }

    private sealed class ImageFramePair
    {
        public ImageFramePair(string fileName) =>
            Frame = Load(fileName);

        public ExpeditionsMacro.Core.Imaging.ImageFrame Frame { get; }
    }

    private static ExpeditionsMacro.Core.Imaging.ImageFrame Load(
        string name) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.SettingsDatasets,
                name));
}
