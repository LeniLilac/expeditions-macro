using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;
using ExpeditionsMacro.Vision.Infrastructure;
using OpenCvSharp;

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
    private const double MatchThreshold = 0.985;
    private const int ActionWindowLeft = 22;
    private const int ActionWindowTop = 105;
    private const int ActionWindowWidth = 24;
    private const int ActionWindowHeight = 24;

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
        ImageFrame image) =>
        Detect(
            image,
            BountyBoardActionDetector.Find(image));

    internal static IReadOnlyList<BountyNumberMatch> Detect(
        ImageFrame image,
        IReadOnlyList<BountyCardAction> actions)
    {
        Validate(image);
        byte[] mask = BuildMask(image);
        using Mat search = ImageCodec.ToMat(
            new ImageFrame(
                SearchWidth,
                SearchHeight,
                PixelFormat.Gray8,
                mask,
                takeOwnership: true));
        List<BountyNumberMatch> matches = [];
        foreach (BountyCardAction action in
                 actions
                     .GroupBy(value => value.X)
                     .Select(group => group.First()))
        {
            int left = action.X -
                ActionWindowLeft -
                SearchX;
            int top = action.Y -
                ActionWindowTop -
                SearchY;
            if (left < 0 ||
                top < 0 ||
                left + ActionWindowWidth >
                search.Width ||
                top + ActionWindowHeight >
                search.Height)
            {
                continue;
            }

            using Mat window = new(
                search,
                new Rect(
                    left,
                    top,
                    ActionWindowWidth,
                    ActionWindowHeight));
            Candidate? best = null;
            foreach (Template template in Templates)
            {
                using Mat result = new();
                Cv2.MatchTemplate(
                    window,
                    template.Mask,
                    result,
                    TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(
                    result,
                    out _,
                    out double maximum,
                    out _,
                    out Point location);
                Candidate candidate = new(
                    template,
                    maximum,
                    location);
                if (best is null ||
                    candidate.Confidence >
                    best.Value.Confidence ||
                    Math.Abs(
                        candidate.Confidence -
                        best.Value.Confidence) <
                        0.000001 &&
                    candidate.Template.Width >
                    best.Value.Template.Width)
                {
                    best = candidate;
                }
            }
            if (best is Candidate match &&
                match.Confidence >= MatchThreshold)
            {
                matches.Add(
                    new BountyNumberMatch(
                        match.Template.Number,
                        match.Confidence,
                        SearchX + left +
                            match.Location.X +
                            match.Template.Width / 2,
                        SearchY + top +
                            match.Location.Y + 3));
            }
        }
        BountyNumberMatch[] ordered = matches
            .OrderBy(value => value.CenterX)
            .ToArray();
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
            ImageCodec.ToMat(
                new ImageFrame(
                    width,
                    7,
                    PixelFormat.Gray8,
                    pixels,
                    takeOwnership: true)));
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
        Mat Mask);

    private readonly record struct Candidate(
        Template Template,
        double Confidence,
        Point Location);
}
