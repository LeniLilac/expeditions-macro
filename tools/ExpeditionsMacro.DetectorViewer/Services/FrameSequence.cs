using System.IO;
using ExpeditionsMacro.DeepDebugViewer.Services;

namespace ExpeditionsMacro.DetectorViewer.Services;

public enum FrameSourceKind
{
    Image,
    Folder,
    RepositoryDatasets,
    DeepDebugArchive,
}

public sealed record ViewerFrameRecord(
    int Index,
    string StorageKey,
    string DisplayPath,
    DateTimeOffset? Timestamp,
    bool Available)
{
    internal int? ArchiveFrameIndex { get; init; }
}

public sealed class FrameSequence : IDisposable
{
    private const long MaximumImageBytes =
        64L * 1024 * 1024;
    private readonly DeepDebugArchive? _archive;
    private bool _disposed;

    private FrameSequence(
        string sourcePath,
        FrameSourceKind kind,
        IReadOnlyList<ViewerFrameRecord> frames,
        DeepDebugArchive? archive = null)
    {
        SourcePath = sourcePath;
        Kind = kind;
        Frames = frames;
        _archive = archive;
    }

    public string SourcePath { get; }

    public FrameSourceKind Kind { get; }

    public IReadOnlyList<ViewerFrameRecord> Frames { get; }

    public static async Task<FrameSequence> OpenAsync(
        string path,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return await OpenFolderAsync(
                    fullPath,
                    FrameSourceKind.Folder,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The image source could not be found.",
                fullPath);
        }
        if (fullPath.EndsWith(
                ".zip",
                StringComparison.OrdinalIgnoreCase))
        {
            DeepDebugArchive archive =
                await DeepDebugArchive.OpenAsync(
                        fullPath,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (archive.Frames.Count == 0)
            {
                archive.Dispose();
                throw new InvalidDataException(
                    "The Deep Debug archive contains no captured PNG frames.");
            }
            ViewerFrameRecord[] frames =
                archive.Frames
                    .Where(frame =>
                        frame.EntryExists)
                    .Select(frame =>
                        new ViewerFrameRecord(
                            0,
                            frame.Path,
                            frame.Path,
                            frame.TimestampUtc,
                            frame.EntryExists)
                        {
                            ArchiveFrameIndex = frame.Index,
                        })
                    .Select((frame, index) =>
                        frame with { Index = index })
                    .ToArray();
            if (frames.Length == 0)
            {
                archive.Dispose();
                throw new InvalidDataException(
                    "The Deep Debug archive contains no retained PNG frame images. Its text timeline can still be opened in Deep Debug Viewer.");
            }
            return new FrameSequence(
                fullPath,
                FrameSourceKind.DeepDebugArchive,
                frames,
                archive);
        }
        if (!IsSupportedImage(fullPath))
        {
            throw new InvalidDataException(
                "Choose a PNG, JPEG, BMP, TIFF, Deep Debug ZIP, or a folder containing those images.");
        }
        FileInfo image = new(fullPath);
        ValidateFileSize(image);
        return new FrameSequence(
            fullPath,
            FrameSourceKind.Image,
            [
                new ViewerFrameRecord(
                    0,
                    fullPath,
                    image.Name,
                    image.LastWriteTimeUtc,
                    true),
            ]);
    }

    public static async Task<FrameSequence>
        OpenRepositoryDatasetsAsync(
            string datasetRoot,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            datasetRoot);
        string fullPath = Path.GetFullPath(
            datasetRoot);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Repository dataset folder was not found: {fullPath}");
        }
        return await OpenFolderAsync(
                fullPath,
                FrameSourceKind.RepositoryDatasets,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<byte[]> ReadFrameBytesAsync(
        int index,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
        if (index < 0 || index >= Frames.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index));
        }
        ViewerFrameRecord frame = Frames[index];
        if (_archive is not null)
        {
            int archiveFrameIndex =
                frame.ArchiveFrameIndex ?? index;
            return await _archive
                .ReadFrameBytesAsync(
                    _archive.Frames[archiveFrameIndex],
                    cancellationToken)
                .ConfigureAwait(false);
        }
        FileInfo info = new(frame.StorageKey);
        ValidateFileSize(info);
        await using FileStream stream = new(
            info.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite |
            FileShare.Delete,
            128 * 1024,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);
        byte[] bytes =
            new byte[checked((int)info.Length)];
        await stream.ReadExactlyAsync(
                bytes,
                cancellationToken)
            .ConfigureAwait(false);
        return bytes;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _archive?.Dispose();
    }

    private static Task<FrameSequence> OpenFolderAsync(
        string folder,
        FrameSourceKind kind,
        IProgress<string>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                progress?.Report(
                    kind == FrameSourceKind.RepositoryDatasets
                        ? "Indexing repository datasets..."
                        : "Indexing image folder...");
                List<string> images = [];
                Stack<string> pending = new();
                pending.Push(folder);
                while (pending.Count > 0)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    string current = pending.Pop();
                    foreach (string file in
                             Directory.EnumerateFiles(
                                 current))
                    {
                        if (IsSupportedImage(file))
                        {
                            images.Add(file);
                            if (images.Count > 100_000)
                            {
                                throw new InvalidDataException(
                                    "The folder contains more than 100,000 supported images.");
                            }
                        }
                    }
                    foreach (string child in
                             Directory.EnumerateDirectories(
                                 current))
                    {
                        FileAttributes attributes =
                            File.GetAttributes(child);
                        if (!attributes.HasFlag(
                                FileAttributes.ReparsePoint))
                        {
                            pending.Push(child);
                        }
                    }
                }
                string[] ordered = images
                    .OrderBy(path =>
                        Path.GetRelativePath(
                            folder,
                            path),
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (ordered.Length == 0)
                {
                    throw new InvalidDataException(
                        "The selected folder contains no supported images.");
                }
                ViewerFrameRecord[] frames =
                    ordered
                        .Select((path, index) =>
                        {
                            FileInfo info = new(path);
                            return new ViewerFrameRecord(
                                index,
                                info.FullName,
                                Path.GetRelativePath(
                                    folder,
                                    info.FullName),
                                info.LastWriteTimeUtc,
                                true);
                        })
                        .ToArray();
                return new FrameSequence(
                    folder,
                    kind,
                    frames);
            },
            cancellationToken);

    private static bool IsSupportedImage(string path) =>
        Path.GetExtension(path)
            .ToLowerInvariant() is
            ".png" or
            ".jpg" or
            ".jpeg" or
            ".bmp" or
            ".tif" or
            ".tiff";

    private static void ValidateFileSize(
        FileInfo info)
    {
        if (!info.Exists)
        {
            throw new FileNotFoundException(
                "The frame image is missing.",
                info.FullName);
        }
        if (info.Length is <= 0 or
            > MaximumImageBytes)
        {
            throw new InvalidDataException(
                $"Frame '{info.Name}' has an invalid size ({info.Length:N0} bytes).");
        }
    }
}
