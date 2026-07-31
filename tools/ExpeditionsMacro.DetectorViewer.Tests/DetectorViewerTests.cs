using System.IO.Compression;
using System.Text;
using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.DetectorViewer.Services;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Tests;

public sealed class DetectorViewerTests
{
    private static readonly byte[] OnePixelPng =
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEAQH/6X1O4QAAAABJRU5ErkJggg==");

    [Fact]
    public async Task CatalogCoversEveryPublicProductionEntryPoint()
    {
        DetectorInspectionCatalogResult catalog =
            await CreateCatalogAsync();
        DetectorCoverageReport report =
            DetectorViewerCoverageAudit.Run(
                catalog,
                SnapshotFixture.Create().Image);

        Assert.NotEmpty(report.Rows);
        Assert.Equal(
            catalog.ProductionDetectorCount,
            report.DiscoveredProductionTypes);
        Assert.Equal(
            report.DiscoveredProductionTypes,
            report.Rows.Count);
        Assert.All(
            report.Rows,
            row =>
                Assert.True(
                    row.HasGeometry ||
                    row.HasChecks ||
                    row.HasExplicitLimitation,
                    row.ProductionType));
        Assert.All(
            report.UnavailableDetails,
            item =>
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        item.Reason)));
    }

    [Fact]
    public async Task ReflectedConstantsRemainAdvisory()
    {
        DetectorInspectionCatalogResult catalog =
            await CreateCatalogAsync();
        var frame =
            SnapshotFixture.Create().Image;

        DetectorInspectionCheck[] checks =
            catalog.Definitions
                .SelectMany(definition =>
                    definition.Evaluate(frame).Checks)
                .ToArray();
        DetectorInspectionCheck[] constants =
            checks
                .Where(check =>
                    check.Id.StartsWith(
                        "constant:",
                        StringComparison.Ordinal))
                .ToArray();

        Assert.NotEmpty(constants);
        Assert.All(
            constants,
            check =>
            {
                Assert.Equal(
                    DetectorInspectionCheckStatus.Observed,
                    check.Status);
                Assert.Equal(
                    "Metric association not exposed",
                    check.Threshold);
                Assert.Contains(
                    "Advisory",
                    check.Expected,
                    StringComparison.Ordinal);
            });
        Assert.All(
            checks.Where(check =>
                check.Status is
                    DetectorInspectionCheckStatus.Passed or
                    DetectorInspectionCheckStatus.Failed),
            check =>
                Assert.True(
                    check.Threshold.StartsWith(
                        ">=",
                        StringComparison.Ordinal) ||
                    check.Threshold.StartsWith(
                        "<=",
                        StringComparison.Ordinal) ||
                    check.Threshold.Equals(
                        "Boolean result",
                        StringComparison.Ordinal),
                    $"{check.Id}: {check.Threshold}"));
    }

    [Fact]
    public async Task FolderSourceIndexesImagesRecursively()
    {
        using TestDirectory directory = new();
        string nested = Path.Combine(
            directory.Path,
            "nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllBytesAsync(
            Path.Combine(
                directory.Path,
                "frame-02.png"),
            OnePixelPng);
        await File.WriteAllBytesAsync(
            Path.Combine(
                nested,
                "frame-01.png"),
            OnePixelPng);
        await File.WriteAllTextAsync(
            Path.Combine(
                nested,
                "ignored.txt"),
            "not an image");

        using FrameSequence source =
            await FrameSequence.OpenAsync(
                directory.Path);

        Assert.Equal(
            FrameSourceKind.Folder,
            source.Kind);
        Assert.Equal(2, source.Frames.Count);
        Assert.Equal(
            [
                "frame-02.png",
                Path.Combine(
                    "nested",
                    "frame-01.png"),
            ],
            source.Frames.Select(frame =>
                frame.DisplayPath));
    }

    [Fact]
    public void RepositoryDatasetLocatorFindsCheckoutFromNestedPath()
    {
        using TestDirectory directory = new();
        string datasets = Path.Combine(
            directory.Path,
            "datasets");
        string nested = Path.Combine(
            directory.Path,
            "artifacts",
            "viewer");
        Directory.CreateDirectory(datasets);
        Directory.CreateDirectory(nested);
        File.WriteAllText(
            Path.Combine(
                directory.Path,
                "ExpeditionsMacro.slnx"),
            string.Empty);

        RepositoryDatasetLocation? location =
            RepositoryDatasetLocator.Find(
                [nested]);

        RepositoryDatasetLocation found =
            Assert.IsType<RepositoryDatasetLocation>(
                location);
        Assert.Equal(
            directory.Path,
            found.RepositoryRoot);
        Assert.Equal(datasets, found.DatasetRoot);
    }

    [Fact]
    public async Task RepositoryDatasetSourceIndexesEveryRepoImage()
    {
        string datasetRoot = Path.Combine(
            FindRepositoryRoot(),
            "datasets");
        int expected = Directory
            .EnumerateFiles(
                datasetRoot,
                "*",
                SearchOption.AllDirectories)
            .Count(IsSupportedDatasetImage);

        using FrameSequence source =
            await FrameSequence
                .OpenRepositoryDatasetsAsync(
                    datasetRoot);

        Assert.Equal(
            FrameSourceKind.RepositoryDatasets,
            source.Kind);
        Assert.Equal(expected, source.Frames.Count);
        Assert.All(
            source.Frames,
            frame =>
                Assert.False(
                    Path.IsPathRooted(
                        frame.DisplayPath)));
    }

    [Fact]
    public void FramePickerLabelStartsWithFilename()
    {
        FramePickerItem item = new(
            7,
            Path.Combine(
                "Event",
                "Villain",
                "selected-home.png"));

        Assert.StartsWith(
            "selected-home.png",
            item.Label,
            StringComparison.Ordinal);
        Assert.Contains(
            Path.Combine(
                "Event",
                "Villain"),
            item.Label,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatasetFrameAutoSelectsOwningDetector()
    {
        string repository = FindRepositoryRoot();
        string relative = Path.Combine(
            "anime-expeditions",
            "bounties",
            "BountyBoard_DimmedSlot1_01.png");
        byte[] bytes = await File.ReadAllBytesAsync(
            Path.Combine(
                repository,
                "datasets",
                relative));
        DecodedViewerFrame frame =
            ViewerFrameDecoder.Decode(bytes);
        DetectorInspectionCatalogResult catalog =
            await CreateCatalogAsync();
        DetectorCatalogItem[] items = catalog.Definitions
            .Select(definition =>
                new DetectorCatalogItem(definition))
            .ToArray();

        DetectorFrameEvaluation? selected =
            await DetectorFrameEvaluator
                .SelectAutomaticAsync(
                    items,
                    frame.Image,
                    relative,
                    CancellationToken.None);

        DetectorFrameEvaluation match =
            Assert.IsType<DetectorFrameEvaluation>(
                selected);
        Assert.Equal("Bounty Board", match.Item.Name);
    }

    [Fact]
    public void EditableFramePickerUsesThemedTemplate()
    {
        string theme = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "tools",
                "ExpeditionsMacro.DetectorViewer",
                "Themes",
                "ComboBox.xaml"));

        Assert.Contains(
            "PART_EditableTextBox",
            theme,
            StringComparison.Ordinal);
        Assert.Contains(
            "Background=\"{TemplateBinding Background}\"",
            theme,
            StringComparison.Ordinal);
        Assert.Contains(
            "Foreground=\"{TemplateBinding Foreground}\"",
            theme,
            StringComparison.Ordinal);
        string application = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "tools",
                "ExpeditionsMacro.DetectorViewer",
                "App.xaml"));
        Assert.True(
            application.IndexOf(
                "Themes/Dark.xaml",
                StringComparison.Ordinal) <
            application.IndexOf(
                "Themes/ComboBox.xaml",
            StringComparison.Ordinal));
    }

    [Fact]
    public void AnnotationStoreRoundTripsCanonicalRegions()
    {
        using TestDirectory directory = new();
        DetectorAnnotationStore store =
            DetectorAnnotationStore.Open(
                directory.Path);
        DetectorImageAnnotation annotation =
            store.GetOrCreate(
                "stages\\TeamUnits_01.png",
                "team-units");
        annotation.Expected =
            DetectorExpectedResult.Match;
        annotation.Notes =
            "Use the stable Teams action and panel structure.";
        annotation.Regions.Add(
            new DetectorAnnotationRegion
            {
                Label = "Teams action",
                X = 268,
                Y = 410,
                Width = 76,
                Height = 38,
            });

        store.Save();
        DetectorAnnotationStore reopened =
            DetectorAnnotationStore.Open(
                directory.Path);
        DetectorImageAnnotation loaded =
            reopened.GetOrCreate(
                "stages/TeamUnits_01.png",
                "team-units");

        Assert.Equal(
            DetectorExpectedResult.Match,
            loaded.Expected);
        Assert.Equal(annotation.Notes, loaded.Notes);
        DetectorAnnotationRegion region =
            Assert.Single(loaded.Regions);
        Assert.Equal("Teams action", region.Label);
        Assert.Equal(268, region.X);
        Assert.True(
            File.Exists(
                Path.Combine(
                    directory.Path,
                    DetectorAnnotationStore.FileName)));
    }

    [Fact]
    public void AnnotationGeometryClampsAndRejectsTinyDrags()
    {
        DetectorAnnotationRegion region =
            Assert.IsType<DetectorAnnotationRegion>(
                DetectorAnnotationGeometry.CreateRegion(
                    900,
                    700,
                    -12,
                    -8,
                    808,
                    611));

        Assert.Equal(0, region.X);
        Assert.Equal(0, region.Y);
        Assert.Equal(808, region.Width);
        Assert.Equal(611, region.Height);
        Assert.Null(
            DetectorAnnotationGeometry.CreateRegion(
                10,
                10,
                11,
                12,
                808,
                611));
    }

    [Fact]
    public async Task DeepDebugZipStreamsCapturedFrames()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(
            directory.Path,
            "deep-debug.zip");
        using (FileStream file = File.Create(path))
        using (ZipArchive archive = new(
                   file,
                   ZipArchiveMode.Create))
        {
            WriteText(
                archive,
                "manifest.json",
                "{\"operation\":\"Detector Viewer test\"}");
            WriteBytes(
                archive,
                "frames/frame-000000002.png",
                OnePixelPng);
            WriteBytes(
                archive,
                "frames/frame-000000001.png",
                OnePixelPng);
        }

        using FrameSequence source =
            await FrameSequence.OpenAsync(path);
        byte[] bytes =
            await source.ReadFrameBytesAsync(0);

        Assert.Equal(
            FrameSourceKind.DeepDebugArchive,
            source.Kind);
        Assert.Equal(2, source.Frames.Count);
        Assert.EndsWith(
            "frame-000000001.png",
            source.Frames[0].DisplayPath,
            StringComparison.Ordinal);
        Assert.Equal(OnePixelPng, bytes);
    }

    [Fact]
    public void PixelInspectorReportsCanonicalRgb()
    {
        var frame =
            SnapshotFixture.Create().Image;

        PixelSample sample =
            Assert.IsType<PixelSample>(
                PixelInspector.Sample(
                    frame,
                    0,
                    0));

        Assert.Equal(28, sample.Red);
        Assert.Equal(32, sample.Green);
        Assert.Equal(42, sample.Blue);
        Assert.Null(
            PixelInspector.Sample(
                frame,
                -1,
                0));
    }

    [Fact]
    public void ViewerSourcesContainNoMojibakeMarkers()
    {
        string repository =
            FindRepositoryRoot();
        string[] roots =
        [
            Path.Combine(
                repository,
                "tools",
                "ExpeditionsMacro.DetectorViewer"),
            Path.Combine(
                repository,
                "src",
                "ExpeditionsMacro.Vision",
                "Inspection"),
        ];

        foreach (string file in roots.SelectMany(root =>
                     Directory.EnumerateFiles(
                         root,
                         "*",
                         SearchOption.AllDirectories))
                     .Where(file =>
                         !file.Contains(
                             $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                             StringComparison.OrdinalIgnoreCase) &&
                         !file.Contains(
                             $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                             StringComparison.OrdinalIgnoreCase)))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain(
                "\u00e2",
                text,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "\ufffd",
                text,
                StringComparison.Ordinal);
        }
    }

    private static async Task<
        DetectorInspectionCatalogResult>
        CreateCatalogAsync() =>
        DetectorInspectionCatalog.Create(
            await BundledDetectorPackLoader.LoadAsync());

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current =
            new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "ExpeditionsMacro.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate the repository root.");
    }

    private static bool IsSupportedDatasetImage(
        string path) =>
        Path.GetExtension(path)
            .ToLowerInvariant() is
            ".png" or
            ".jpg" or
            ".jpeg" or
            ".bmp" or
            ".tif" or
            ".tiff";

    private static void WriteText(
        ZipArchive archive,
        string name,
        string value) =>
        WriteBytes(
            archive,
            name,
            Encoding.UTF8.GetBytes(value));

    private static void WriteBytes(
        ZipArchive archive,
        string name,
        byte[] value)
    {
        ZipArchiveEntry entry =
            archive.CreateEntry(
                name,
                CompressionLevel.NoCompression);
        using Stream stream = entry.Open();
        stream.Write(value);
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ExpeditionsMacro.DetectorViewer.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
