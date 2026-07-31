using System.Text;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Services;

public sealed record DetectorFrameEvaluation(
    DetectorCatalogItem Item,
    DetectorInspectionReport Report);

public static class DetectorFrameEvaluator
{
    private static readonly HashSet<string> IgnoredPathTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "anime",
            "expeditions",
            "dataset",
            "datasets",
            "frame",
            "image",
            "negative",
            "positive",
            "stable",
        };

    public static async Task<DetectorInspectionReport>
        EvaluateAsync(
            DetectorCatalogItem item,
            ImageFrame image,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(image);
        if (!IsCanonical(image))
        {
            return DetectorInspectionReport.Unavailable(
                CanonicalSizeMessage(image),
                item.Definition.Regions);
        }
        return await Task.Run(
            () => item.Definition.Evaluate(image),
            cancellationToken);
    }

    public static async Task<DetectorFrameEvaluation?>
        SelectAutomaticAsync(
            IReadOnlyList<DetectorCatalogItem> items,
            ImageFrame image,
            string displayPath,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            displayPath);
        if (!IsCanonical(image))
        {
            return null;
        }
        return await Task.Run(
            () => SelectAutomatic(
                items,
                image,
                displayPath,
                cancellationToken),
            cancellationToken);
    }

    private static DetectorFrameEvaluation? SelectAutomatic(
        IReadOnlyList<DetectorCatalogItem> items,
        ImageFrame image,
        string displayPath,
        CancellationToken cancellationToken)
    {
        HashSet<string> pathTokens =
            Tokenize(displayPath);
        string compactPath = Compact(displayPath);
        List<Candidate> candidates = [];
        foreach (DetectorCatalogItem item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!item.Definition.CanEvaluate)
            {
                continue;
            }
            try
            {
                DetectorInspectionReport report =
                    item.Definition.Evaluate(image);
                candidates.Add(
                    Score(
                        item,
                        report,
                        pathTokens,
                        compactPath));
            }
            catch (Exception error) when (
                error is not OperationCanceledException)
            {
                // One inspection surface must not prevent another
                // production detector from owning the frame.
            }
        }
        if (candidates.Count == 0)
        {
            return null;
        }

        Candidate? intended = candidates
            .Where(candidate =>
                candidate.ExactName ||
                candidate.NameTokenMatches >= 2)
            .OrderByDescending(candidate =>
                candidate.ExactName)
            .ThenByDescending(candidate =>
                candidate.NameTokenMatches)
            .ThenByDescending(candidate =>
                candidate.GroupTokenMatches)
            .ThenByDescending(candidate =>
                candidate.Report.Passed is true)
            .ThenByDescending(candidate =>
                candidate.Report.Confidence ?? 0)
            .FirstOrDefault();
        if (intended is null)
        {
            Candidate[] byName = candidates
                .Where(candidate =>
                    candidate.NameTokenMatches > 0)
                .OrderByDescending(candidate =>
                    candidate.NameTokenMatches)
                .ThenByDescending(candidate =>
                    candidate.Report.Passed is true)
                .ThenByDescending(candidate =>
                    candidate.Report.Confidence ?? 0)
                .ToArray();
            if (byName.Length > 0 &&
                (byName.Length == 1 ||
                 byName[0].NameTokenMatches >
                 byName[1].NameTokenMatches))
            {
                intended = byName[0];
            }
        }

        Candidate? selected = intended ??
            candidates
                .Where(candidate =>
                    candidate.Report.Passed is true)
                .OrderByDescending(candidate =>
                    candidate.NameTokenMatches)
                .ThenByDescending(candidate =>
                    candidate.GroupTokenMatches)
                .ThenByDescending(candidate =>
                    candidate.Report.Confidence ?? 0)
                .ThenByDescending(candidate =>
                    candidate.Report.Action is not null)
                .FirstOrDefault();
        return selected is null
            ? null
            : new DetectorFrameEvaluation(
                selected.Item,
                selected.Report);
    }

    private static Candidate Score(
        DetectorCatalogItem item,
        DetectorInspectionReport report,
        IReadOnlySet<string> pathTokens,
        string compactPath)
    {
        HashSet<string> nameTokens =
            Tokenize(item.Name);
        HashSet<string> groupTokens =
            Tokenize(item.Group);
        return new Candidate(
            item,
            report,
            ExactName: compactPath.Contains(
                Compact(item.Name),
                StringComparison.OrdinalIgnoreCase),
            NameTokenMatches: nameTokens.Count(
                pathTokens.Contains),
            GroupTokenMatches: groupTokens.Count(
                pathTokens.Contains));
    }

    private static HashSet<string> Tokenize(
        string value) =>
        SplitWords(value)
            .Where(token =>
                token.Length > 1 &&
                !IgnoredPathTokens.Contains(token) &&
                !token.All(char.IsDigit))
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitWords(
        string value)
    {
        StringBuilder current = new();
        char previous = '\0';
        foreach (char character in value)
        {
            bool boundary = char.IsUpper(character) &&
                current.Length > 0 &&
                char.IsLower(previous);
            if (boundary || !char.IsLetterOrDigit(character))
            {
                if (current.Length > 0)
                {
                    yield return current
                        .ToString()
                        .ToLowerInvariant();
                    current.Clear();
                }
                if (!char.IsLetterOrDigit(character))
                {
                    previous = character;
                    continue;
                }
            }
            current.Append(character);
            previous = character;
        }
        if (current.Length > 0)
        {
            yield return current
                .ToString()
                .ToLowerInvariant();
        }
    }

    private static string Compact(string value) =>
        string.Concat(
            value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant));

    public static bool IsCanonical(ImageFrame image) =>
        image.Width == 808 &&
        image.Height == 611;

    public static string CanonicalSizeMessage(
        ImageFrame image) =>
        $"Production detectors require the canonical 808 × 611 Roblox client. This frame is {image.Width} × {image.Height}; it is displayed but not evaluated.";

    private sealed record Candidate(
        DetectorCatalogItem Item,
        DetectorInspectionReport Report,
        bool ExactName,
        int NameTokenMatches,
        int GroupTokenMatches);
}
