using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Bounties;

public readonly record struct BountyNumberMatch(
    int Number,
    double Confidence,
    int CenterX,
    int CenterY);

public static class BountyNumberRecognizer
{
    private const int SearchX = 180;
    private const int SearchY = 200;
    private const int SearchWidth = 628;
    private const int SearchHeight = 320;
    private const int TemplateHeight = 7;
    private const int MaximumTemplateWidth = 17;
    private const int MaximumPixelDistance = 1;
    private const int MinimumDistanceMargin = 3;
    private const double MinimumRasterSimilarity = 0.90;
    private const double MinimumRasterSimilarityMargin = 0.03;
    private const double MinimumClearRasterSimilarity = 0.80;
    private const double MinimumClearRasterSimilarityMargin = 0.08;
    private const int MaximumRasterPositionDistance = 4;
    // The widest reviewed paper places its suffix 46 pixels left of the live action.
    private const int ActionWindowLeft = 54;
    private const int ActionWindowTop = 105;
    private const int ActionWindowWidth = 62;
    private const int ActionWindowHeight = 26;
    private const int ExpectedGlyphCenter = 47;
    private const int ExpectedGlyphTop = 8;

    private static readonly Template[] Templates =
    [
        Create(1, 10, "JJLsM4U/Sigh"),
        Create(2, 13, "JIbk+SQK9GMkjsQH"),
        Create(3, 12, "JEfSPkxhPyjZEgc="),
        Create(4, 11, "JCWpbyn7ewlKEA=="),
        Create(5, 12, "JE4yPkNxPyjxEgY="),
        Create(6, 12, "JExiPkJxPy/xEgY="),
        Create(7, 12, "pE/CPkRhPyIxEgE="),
        Create(8, 13, "JI7k+3wK9zMlvoQD"),
        Create(9, 12, "JEbyPkvhPyxBEgM="),
        Create(10, 17, "JGJIxvnMppD4I1tClIQ4"),
    ];

    public static IReadOnlyList<BountyNumberMatch> Detect(
        ImageFrame image)
    {
        Validate(image);
        return Detect(
            image,
            BountyBoardActionDetector.Find(image));
    }

    internal static IReadOnlyList<BountyNumberMatch> Detect(
        ImageFrame image,
        IReadOnlyList<BountyCardAction> actions)
    {
        Validate(image);
        byte[] mask = BuildMask(image);
        List<BountyNumberMatch> matches = [];
        foreach (BountyCardAction action in
                 actions
                     .GroupBy(value =>
                         value.CardAnchorX)
                     .Select(group => group.First()))
        {
            int left = action.CardAnchorX -
                ActionWindowLeft -
                SearchX;
            int top = action.Y -
                ActionWindowTop -
                SearchY;
            if (left < 0 ||
                top < 0 ||
                left + ActionWindowWidth >
                SearchWidth ||
                top + ActionWindowHeight >
                SearchHeight)
            {
                continue;
            }

            List<Candidate> candidates = [];
            foreach (Template template in Templates)
            {
                candidates.Add(
                    BestCandidate(
                        mask,
                        left,
                        top,
                        template));
            }

            Candidate[] ranked = candidates
                .OrderBy(candidate =>
                    candidate.Distance)
                .ThenBy(candidate =>
                    candidate.PositionDistance)
                .ThenByDescending(candidate =>
                    candidate.Template.Width)
                .ToArray();
            Candidate match = ranked[0];
            int margin =
                ranked[1].Distance -
                match.Distance;
            bool accepted =
                match.Distance <=
                MaximumPixelDistance &&
                margin >= MinimumDistanceMargin;
            if (!accepted)
            {
                Candidate[] rasterRanked = candidates
                    .OrderByDescending(candidate =>
                        candidate.Similarity)
                    .ThenBy(candidate =>
                        candidate.PositionDistance)
                    .ThenByDescending(candidate =>
                        candidate.Template.Width)
                    .ToArray();
                Candidate rasterMatch = rasterRanked[0];
                double rasterMargin =
                    rasterMatch.Similarity -
                    rasterRanked[1].Similarity;
                bool highSimilarity =
                    rasterMatch.Similarity >=
                        MinimumRasterSimilarity &&
                    rasterMargin >=
                        MinimumRasterSimilarityMargin;
                bool clearIdentity =
                    rasterMatch.Similarity >=
                        MinimumClearRasterSimilarity &&
                    rasterMargin >=
                        MinimumClearRasterSimilarityMargin;
                if ((highSimilarity || clearIdentity) &&
                    rasterMatch.PositionDistance <=
                        MaximumRasterPositionDistance)
                {
                    match = rasterMatch;
                    accepted = true;
                }
            }
            if (accepted)
            {
                matches.Add(
                    new BountyNumberMatch(
                        match.Template.Number,
                        Math.Clamp(
                            Math.Max(
                                match.Similarity,
                                1d -
                                match.Distance /
                                (double)(
                                    match.Template.Width *
                                    TemplateHeight)),
                            0,
                            1),
                        SearchX + left +
                            match.LocalX +
                            match.Template.Width / 2,
                        SearchY + top +
                            match.LocalY +
                            TemplateHeight / 2));
            }
        }
        BountyNumberMatch[] ordered =
            CollapseSameCardMatches(matches);
        VisionTrace.Emit(
            "bounty_numbers",
            string.Join(
                ",",
                ordered.Select(value =>
                    value.Number)),
            ordered.Length == 0
                ? 0
                : ordered.Min(value =>
                    value.Confidence),
            new
            {
                Matches = ordered.Select(value => new
                {
                    value.Number,
                    value.Confidence,
                    value.CenterX,
                    value.CenterY,
                }),
            });
        return ordered;
    }

    private static Candidate BestCandidate(
        IReadOnlyList<byte> mask,
        int windowLeft,
        int windowTop,
        Template template)
    {
        Candidate? best = null;
        for (int localY = 0;
             localY <=
             ActionWindowHeight - TemplateHeight;
             localY++)
        {
            for (int localX = 0;
                 localX <=
                 ActionWindowWidth -
                 MaximumTemplateWidth;
                 localX++)
            {
                // Right-side pixels disambiguate #1 from the identical prefix of #10.
                int distance = 0;
                int livePixels = 0;
                int intersection = 0;
                for (int y = 0;
                     y < TemplateHeight;
                     y++)
                {
                    for (int x = 0;
                         x <
                         MaximumTemplateWidth;
                         x++)
                    {
                        bool live =
                            mask[
                                (windowTop +
                                 localY + y) *
                                    SearchWidth +
                                windowLeft +
                                localX + x] != 0;
                        bool expected =
                            x < template.Width &&
                            template.Mask[
                                y * template.Width +
                                x] != 0;
                        if (live)
                        {
                            livePixels++;
                        }
                        if (live && expected)
                        {
                            intersection++;
                        }
                        if (live != expected)
                        {
                            distance++;
                        }
                    }
                }
                int positionDistance =
                    Math.Abs(
                        localX +
                        template.Width / 2 -
                        ExpectedGlyphCenter) +
                    Math.Abs(
                        localY -
                        ExpectedGlyphTop);
                double similarity =
                    2d * intersection /
                    (livePixels +
                     template.ForegroundPixels);
                Candidate candidate = new(
                    template,
                    distance,
                    similarity,
                    positionDistance,
                    localX,
                    localY);
                if (best is null ||
                    candidate.Distance <
                    best.Value.Distance ||
                    candidate.Distance ==
                    best.Value.Distance &&
                    candidate.PositionDistance <
                    best.Value.PositionDistance)
                {
                    best = candidate;
                }
            }
        }
        return best!.Value;
    }

    private static BountyNumberMatch[]
        CollapseSameCardMatches(
        IEnumerable<BountyNumberMatch> matches)
    {
        List<BountyNumberMatch> collapsed = [];
        foreach (BountyNumberMatch match in
                 matches
                     .OrderBy(value =>
                         value.CenterX)
                     .ThenByDescending(value =>
                         value.Confidence))
        {
            int existing = collapsed.FindIndex(
                value =>
                    Math.Abs(
                        value.CenterX -
                        match.CenterX) <= 18);
            if (existing < 0)
            {
                collapsed.Add(match);
            }
            else if (match.Confidence >
                     collapsed[existing].Confidence)
            {
                collapsed[existing] = match;
            }
        }
        return collapsed
            .OrderBy(value => value.CenterX)
            .ToArray();
    }

    private static byte[] BuildMask(ImageFrame image)
    {
        byte[] mask =
            new byte[SearchWidth * SearchHeight];
        for (int y = 0; y < SearchHeight; y++)
        {
            for (int x = 0; x < SearchWidth; x++)
            {
                int pixel =
                    ((SearchY + y) * image.Width +
                     SearchX + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                int maximum = Math.Max(
                    red,
                    Math.Max(green, blue));
                int minimum = Math.Min(
                    red,
                    Math.Min(green, blue));
                if (red > 185 &&
                    green > 185 &&
                    blue > 185 &&
                    maximum - minimum < 35)
                {
                    mask[y * SearchWidth + x] = 255;
                }
            }
        }
        return mask;
    }

    private static Template Create(
        int number,
        int width,
        string packed)
    {
        byte[] bits = Convert.FromBase64String(packed);
        byte[] pixels = new byte[width * 7];
        for (int bit = 0; bit < pixels.Length; bit++)
        {
            if ((bits[bit / 8] &
                 (1 << (bit % 8))) != 0)
            {
                pixels[bit] = 255;
            }
        }
        return new Template(
            number,
            width,
            pixels,
            pixels.Count(value => value != 0));
    }

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new ArgumentException(
                "Bounty number detection requires an 808 by 611 RGB client capture.",
                nameof(image));
        }
    }

    private sealed record Template(
        int Number,
        int Width,
        byte[] Mask,
        int ForegroundPixels);

    private readonly record struct Candidate(
        Template Template,
        int Distance,
        double Similarity,
        int PositionDistance,
        int LocalX,
        int LocalY);
}
