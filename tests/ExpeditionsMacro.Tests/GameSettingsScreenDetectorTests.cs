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
        "GraphicsPageCurrent.png",
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
    [InlineData(
        "MiscellaneousPageCurrent.png",
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

    [Theory]
    [InlineData("LobbyClosed.png")]
    [InlineData("LobbyEventTheme.png")]
    public void Lobby_DoesNotMatchTheSettingsPanel(
        string fileName)
    {
        GameSettingsPanelMatch match =
            GameSettingsScreenDetector.DetectPanel(
                Load(fileName));

        Assert.False(match.Visible);
        Assert.Equal(0, match.CloseX);
        Assert.Equal(0, match.CloseY);
        Assert.Equal(
            GameSettingsPage.None,
            GameSettingsScreenDetector.DetectPage(
                Load(fileName)).Page);
        Assert.Equal(
            StageScreenState.None,
            StageScreenDetector.Detect(
                Load(fileName)).State);
        Assert.Equal(
            TeamScreenState.None,
            TeamScreenDetector.Detect(
                Load(fileName)).State);
        Assert.Equal(
            AreasScreenState.None,
            AreasScreenDetector.Detect(
                Load(fileName)).State);
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
                    new("GraphicsPageCurrent.png"),
                [GameSettingsPage.Miscellaneous] =
                    new("MiscellaneousPageCurrent.png"),
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
    public void CurrentGraphics_RequiresEventThemeOff()
    {
        GameSettingToggleMatch match =
            GameSettingsScreenDetector.DetectToggle(
                Load("GraphicsPageCurrent.png"),
                RequiredGameSetting.EventThemeEnabled);

        Assert.Equal(
            GameSettingToggleState.Disabled,
            match.State);
        Assert.InRange(match.Confidence, 0.72, 1);
        Assert.Equal((436, 293),
            (match.ActionX, match.ActionY));
    }

    [Fact]
    public void CurrentMiscellaneous_UsesShiftedRequiredRow()
    {
        GameSettingToggleMatch updateLog =
            GameSettingsScreenDetector.DetectToggle(
                Load("MiscellaneousPageCurrent.png"),
                RequiredGameSetting
                    .DisplayUpdateLogOnLogin);
        GameSettingToggleMatch autoSprint =
            GameSettingsScreenDetector.DetectToggle(
                Load("MiscellaneousPageCurrent.png"),
                RequiredGameSetting.AutoSprint);

        Assert.Equal(
            GameSettingToggleState.Disabled,
            updateLog.State);
        Assert.Equal(
            GameSettingToggleState.Enabled,
            autoSprint.State);
        Assert.Equal(364, updateLog.ActionY);
        Assert.Equal(364, autoSprint.ActionY);
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
