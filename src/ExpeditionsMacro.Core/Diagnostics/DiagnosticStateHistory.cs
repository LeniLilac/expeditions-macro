using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Core.Diagnostics;

public sealed record DiagnosticStateFrame(
    ImageFrame Image,
    DateTimeOffset CapturedAtUtc,
    string Action);

public sealed class DiagnosticStateHistory
{
    private readonly int _capacity;
    private readonly Queue<DiagnosticStateFrame> _frames = [];

    public DiagnosticStateHistory(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count => _frames.Count;

    public void Add(ImageFrame image, DateTimeOffset capturedAtUtc, string action)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("An action label is required.", nameof(action));

        _frames.Enqueue(new DiagnosticStateFrame(image, capturedAtUtc, action.Trim()));
        while (_frames.Count > _capacity) _frames.Dequeue();
    }

    public IReadOnlyList<DiagnosticStateFrame> Snapshot() => _frames.ToArray();

    public void Clear() => _frames.Clear();
}
