using System.Threading.Channels;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Diagnostics;

internal sealed class DeepDebugSession
{
    public DeepDebugSession(
        string operation,
        DeepDebugOperationContext context,
        DateTimeOffset startedAtUtc,
        string stagingDirectory,
        Channel<DeepDebugWriteItem> channel,
        int frameRetentionMinutes)
    {
        Operation = operation;
        Context = context;
        StartedAtUtc = startedAtUtc;
        StagingDirectory = stagingDirectory;
        Channel = channel;
        FrameRetentionMinutes = frameRetentionMinutes;
    }

    public string Operation { get; }

    public DeepDebugOperationContext Context { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public string StagingDirectory { get; }

    public Channel<DeepDebugWriteItem> Channel { get; }

    public int FrameRetentionMinutes { get; }

    public PriorityQueue<DeepDebugRetainedFrame, long>
        RetainedFrames { get; } = new();

    public DateTimeOffset LatestFrameTimestampUtc { get; set; }

    public Task WriterTask { get; set; } = Task.CompletedTask;

    public Exception? WriterFailure { get; set; }

    public long Sequence;

    public int FrameCount;

    public int EventCount;

    public int InputEventCount;

    public int DiscardedFrameCount;

    public HashSet<string> ReferencedPlacementModelIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> ReferencedDetectorPackIds { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}

internal sealed record DeepDebugWriteItem(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Category,
    string Action,
    object? Data,
    string? FramePath,
    ImageFrame? Frame);

internal sealed record DeepDebugEventRecord(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Category,
    string Action,
    string? Frame,
    object? Data);

internal sealed record DeepDebugRetainedFrame(
    string Path,
    DateTimeOffset TimestampUtc);

internal sealed record DeepDebugManifest(
    string Operation,
    string Outcome,
    string AppVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    TimeSpan Runtime,
    int Frames,
    int Events,
    int InputEvents,
    int FrameRetentionMinutes,
    int RetainedFrameImages,
    int DiscardedFrameImages,
    DateTimeOffset FrameWindowStartedAtUtc,
    string FramePolicy,
    string? WriterFailure,
    string? OperationError,
    string SecretPolicy);
