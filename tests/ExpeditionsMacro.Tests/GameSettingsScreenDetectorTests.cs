using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
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
    [InlineData(0.98)]
    [InlineData(1.00)]
    [InlineData(1.02)]
    public void CanonicalUiScale_AcceptsInclusiveTwoPercentRange(
        double uiScale) =>
        Assert.True(
            GameSettingsScreenDetector
                .IsCanonicalUiScale(uiScale));

    [Theory]
    [InlineData(0.979999)]
    [InlineData(1.020001)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    public void CanonicalUiScale_RejectsValuesOutsideRange(
        double uiScale) =>
        Assert.False(
            GameSettingsScreenDetector
                .IsCanonicalUiScale(uiScale));

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
    [InlineData(
        "MiscellaneousPageEventUpdate.png",
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
    public void AreasPanel_DoesNotImpersonateSettingsPanel()
    {
        GameSettingsPanelMatch match =
            GameSettingsScreenDetector.DetectPanel(
                ImageCodec.Load(
                    Path.Combine(
                        TestPaths.RefuelDatasets,
                        "AreasLobby_01.png")));

        Assert.False(match.Visible);
        Assert.False(match.Settled);
        Assert.Equal(0, match.CloseX);
        Assert.Equal(0, match.CloseY);
    }

    [Fact]
    public void BrightPageBody_DoesNotHideOwnedSettingsPanel()
    {
        ImageFrame frame = Paint(
            Load("GameplayPage.png"),
            new ScreenRegion(253, 188, 387, 250),
            120,
            120,
            120);

        GameSettingsPanelMatch panel =
            GameSettingsScreenDetector.DetectPanel(frame);

        Assert.True(panel.Visible);
        Assert.True(panel.Settled);
        Assert.Equal(
            GameSettingsPage.Gameplay,
            GameSettingsScreenDetector.DetectPage(frame).Page);
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
            bool fixtureEnabled =
                required.Setting ==
                    RequiredGameSetting.AutoUpgradePlacedUnits ||
                required.Enabled;

            Assert.True(
                match.State ==
                (fixtureEnabled
                    ? GameSettingToggleState.Enabled
                    : GameSettingToggleState.Disabled),
                $"{required.Setting} was {match.State} at {match.Confidence:P0}.");
            Assert.InRange(match.Confidence, 0.72, 1);
        }
    }

    [Fact]
    public void AutoUpgradePlacedUnits_IsDetectedAsWrongAndRequiredOff()
    {
        GameSettingToggleMatch match =
            GameSettingsScreenDetector.DetectToggle(
                Load("UnitsBottom.png"),
                RequiredGameSetting.AutoUpgradePlacedUnits);
        RequiredGameSettingState requirement =
            Assert.Single(
                RequiredGameSettings.Profile,
                entry =>
                    entry.Setting ==
                    RequiredGameSetting.AutoUpgradePlacedUnits);

        Assert.Equal(
            GameSettingToggleState.Enabled,
            match.State);
        Assert.False(requirement.Enabled);
        Assert.InRange(match.Confidence, 0.72, 1);
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
    public void ToggleDetection_SearchesLocalVerticalRasterPhase()
    {
        ImageFrame frame = TranslateVertically(
            Load("GameplayPage.png"),
            offsetY: -2);

        GameSettingToggleMatch match =
            GameSettingsScreenDetector.DetectToggle(
                frame,
                RequiredGameSetting.DisplayPinnedQuests);

        Assert.Equal(
            GameSettingToggleState.Disabled,
            match.State);
        Assert.Equal((436, 293),
            (match.ActionX, match.ActionY));
    }

    [Fact]
    public void MissingToggleColor_RemainsUnknownAcrossLocalSearch()
    {
        ImageFrame frame = Paint(
            Load("GameplayPage.png"),
            new ScreenRegion(428, 283, 17, 21),
            20,
            20,
            20);

        GameSettingToggleMatch match =
            GameSettingsScreenDetector.DetectToggle(
                frame,
                RequiredGameSetting.DisplayPinnedQuests);

        Assert.Equal(
            GameSettingToggleState.Unknown,
            match.State);
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
    public void EventUpdateMiscellaneous_UsesCompactRequiredRow()
    {
        GameSettingToggleMatch updateLog =
            GameSettingsScreenDetector.DetectToggle(
                Load("MiscellaneousPageEventUpdate.png"),
                RequiredGameSetting
                    .DisplayUpdateLogOnLogin);
        GameSettingToggleMatch autoSprint =
            GameSettingsScreenDetector.DetectToggle(
                Load("MiscellaneousPageEventUpdate.png"),
                RequiredGameSetting.AutoSprint);

        Assert.Equal(
            GameSettingToggleState.Disabled,
            updateLog.State);
        Assert.Equal(
            GameSettingToggleState.Enabled,
            autoSprint.State);
        Assert.Equal(350, updateLog.ActionY);
        Assert.Equal(350, autoSprint.ActionY);
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

    [Fact]
    public void UnitsScrollbar_AcceptsReviewedBrightBlueFieldVariant()
    {
        ImageFrame frame = Load(
            "UnitsTopBrightScrollbar.png");

        GameSettingsScrollbarThumb thumb =
            GameSettingsScreenDetector
                .FindUnitsScrollbarThumb(frame)!.Value;

        Assert.True(thumb.IsAtTop);
        Assert.InRange(thumb.X, 665, 671);
        Assert.InRange(thumb.EndY - thumb.StartY + 1, 120, 220);
    }

    [Theory]
    [InlineData("UnitsTop.png", 277, true)]
    [InlineData("UnitsBottom.png", 360, false)]
    public void UnitsScrollbar_ToleratesOneMissingRasterRow(
        string fileName,
        int missingY,
        bool expectedTop)
    {
        ImageFrame frame = Paint(
            Load(fileName),
            new ScreenRegion(665, missingY, 7, 1),
            20,
            20,
            20);

        GameSettingsScrollbarThumb thumb =
            GameSettingsScreenDetector
                .FindUnitsScrollbarThumb(frame)!.Value;

        Assert.Equal(expectedTop, thumb.IsAtTop);
        Assert.Equal(!expectedTop, thumb.IsAtBottom);
    }

    private static ImageFrame Paint(
        ImageFrame source,
        ScreenRegion region,
        byte red,
        byte green,
        byte blue)
    {
        byte[] pixels = source.Pixels.ToArray();
        for (int y = region.Y;
             y < region.Bottom;
             y++)
        {
            for (int x = region.X;
                 x < region.Right;
                 x++)
            {
                int pixel =
                    (y * source.Width + x) * 3;
                pixels[pixel] = red;
                pixels[pixel + 1] = green;
                pixels[pixel + 2] = blue;
            }
        }

        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }

    private static ImageFrame TranslateVertically(
        ImageFrame source,
        int offsetY)
    {
        byte[] pixels = new byte[source.Pixels.Length];
        int sourceStartY = Math.Max(0, -offsetY);
        int destinationStartY = Math.Max(0, offsetY);
        int rows = source.Height - Math.Abs(offsetY);
        int rowBytes = source.Width * 3;
        for (int row = 0; row < rows; row++)
        {
            Array.Copy(
                source.Pixels,
                (sourceStartY + row) * rowBytes,
                pixels,
                (destinationStartY + row) * rowBytes,
                rowBytes);
        }

        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }

    private sealed class ImageFramePair
    {
        public ImageFramePair(string fileName) =>
            Frame = Load(fileName);

        public ImageFrame Frame { get; }
    }

    private static ImageFrame Load(
        string name) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.SettingsDatasets,
                name));
}
