using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class StageOptionSelectionDetectorTests
{
    [Theory]
    [InlineData(
        "StoryDetail_01.png",
        StoryRunKind.Act,
        1,
        false)]
    [InlineData(
        "StoryDetail_Act_Wide_01.png",
        StoryRunKind.Act,
        4,
        true)]
    [InlineData(
        "StoryDetail_Infinite_Wide_01.png",
        StoryRunKind.Infinite,
        0,
        null)]
    [InlineData(
        "StoryDetail_Mastery_01.png",
        StoryRunKind.Mastery,
        0,
        null)]
    [InlineData(
        "StoryDetail_Mastery_Wide_01.png",
        StoryRunKind.Mastery,
        0,
        null)]
    public void ReviewedStoryDetails_ReportSelectedOption(
        string fileName,
        StoryRunKind expectedKind,
        int expectedAct,
        bool? expectedHardMode)
    {
        StoryOptionSelectionMatch match =
            StageOptionSelectionDetector.DetectStory(
                Load(fileName));

        Assert.True(match.Matches(
            expectedKind,
            expectedAct,
            expectedHardMode));
        Assert.InRange(match.Confidence, 0.40, 1);
        Assert.NotNull(match.ActionX);
        Assert.NotNull(match.ActionY);
    }

    [Theory]
    [InlineData(
        "StoryDetail_01.png",
        StoryRunKind.Act,
        1,
        false)]
    [InlineData(
        "StoryDetail_Act_Wide_01.png",
        StoryRunKind.Act,
        4,
        true)]
    [InlineData(
        "StoryDetail_Infinite_Wide_01.png",
        StoryRunKind.Infinite,
        0,
        null)]
    [InlineData(
        "StoryDetail_Mastery_Wide_01.png",
        StoryRunKind.Mastery,
        0,
        null)]
    public void StoryDetails_RejectEveryWrongOptionRow(
        string fileName,
        StoryRunKind selectedKind,
        int selectedAct,
        bool? selectedHardMode)
    {
        StoryOptionSelectionMatch match =
            StageOptionSelectionDetector.DetectStory(
                Load(fileName));

        foreach (
            (StoryRunKind Kind, int Act) candidate in
            StoryRequirements())
        {
            bool expected =
                candidate.Kind == selectedKind &&
                (candidate.Kind != StoryRunKind.Act ||
                 candidate.Act == selectedAct);
            Assert.Equal(
                expected,
                match.Matches(
                    candidate.Kind,
                    candidate.Act,
                    expectedHardMode: null));
        }

        if (selectedKind == StoryRunKind.Act)
        {
            Assert.True(match.Matches(
                selectedKind,
                selectedAct,
                selectedHardMode));
            Assert.False(match.Matches(
                selectedKind,
                selectedAct,
                !selectedHardMode!.Value));
        }
    }

    [Theory]
    [InlineData("RaidDetail_01.png")]
    [InlineData("RaidDetail_Current_CustomFont_01.png")]
    [InlineData("RaidDetail_Current_DefaultFont_01.png")]
    public void ReviewedRaidDetails_ReportOnlySelectedAct(
        string fileName)
    {
        RaidOptionSelectionMatch match =
            StageOptionSelectionDetector.DetectRaid(
                Load(fileName));

        Assert.True(match.Matches(RaidAct.Act1));
        Assert.False(match.Matches(RaidAct.Act2));
        Assert.False(match.Matches(RaidAct.Act3));
        Assert.InRange(match.Confidence, 0.65, 1);
        Assert.NotNull(match.ActionX);
        Assert.NotNull(match.ActionY);
    }

    [Theory]
    [InlineData("StorySelector_01.png")]
    [InlineData("RaidSelector_01.png")]
    [InlineData("StoryPartyPreview_Mastery_01.png")]
    [InlineData("RaidPartyPreview_01.png")]
    [InlineData("GameModeNegative_01.png")]
    public void NonDetailScreens_HaveNoSelectedOption(
        string fileName)
    {
        ImageFrame image = Load(fileName);

        Assert.Null(
            StageOptionSelectionDetector
                .DetectStory(image)
                .RunKind);
        Assert.Null(
            StageOptionSelectionDetector
                .DetectRaid(image)
                .Act);
    }

    [Fact]
    public void StoryRailCoverageSplitAcrossAdjacentColumns_RetainsSelectedOption()
    {
        ImageFrame image = Load("StoryDetail_01.png").Clone();
        SplitRailCoverage(image, railX: 124);

        StoryOptionSelectionMatch match =
            StageOptionSelectionDetector.DetectStory(image);

        Assert.True(match.Matches(
            StoryRunKind.Act,
            expectedActNumber: 1,
            expectedHardMode: false));
        Assert.NotNull(match.ActionX);
        Assert.NotNull(match.ActionY);
    }

    [Fact]
    public void RaidRailCoverageSplitAcrossAdjacentColumns_RetainsSelectedAct()
    {
        ImageFrame image = Load("RaidDetail_01.png").Clone();
        SplitRailCoverage(image, railX: 124);

        RaidOptionSelectionMatch match =
            StageOptionSelectionDetector.DetectRaid(image);

        Assert.True(match.Matches(RaidAct.Act1));
        Assert.NotNull(match.ActionX);
        Assert.NotNull(match.ActionY);
    }

    [Fact]
    public void SplitRailCoverage_WithAmbiguousRows_DoesNotAuthorizeAnOption()
    {
        ImageFrame image = Load("StoryDetail_01.png").Clone();
        SplitRailCoverage(image, railX: 124);
        CopyRegion(
            image,
            sourceX: 128,
            sourceY: 189,
            targetX: 128,
            targetY: 228,
            width: 42,
            height: 24);

        StoryOptionSelectionMatch match =
            StageOptionSelectionDetector.DetectStory(image);

        Assert.Null(match.RunKind);
        Assert.Null(match.ActionX);
        Assert.Null(match.ActionY);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void OptionSelection_DoesNotStealOtherCapturedScreens()
    {
        string[] roots =
        [
            TestPaths.Datasets,
            TestPaths.ChallengeDatasets,
            TestPaths.StageDatasets,
            TestPaths.NavigationVariantDatasets,
            TestPaths.EventDatasets,
            TestPaths.RefuelDatasets,
            TestPaths.SettingsDatasets,
            TestPaths.BountyDatasets,
        ];
        List<string> failures = [];
        foreach (string file in roots
                     .Where(Directory.Exists)
                     .SelectMany(root =>
                         Directory.EnumerateFiles(
                             root,
                             "*.png",
                             SearchOption.AllDirectories)))
        {
            string fileName =
                Path.GetFileNameWithoutExtension(file);
            if (fileName.Contains(
                    "StoryDetail",
                    StringComparison.Ordinal) ||
                fileName.Contains(
                    "RaidDetail",
                    StringComparison.Ordinal))
            {
                continue;
            }

            ImageFrame image = ImageCodec.Load(file);
            if (image.Width != 808 ||
                image.Height != 611)
            {
                continue;
            }

            StoryOptionSelectionMatch story =
                StageOptionSelectionDetector.DetectStory(image);
            RaidOptionSelectionMatch raid =
                StageOptionSelectionDetector.DetectRaid(image);
            if (story.RunKind is not null ||
                raid.Act is not null)
            {
                failures.Add(
                    $"{Path.GetRelativePath(TestPaths.RepositoryRoot, file)}: Story={story.RunKind}, Raid={raid.Act}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Stage option false matches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [Fact]
    public async Task UnchangedPreClickFrames_NeverAuthorizeWrongRoute()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;
        int clicks = 0;
        int observations = 0;
        StoryOptionSelectionMatch remembered =
            Story(
                act: 1,
                hardMode: false);

        StageOptionSelectionWaitResult<StoryObservation>
            result =
                await StageOptionSelectionWaiter
                    .ClickOnceAndWaitAsync(
                        _ =>
                        {
                            clicks++;
                            return Task.CompletedTask;
                        },
                        stableDetections: 2,
                        observe: () =>
                        {
                            observations++;
                            return new StoryObservation(
                                remembered,
                                null);
                        },
                        matches: observation =>
                            observation.Selection.Matches(
                                StoryRunKind.Act,
                                4,
                                expectedHardMode: true),
                        actionFor: observation =>
                            SelectionAction(
                                observation.Selection),
                        interruptionFor: observation =>
                            observation.Recovery,
                        timeout: TimeSpan.FromSeconds(3),
                        pollInterval: TimeSpan.Zero,
                        CancellationToken.None,
                        utcNow: () => now,
                        delay: (_, _) =>
                        {
                            now += TimeSpan.FromSeconds(50);
                            return Task.CompletedTask;
                        });

        Assert.False(result.Succeeded);
        Assert.Equal(1, clicks);
        Assert.Equal(1, observations);
    }

    [Fact]
    public async Task DelayedTargetSelection_ExtendsPendingProofPastSoftBoundary()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;
        int clicks = 0;
        Queue<StoryObservation> observations = new(
        [
            new(Story(1, false), null),
            new(Story(1, false), null),
            new(Story(4, true), null),
            new(Story(4, true), null),
            new(Story(4, true), null),
        ]);

        StageOptionSelectionWaitResult<StoryObservation>
            result =
                await StageOptionSelectionWaiter
                    .ClickOnceAndWaitAsync(
                        _ =>
                        {
                            clicks++;
                            return Task.CompletedTask;
                        },
                        stableDetections: 3,
                        observe: observations.Dequeue,
                        matches: observation =>
                            observation.Selection.Matches(
                                StoryRunKind.Act,
                                4,
                                expectedHardMode: true),
                        actionFor: observation =>
                            SelectionAction(
                                observation.Selection),
                        interruptionFor: observation =>
                            observation.Recovery,
                        timeout: TimeSpan.FromSeconds(12),
                        pollInterval: TimeSpan.Zero,
                        CancellationToken.None,
                        utcNow: () => now,
                        delay: (_, _) =>
                        {
                            now += TimeSpan.FromSeconds(8);
                            return Task.CompletedTask;
                        });

        Assert.True(result.Succeeded);
        Assert.Equal((360, 430), (
            result.ActionX,
            result.ActionY));
        Assert.Equal(1, clicks);
        Assert.Empty(observations);
    }

    [Fact]
    public async Task WrongDifficulty_DoesNotAuthorizeSelectStage()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;
        int clicks = 0;
        StoryOptionSelectionMatch wrongDifficulty =
            Story(
                act: 4,
                hardMode: false);

        StageOptionSelectionWaitResult<StoryObservation>
            result =
                await StageOptionSelectionWaiter
                    .ClickOnceAndWaitAsync(
                        _ =>
                        {
                            clicks++;
                            return Task.CompletedTask;
                        },
                        stableDetections: 2,
                        observe: () =>
                            new StoryObservation(
                                wrongDifficulty,
                                null),
                        matches: observation =>
                            observation.Selection.Matches(
                                StoryRunKind.Act,
                                4,
                                expectedHardMode: true),
                        actionFor: observation =>
                            SelectionAction(
                                observation.Selection),
                        interruptionFor: observation =>
                            observation.Recovery,
                        timeout: TimeSpan.FromSeconds(3),
                        pollInterval: TimeSpan.Zero,
                        CancellationToken.None,
                        utcNow: () => now,
                        delay: (_, _) =>
                        {
                            now += TimeSpan.FromSeconds(50);
                            return Task.CompletedTask;
                        });

        Assert.False(result.Succeeded);
        Assert.Equal(1, clicks);
        Assert.Null(result.ActionX);
        Assert.Null(result.ActionY);
    }

    [Fact]
    public async Task RunSelectionProof_CanPrecedeDifficultyAction()
    {
        int clicks = 0;
        Queue<StoryObservation> observations = new(
        [
            new(Story(4, false), null),
            new(Story(4, false), null),
        ]);

        StageOptionSelectionWaitResult<StoryObservation>
            result =
                await StageOptionSelectionWaiter
                    .ClickOnceAndWaitAsync(
                        _ =>
                        {
                            clicks++;
                            return Task.CompletedTask;
                        },
                        stableDetections: 2,
                        observe: observations.Dequeue,
                        matches: observation =>
                            observation.Selection.Matches(
                                StoryRunKind.Act,
                                4,
                                expectedHardMode: null),
                        actionFor: null,
                        interruptionFor: observation =>
                            observation.Recovery,
                        timeout: TimeSpan.FromSeconds(12),
                        pollInterval: TimeSpan.Zero,
                        CancellationToken.None,
                        delay: static (_, _) =>
                            Task.CompletedTask);

        Assert.True(result.Succeeded);
        Assert.Equal(1, clicks);
        Assert.Null(result.ActionX);
        Assert.Null(result.ActionY);
    }

    [Fact]
    public async Task StableRecovery_InterruptsWithoutAnotherClick()
    {
        int clicks = 0;
        Queue<StoryObservation> observations = new(
        [
            new(Story(1, false), "lobby"),
            new(Story(1, false), "lobby"),
        ]);

        StageOptionSelectionWaitResult<StoryObservation>
            result =
                await StageOptionSelectionWaiter
                    .ClickOnceAndWaitAsync(
                        _ =>
                        {
                            clicks++;
                            return Task.CompletedTask;
                        },
                        stableDetections: 2,
                        observe: observations.Dequeue,
                        matches: observation =>
                            observation.Selection.Matches(
                                StoryRunKind.Act,
                                4,
                                expectedHardMode: true),
                        actionFor: observation =>
                            SelectionAction(
                                observation.Selection),
                        interruptionFor: observation =>
                            observation.Recovery,
                        timeout: TimeSpan.FromSeconds(12),
                        pollInterval: TimeSpan.Zero,
                        CancellationToken.None,
                        delay: static (_, _) =>
                            Task.CompletedTask);

        Assert.False(result.Succeeded);
        Assert.Equal("lobby", result.Interruption);
        Assert.Equal(1, clicks);
        Assert.Empty(observations);
    }

    private static IEnumerable<(
        StoryRunKind Kind,
        int Act)> StoryRequirements()
    {
        for (int act = 1; act <= 5; act++)
        {
            yield return (StoryRunKind.Act, act);
        }
        yield return (StoryRunKind.Infinite, 0);
        yield return (StoryRunKind.Mastery, 0);
    }

    private static StoryOptionSelectionMatch Story(
        int act,
        bool hardMode) =>
        new(
            StoryRunKind.Act,
            act,
            hardMode,
            0.90,
            360,
            430);

    private static (int X, int Y)? SelectionAction(
        StoryOptionSelectionMatch match) =>
        match.ActionX is int x &&
        match.ActionY is int y
            ? (x, y)
            : null;

    private static void SplitRailCoverage(
        ImageFrame image,
        int railX)
    {
        for (int y = 150; y < 470; y++)
        {
            int source =
                (y * image.Width + railX) * 3;
            byte red = image.Pixels[source];
            byte green = image.Pixels[source + 1];
            byte blue = image.Pixels[source + 2];
            int targetX =
                railX + (y & 1);
            FillPixel(
                image,
                railX,
                y,
                red: 12,
                green: 12,
                blue: 12);
            FillPixel(
                image,
                railX + 1,
                y,
                red: 12,
                green: 12,
                blue: 12);
            FillPixel(
                image,
                targetX,
                y,
                red,
                green,
                blue);
        }
    }

    private static void CopyRegion(
        ImageFrame image,
        int sourceX,
        int sourceY,
        int targetX,
        int targetY,
        int width,
        int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int source =
                    ((sourceY + y) * image.Width +
                     sourceX + x) * 3;
                FillPixel(
                    image,
                    targetX + x,
                    targetY + y,
                    image.Pixels[source],
                    image.Pixels[source + 1],
                    image.Pixels[source + 2]);
            }
        }
    }

    private static void FillPixel(
        ImageFrame image,
        int x,
        int y,
        byte red,
        byte green,
        byte blue)
    {
        int pixel =
            (y * image.Width + x) * 3;
        image.Pixels[pixel] = red;
        image.Pixels[pixel + 1] = green;
        image.Pixels[pixel + 2] = blue;
    }

    private static ImageFrame Load(string name) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.StageDatasets,
                name));

    private sealed record StoryObservation(
        StoryOptionSelectionMatch Selection,
        string? Recovery);
}
