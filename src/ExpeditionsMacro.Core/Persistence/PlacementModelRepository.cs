using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class PlacementModelRepository
{
    private readonly AppPaths _paths;

    public PlacementModelRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<PlacementModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        List<PlacementModel> models = [];
        foreach (string file in Directory.EnumerateFiles(_paths.PlacementModels, "placement.json", SearchOption.AllDirectories))
        {
            try
            {
                PlacementModel? model = await JsonFileStore.ReadAsync<PlacementModel>(file, cancellationToken).ConfigureAwait(false);
                model?.Validate();
                if (model is not null) models.Add(model);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                // A corrupt model does not prevent other models from loading.
            }
        }

        return models.OrderBy(model => model.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public async Task<PlacementModel?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(_paths.PlacementModels, ValidateId(id), "placement.json");
        PlacementModel? model = await JsonFileStore.ReadAsync<PlacementModel>(path, cancellationToken).ConfigureAwait(false);
        model?.Validate();
        return model;
    }

    public async Task SaveAsync(PlacementModel model, CancellationToken cancellationToken = default)
    {
        model.Validate();
        string path = Path.Combine(_paths.PlacementModels, ValidateId(model.Id), "placement.json");
        await JsonFileStore.WriteAtomicAsync(path, model, cancellationToken).ConfigureAwait(false);
    }

    public void Delete(string id)
    {
        string directory = Path.Combine(_paths.PlacementModels, ValidateId(id));
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new ArgumentException("Invalid model id.", nameof(id));
        return id;
    }
}
