using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpeditionsMacro.DetectorViewer.Models;

namespace ExpeditionsMacro.DetectorViewer.Services;

public sealed class DetectorAnnotationStore
{
    public const string FileName =
        "detector-annotations.json";
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();
    private readonly string _path;
    private readonly DetectorAnnotationDocument _document;

    private DetectorAnnotationStore(
        string path,
        DetectorAnnotationDocument document)
    {
        _path = path;
        _document = document;
    }

    public string Path => _path;

    public static DetectorAnnotationStore Open(
        string datasetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            datasetRoot);
        string root = System.IO.Path.GetFullPath(
            datasetRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Dataset folder was not found: {root}");
        }
        string path = System.IO.Path.Combine(
            root,
            FileName);
        DetectorAnnotationDocument document =
            File.Exists(path)
                ? Deserialize(path)
                : new DetectorAnnotationDocument();
        Validate(document);
        return new DetectorAnnotationStore(
            path,
            document);
    }

    public DetectorImageAnnotation GetOrCreate(
        string imagePath,
        string detectorId)
    {
        string normalized = NormalizePath(imagePath);
        DetectorImageAnnotation? existing =
            _document.Images.FirstOrDefault(item =>
                item.ImagePath.Equals(
                    normalized,
                    StringComparison.OrdinalIgnoreCase) &&
                item.DetectorId.Equals(
                    detectorId,
                    StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }
        DetectorImageAnnotation created = new()
        {
            ImagePath = normalized,
            DetectorId = detectorId,
        };
        _document.Images.Add(created);
        return created;
    }

    public void Save()
    {
        Validate(_document);
        _document.Images = _document.Images
            .OrderBy(item =>
                item.ImagePath,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(item =>
                item.DetectorId,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
        string json = JsonSerializer.Serialize(
            _document,
            JsonOptions);
        string temporary =
            $"{_path}.tmp-{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(
                temporary,
                json);
            File.Move(
                temporary,
                _path,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static DetectorAnnotationDocument Deserialize(
        string path)
    {
        try
        {
            return JsonSerializer.Deserialize<
                       DetectorAnnotationDocument>(
                       File.ReadAllText(path),
                       JsonOptions) ??
                throw new InvalidDataException(
                    "The detector annotation document is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException(
                $"The detector annotation document is invalid: {error.Message}",
                error);
        }
    }

    private static void Validate(
        DetectorAnnotationDocument document)
    {
        if (document.Schema != 1)
        {
            throw new InvalidDataException(
                $"Detector annotation schema {document.Schema} is not supported.");
        }
        foreach (DetectorImageAnnotation image in
                 document.Images)
        {
            if (string.IsNullOrWhiteSpace(
                    image.ImagePath) ||
                string.IsNullOrWhiteSpace(
                    image.DetectorId))
            {
                throw new InvalidDataException(
                    "Every detector annotation requires an image path and detector ID.");
            }
            foreach (DetectorAnnotationRegion region in
                     image.Regions)
            {
                if (region.X < 0 || region.Y < 0 ||
                    region.Width <= 0 ||
                    region.Height <= 0 ||
                    region.X + region.Width > 808 ||
                    region.Y + region.Height > 611)
                {
                    throw new InvalidDataException(
                        $"Annotation region '{region.Label}' falls outside the canonical 808 by 611 frame.");
                }
            }
        }
    }

    private static string NormalizePath(string value) =>
        value.Replace('\\', '/');

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy =
                JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
