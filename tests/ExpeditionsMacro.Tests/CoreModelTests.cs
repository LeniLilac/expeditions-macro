using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class CoreModelTests
{
    [Fact]
    public void ScreenRegion_UsesHalfOpenBoundsAndTranslates()
    {
        ScreenRegion region = new(10, 20, 30, 40);

        Assert.True(region.Contains(10, 20));
        Assert.True(region.Contains(39, 59));
        Assert.False(region.Contains(40, 60));
        Assert.True(region.FitsWithin(40, 60));
        Assert.Equal(new ScreenRegion(3, 29, 30, 40), region.Translate(-7, 9));
    }

    [Fact]
    public void ClientBounds_ConvertsBetweenRelativeAndScreenCoordinates()
    {
        ClientBounds client = new(700, 250, 808, 611);
        ScreenRegion relative = new(116, 72, 259, 185);

        Assert.Equal(new ScreenRegion(816, 322, 259, 185), client.ToScreen(relative));
        Assert.Equal((116, 72), client.ToRelative(816, 322));
        Assert.Null(client.ToRelative(699, 322));
    }

    [Fact]
    public void ImageFrame_CropCopiesTheRequestedPixels()
    {
        byte[] pixels = Enumerable.Range(0, 4 * 3).Select(value => (byte)value).ToArray();
        ImageFrame frame = new(4, 3, PixelFormat.Gray8, pixels);

        ImageFrame crop = frame.Crop(new ScreenRegion(1, 1, 2, 2));

        Assert.Equal(2, crop.Width);
        Assert.Equal(2, crop.Height);
        Assert.Equal([5, 6, 9, 10], crop.Pixels);
    }

    [Fact]
    public void PlacementCaptures_CanUseRecordedOrDefaultDelays()
    {
        PlacementCapture[] captures =
        [
            new(1, 100, 200, 50, 100),
            new(2, 300, 400, 450, 500),
            new(3, 500, 550, 900, 950),
        ];

        IReadOnlyList<PlacementStep> recorded = PlacementModel.FromCaptures(captures, 125, useRecordedDelays: true);
        IReadOnlyList<PlacementStep> defaults = PlacementModel.FromCaptures(captures, 125, useRecordedDelays: false);

        Assert.Equal([350, 400, 125], recorded.Select(step => step.DelayAfterMilliseconds));
        Assert.All(defaults, step => Assert.Equal(125, step.DelayAfterMilliseconds));
    }

    [Fact]
    public void StableStateTracker_RequiresConsecutiveMatchesAndResetsOnNull()
    {
        StableStateTracker<string> tracker = new(2);

        Assert.Null(tracker.Update("reward"));
        Assert.Equal("reward", tracker.Update("reward"));
        Assert.Null(tracker.Update(null));
        Assert.Null(tracker.Update("reward"));
        Assert.Null(tracker.Update("continue"));
        Assert.Equal("continue", tracker.Update("continue"));
    }

    [Fact]
    public void AppSettings_DiagnosticCaptureOptionsDefaultOff()
    {
        AppSettings settings = new();

        Assert.False(settings.AutoCaptureOnMacroError);
        Assert.False(settings.IncludeLogsInDiagnosticArchives);
    }

    [Fact]
    public void ModelId_IsReadableStableAndNameSensitive()
    {
        string first = ModelId.FromName("Expedition Map 1");
        string second = ModelId.FromName("Expedition Map 1");

        Assert.Equal(first, second);
        Assert.StartsWith("expedition-map-1-", first);
        Assert.NotEqual(first, ModelId.FromName("Expedition Map 2"));
    }

    [Fact]
    public void AppPaths_CreatesTheDiagnosticsFolder()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();

            Assert.True(Directory.Exists(paths.Diagnostics));
            Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(paths.Diagnostics), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task PlacementRepository_OverwritesTheSameNamedModelAtomically()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            PlacementModelRepository repository = new(paths);
            string id = ModelId.FromName("Preplace");
            PlacementModel first = Model(id, 100);
            PlacementModel second = first with
            {
                Steps = [first.Steps[0] with { X = 200 }],
                UpdatedAt = first.UpdatedAt.AddMinutes(1),
            };

            await repository.SaveAsync(first);
            await repository.SaveAsync(second);

            PlacementModel loaded = Assert.IsType<PlacementModel>(await repository.LoadAsync(id));
            Assert.Equal(200, loaded.Steps[0].X);
            Assert.Single(await repository.ListAsync());
            Assert.Empty(Directory.EnumerateFiles(paths.PlacementModels, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static PlacementModel Model(string id, int x) => new()
    {
        Id = id,
        Name = "Preplace",
        ClientWidth = 808,
        ClientHeight = 611,
        Steps = [new PlacementStep { UnitKey = 1, X = x, Y = 300, DelayAfterMilliseconds = 100 }],
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
