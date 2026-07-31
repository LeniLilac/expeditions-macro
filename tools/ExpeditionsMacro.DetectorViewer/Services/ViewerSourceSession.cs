namespace ExpeditionsMacro.DetectorViewer.Services;

public sealed record LoadedViewerFrame(
    DecodedViewerFrame Frame,
    ViewerFrameRecord Record,
    int Index,
    int FrameCount,
    string SourcePath,
    FrameSourceKind SourceKind,
    IReadOnlyList<ViewerFrameRecord> Frames)
{
    public string SourceKindLabel =>
        SourceKind switch
        {
            FrameSourceKind.Image =>
                "Single image",
            FrameSourceKind.Folder =>
                "Image folder",
            FrameSourceKind.RepositoryDatasets =>
                "Repository datasets",
            _ => "Deep Debug archive",
        };
}

public sealed class ViewerSourceSession : IDisposable
{
    private FrameSequence? _source;
    private DecodedFrameCache _cache = new();

    public async Task<LoadedViewerFrame> OpenAsync(
        string path,
        IProgress<string>? progress,
        CancellationToken cancellationToken) =>
        await OpenCoreAsync(
            () => FrameSequence.OpenAsync(
                path,
                progress,
                cancellationToken),
            cancellationToken);

    public async Task<LoadedViewerFrame>
        OpenRepositoryDatasetsAsync(
            string datasetRoot,
            IProgress<string>? progress,
            CancellationToken cancellationToken) =>
        await OpenCoreAsync(
            () => FrameSequence
                .OpenRepositoryDatasetsAsync(
                    datasetRoot,
                    progress,
                    cancellationToken),
            cancellationToken);

    private async Task<LoadedViewerFrame> OpenCoreAsync(
        Func<Task<FrameSequence>> openSource,
        CancellationToken cancellationToken)
    {
        FrameSequence? candidate = null;
        try
        {
            candidate =
                await openSource();
            DecodedFrameCache cache = new();
            DecodedViewerFrame first =
                await cache.GetAsync(
                    0,
                    token =>
                        candidate.ReadFrameBytesAsync(
                            0,
                            token),
                    cancellationToken);
            cancellationToken
                .ThrowIfCancellationRequested();

            FrameSequence? previous =
                _source;
            _source = candidate;
            candidate = null;
            _cache = cache;
            previous?.Dispose();
            return CreateResult(
                first,
                0,
                _source);
        }
        finally
        {
            candidate?.Dispose();
        }
    }

    public async Task<LoadedViewerFrame> LoadAsync(
        int index,
        CancellationToken cancellationToken)
    {
        FrameSequence source =
            _source ??
            throw new InvalidOperationException(
                "No image source is open.");
        if (index < 0 ||
            index >= source.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index));
        }
        DecodedViewerFrame frame =
            await _cache.GetAsync(
                index,
                token =>
                    source.ReadFrameBytesAsync(
                        index,
                        token),
                cancellationToken);
        cancellationToken
            .ThrowIfCancellationRequested();
        if (!ReferenceEquals(
                source,
                _source))
        {
            throw new OperationCanceledException(
                cancellationToken);
        }
        return CreateResult(
            frame,
            index,
            source);
    }

    public void Dispose() =>
        _source?.Dispose();

    private static LoadedViewerFrame CreateResult(
        DecodedViewerFrame frame,
        int index,
        FrameSequence source) =>
        new(
            frame,
            source.Frames[index],
            index,
            source.Frames.Count,
            source.SourcePath,
            source.Kind,
            source.Frames);
}
