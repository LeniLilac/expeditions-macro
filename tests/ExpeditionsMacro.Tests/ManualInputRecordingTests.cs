using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class ManualInputRecordingTests
{
    [Fact]
    public void Validate_AcceptsOrderedKeyboardMouseAndWheelEvents()
    {
        ManualInputRecording recording = ValidRecording();

        recording.Validate();

        Assert.Equal(5, recording.Events.Count);
        Assert.Equal(40_000, recording.DurationMicroseconds);
    }

    [Fact]
    public void Validate_RejectsNonCanonicalClient()
    {
        ManualInputRecording recording =
            ValidRecording() with
            {
                ClientWidth = 807,
            };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                recording.Validate);

        Assert.Contains(
            "canonical",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsInitialPointerOutsideClient()
    {
        ManualInputRecording recording =
            ValidRecording() with
            {
                InitialClientX = 808,
            };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                recording.Validate);

        Assert.Contains(
            "initial pointer",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsOutOfOrderEvents()
    {
        ManualInputRecording recording =
            ValidRecording() with
            {
                Events =
                [
                    Key(20_000, ManualInputEventKind.KeyDown),
                    Key(10_000, ManualInputEventKind.KeyUp),
                ],
            };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                recording.Validate);

        Assert.Contains(
            "ordered",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsEmptyRecording()
    {
        ManualInputRecording recording =
            ValidRecording() with
            {
                DurationMicroseconds = 0,
                Events = [],
            };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                recording.Validate);

        Assert.Contains(
            "at least one",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsNullEventCollection()
    {
        ManualInputRecording recording =
            ValidRecording() with
            {
                Events = null!,
            };

        Assert.Throws<InvalidDataException>(
            recording.Validate);
    }

    [Fact]
    public void Validate_RejectsNullEvent()
    {
        ManualInputRecording recording =
            ValidRecording() with
            {
                Events =
                [
                    null!,
                ],
            };

        Assert.Throws<InvalidDataException>(
            recording.Validate);
    }

    [Fact]
    public void Validate_RejectsUndefinedMouseButton()
    {
        ManualInputRecording recording =
            ValidRecording() with
            {
                Events =
                [
                    new ManualInputEvent
                    {
                        OffsetMicroseconds = 0,
                        Kind =
                            ManualInputEventKind
                                .MouseButtonDown,
                        ClientX = 240,
                        ClientY = 300,
                        MouseButton =
                            (ManualMouseButton)99,
                    },
                ],
            };

        Assert.Throws<InvalidDataException>(
            recording.Validate);
    }

    [Fact]
    public void Validate_RejectsMouseOutsideClient()
    {
        ManualInputRecording recording =
            ValidRecording() with
            {
                Events =
                [
                    new ManualInputEvent
                    {
                        OffsetMicroseconds = 0,
                        Kind = ManualInputEventKind.MouseMove,
                        ClientX = 808,
                        ClientY = 40,
                    },
                ],
            };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                recording.Validate);

        Assert.Contains(
            "outside",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImmutableSnapshot_DoesNotFollowSourceEventMutations()
    {
        List<ManualInputEvent> sourceEvents =
            ValidRecording().Events.ToList();
        ManualInputRecording source =
            ValidRecording() with
            {
                Events = sourceEvents,
            };
        ManualInputEvent sourceFirst =
            source.Events[0];

        ManualInputRecording snapshot =
            source.CreateImmutableSnapshot();
        sourceEvents.Clear();

        Assert.Equal(5, snapshot.Events.Count);
        Assert.NotSame(
            source.Events,
            snapshot.Events);
        Assert.NotSame(
            sourceFirst,
            snapshot.Events[0]);
        snapshot.Validate();
    }

    [Fact]
    public async Task Repository_RoundTripsListsAndDeletesRecording()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            ManualInputRecordingRepository repository =
                new(paths);
            ManualInputRecording recording =
                ValidRecording();

            await repository.SaveAsync(recording);
            ManualInputRecording? loaded =
                await repository.LoadAsync(recording.Id);
            IReadOnlyList<ManualInputRecording> listed =
                await repository.ListAsync();

            Assert.NotNull(loaded);
            Assert.Equal(recording.Id, loaded.Id);
            Assert.Equal(recording.Name, loaded.Name);
            Assert.Equal(
                recording.DurationMicroseconds,
                loaded.DurationMicroseconds);
            Assert.Equal(
                recording.InitialClientX,
                loaded.InitialClientX);
            Assert.Equal(
                recording.InitialClientY,
                loaded.InitialClientY);
            Assert.Equal(recording.Events, loaded.Events);
            Assert.Single(listed);
            Assert.Equal(
                paths.ManualRecordings,
                Path.Combine(root, "manual-recordings"));
            repository.Delete(recording.Id);
            Assert.Null(
                await repository.LoadAsync(recording.Id));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Repository_ListSkipsCorruptRecording()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            ManualInputRecordingRepository repository =
                new(paths);
            ManualInputRecording valid = ValidRecording();
            await repository.SaveAsync(valid);
            string corruptDirectory = Path.Combine(
                paths.ManualRecordings,
                "corrupt");
            Directory.CreateDirectory(corruptDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(
                    corruptDirectory,
                    "recording.json"),
                "{ invalid");

            IReadOnlyList<ManualInputRecording> listed =
                await repository.ListAsync();

            ManualInputRecording saved =
                Assert.Single(listed);
            Assert.Equal(valid.Id, saved.Id);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Repository_ListSkipsRecordingWithInvalidId()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            ManualInputRecordingRepository repository =
                new(paths);
            await repository.SaveAsync(ValidRecording());
            string invalidDirectory = Path.Combine(
                paths.ManualRecordings,
                "invalid");
            Directory.CreateDirectory(invalidDirectory);
            ManualInputRecording invalid =
                ValidRecording() with
                {
                    Id = "../escape",
                };
            await JsonFileStore.WriteAtomicAsync(
                Path.Combine(
                    invalidDirectory,
                    "recording.json"),
                invalid);

            IReadOnlyList<ManualInputRecording> listed =
                await repository.ListAsync();

            Assert.Single(listed);
            Assert.Equal("route-manual-1", listed[0].Id);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Theory]
    [InlineData("../recording")]
    [InlineData(".")]
    [InlineData("")]
    public void ValidateId_RejectsTraversalAndEmptyIds(
        string id)
    {
        Assert.Throws<ArgumentException>(
            () => ManualInputRecording.ValidateId(id));
    }

    internal static ManualInputRecording ValidRecording() =>
        new()
        {
            Id = "route-manual-1",
            Name = "Route manual 1",
            InitialClientX = 240,
            InitialClientY = 300,
            CreatedAtUtc =
                new DateTimeOffset(
                    2026,
                    7,
                    27,
                    12,
                    0,
                    0,
                    TimeSpan.Zero),
            DurationMicroseconds = 40_000,
            Events =
            [
                Key(
                    0,
                    ManualInputEventKind.KeyDown),
                new ManualInputEvent
                {
                    OffsetMicroseconds = 10_000,
                    Kind = ManualInputEventKind.MouseMove,
                    ClientX = 240,
                    ClientY = 300,
                },
                new ManualInputEvent
                {
                    OffsetMicroseconds = 20_000,
                    Kind =
                        ManualInputEventKind.MouseButtonDown,
                    ClientX = 240,
                    ClientY = 300,
                    MouseButton = ManualMouseButton.Left,
                },
                new ManualInputEvent
                {
                    OffsetMicroseconds = 25_000,
                    Kind =
                        ManualInputEventKind.MouseButtonUp,
                    ClientX = 240,
                    ClientY = 300,
                    MouseButton = ManualMouseButton.Left,
                },
                new ManualInputEvent
                {
                    OffsetMicroseconds = 30_000,
                    Kind = ManualInputEventKind.MouseWheel,
                    ClientX = 240,
                    ClientY = 300,
                    WheelDelta = -120,
                },
            ],
        };

    internal static ManualInputEvent Key(
        long offset,
        ManualInputEventKind kind) =>
        new()
        {
            OffsetMicroseconds = offset,
            Kind = kind,
            VirtualKey = 0x57,
            ScanCode = 0x11,
        };
}
