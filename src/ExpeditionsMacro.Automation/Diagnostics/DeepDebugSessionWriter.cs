using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Automation.Diagnostics;

internal static class DeepDebugSessionWriter
{
    public static async Task WriteAsync(
        DeepDebugSession session,
        JsonSerializerOptions compactJson,
        Func<string, string> redact)
    {
        string eventsPath = Path.Combine(
            session.StagingDirectory,
            "events.jsonl");
        try
        {
            await using FileStream stream = new(
                eventsPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            await using StreamWriter writer = new(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            await foreach (DeepDebugWriteItem item in
                session.Channel.Reader
                    .ReadAllAsync()
                    .ConfigureAwait(false))
            {
                WriteFrame(session, item);
                DeepDebugEventRecord record = new(
                    item.Sequence,
                    item.TimestampUtc,
                    item.Category,
                    item.Action,
                    item.FramePath,
                    item.Data);
                string json;
                try
                {
                    json = redact(
                        JsonSerializer.Serialize(
                            record,
                            compactJson));
                }
                catch (Exception error)
                {
                    json = redact(
                        JsonSerializer.Serialize(
                            record with
                            {
                                Data = new
                                {
                                    SerializationError =
                                        error.Message,
                                },
                            },
                            compactJson));
                }
                await writer.WriteLineAsync(json)
                    .ConfigureAwait(false);
            }
            await writer.FlushAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception error)
        {
            session.WriterFailure = error;
            session.Channel.Writer.TryComplete(error);
            throw;
        }
    }

    public static void PruneExpiredFrames(
        DeepDebugSession session,
        DateTimeOffset referenceUtc)
    {
        DateTimeOffset cutoff = referenceUtc.AddMinutes(
            -session.FrameRetentionMinutes);
        List<(DeepDebugRetainedFrame Frame, long Priority)>
            failed = [];
        while (session.RetainedFrames.TryPeek(
                   out DeepDebugRetainedFrame? retained,
                   out long priority) &&
               retained.TimestampUtc < cutoff)
        {
            session.RetainedFrames.Dequeue();
            try
            {
                if (File.Exists(retained.Path))
                {
                    File.Delete(retained.Path);
                }
            }
            catch (Exception error) when (
                error is IOException or
                UnauthorizedAccessException)
            {
                failed.Add((retained, priority));
                continue;
            }
            Interlocked.Increment(
                ref session.DiscardedFrameCount);
        }
        foreach ((DeepDebugRetainedFrame frame,
                  long priority) in failed)
        {
            session.RetainedFrames.Enqueue(
                frame,
                priority);
        }
    }

    private static void WriteFrame(
        DeepDebugSession session,
        DeepDebugWriteItem item)
    {
        if (item.Frame is null ||
            item.FramePath is null)
        {
            return;
        }
        string framePath = Path.GetFullPath(
            Path.Combine(
                session.StagingDirectory,
                item.FramePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
        EnsureFramePath(session, framePath);
        ImageCodec.SavePng(
            framePath,
            item.Frame,
            compression: 1);
        session.RetainedFrames.Enqueue(
            new DeepDebugRetainedFrame(
                framePath,
                item.TimestampUtc),
            item.TimestampUtc.UtcDateTime.Ticks);
        if (item.TimestampUtc >
            session.LatestFrameTimestampUtc)
        {
            session.LatestFrameTimestampUtc =
                item.TimestampUtc;
        }
        PruneExpiredFrames(
            session,
            session.LatestFrameTimestampUtc);
    }

    private static void EnsureFramePath(
        DeepDebugSession session,
        string path)
    {
        string frameRoot = Path.GetFullPath(
                Path.Combine(
                    session.StagingDirectory,
                    "frames"))
            .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!path.StartsWith(
                frameRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Deep debug frame output resolved outside the session frame folder.");
        }
    }
}
