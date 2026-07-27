using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class ManualInputRecordingRepository
{
    private const string FileName = "recording.json";
    private readonly AppPaths _paths;

    public ManualInputRecordingRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<ManualInputRecording>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        List<ManualInputRecording> recordings = [];
        foreach (string file in Directory.EnumerateFiles(
            _paths.ManualRecordings,
            FileName,
            SearchOption.AllDirectories))
        {
            try
            {
                ManualInputRecording? recording =
                    await JsonFileStore.ReadAsync<ManualInputRecording>(
                            file,
                            cancellationToken)
                        .ConfigureAwait(false);
                recording?.Validate();
                if (recording is not null)
                {
                    recordings.Add(recording);
                }
            }
            catch (Exception error) when (
                error is IOException or
                    UnauthorizedAccessException or
                    InvalidDataException or
                    System.Text.Json.JsonException)
            {
                // A corrupt recording does not hide other saved recordings.
            }
        }

        return recordings
            .OrderByDescending(recording => recording.CreatedAtUtc)
            .ThenBy(recording => recording.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<ManualInputRecording?> LoadAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        string path = RecordingPath(id);
        ManualInputRecording? recording =
            await JsonFileStore.ReadAsync<ManualInputRecording>(
                    path,
                    cancellationToken)
                .ConfigureAwait(false);
        recording?.Validate();
        return recording;
    }

    public async Task SaveAsync(
        ManualInputRecording recording,
        CancellationToken cancellationToken = default)
    {
        recording.Validate();
        await JsonFileStore.WriteAtomicAsync(
                RecordingPath(recording.Id),
                recording,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public void Delete(string id)
    {
        string directory = Path.Combine(
            _paths.ManualRecordings,
            ManualInputRecording.ValidateId(id));
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string RecordingPath(string id) =>
        Path.Combine(
            _paths.ManualRecordings,
            ManualInputRecording.ValidateId(id),
            FileName);
}
