using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Bounties;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class BountyNumberRecognizerTests
{
    private static readonly string[][] NumberGlyphs =
    [
        ["..#..#...#", "..#..#..##", ".#####..##", "..#.#....#", "######...#", ".#..#....#", ".#..#....#"],
        ["..#..#...##..", "..#..#..####.", ".#####..#..#.", "..#.#......#.", "######...##..", ".#..#...###..", ".#..#...#####"],
        ["..#..#..###.", "..#..#..#.##", ".#####....##", "..#.#....##.", "######.....#", ".#..#..##.##", ".#..#...###."],
        ["..#..#..#.#", "..#..#..#.#", ".#####.##.#", "..#.#..##.#", "######.####", ".#..#.....#", ".#..#.....#"],
        ["..#..#...###", "..#..#..##..", ".#####..##..", "..#.#...###.", "######.....#", ".#..#...####", ".#..#....##."],
        ["..#..#....##", "..#..#...##.", ".#####...#..", "..#.#...###.", "######..####", ".#..#...####", ".#..#....##."],
        ["..#..#.#####", "..#..#....##", ".#####....#.", "..#.#....##.", "######...#..", ".#..#...##..", ".#..#...#..."],
        ["..#..#...###.", "..#..#..#####", ".#####..#####", "..#.#....###.", "######..##..#", ".#..#...#####", ".#..#....###."],
        ["..#..#...##.", "..#..#..####", ".#####..##.#", "..#.#....###", "######....##", ".#..#.....#.", ".#..#...##.."],
        ["..#..#...#...##..", "..#..#..##...###.", ".#####..##..##.##", "..#.#....#..#...#", "######...#..##.##", ".#..#....#...#.#.", ".#..#....#...###."],
    ];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Detect_UsesOnlyNumberSuffix(
        int number)
    {
        const int actionX = 315;
        const int actionY = 359;
        ImageFrame image = CreateFrame();
        DrawGlyph(
            image,
            NumberGlyphs[number - 1],
            centerX: actionX - 7,
            top: actionY - 97);

        BountyNumberMatch match = Assert.Single(
            BountyNumberRecognizer.Detect(
                image,
                [
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        actionX,
                        actionY),
                ]));

        Assert.Equal(number, match.Number);
        Assert.True(match.Confidence >= 0.985);
    }

    [Fact]
    public void Detect_IgnoresSuffixAwayFromVerifiedCardAction()
    {
        ImageFrame image = CreateFrame();
        DrawGlyph(
            image,
            NumberGlyphs[7],
            centerX: 500,
            top: 260);

        Assert.Empty(
            BountyNumberRecognizer.Detect(
                image,
                [
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        315,
                        359),
                ]));
    }

    [Fact]
    public void Detect_UsesTheCardAnchorForAClaimAction()
    {
        const int cardAnchorX = 315;
        const int actionY = 359;
        ImageFrame image = CreateFrame();
        DrawGlyph(
            image,
            NumberGlyphs[3],
            centerX: cardAnchorX - 7,
            top: actionY - 97);

        BountyNumberMatch match = Assert.Single(
            BountyNumberRecognizer.Detect(
                image,
                [
                    new BountyCardAction(
                        BountyCardActionKind.Claim,
                        cardAnchorX - 40,
                        actionY),
                ]));

        Assert.Equal(4, match.Number);
    }

    [Fact]
    public void Detect_CollapsesClaimAndRerollEvidenceForOneCard()
    {
        const int cardAnchorX = 315;
        const int actionY = 359;
        ImageFrame image = CreateFrame();
        DrawGlyph(
            image,
            NumberGlyphs[3],
            centerX: cardAnchorX - 7,
            top: actionY - 97);

        BountyNumberMatch match = Assert.Single(
            BountyNumberRecognizer.Detect(
                image,
                [
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        cardAnchorX,
                        actionY),
                    new BountyCardAction(
                        BountyCardActionKind.Claim,
                        cardAnchorX - 40,
                        actionY),
                ]));

        Assert.Equal(4, match.Number);
    }

    [Fact]
    public void Detect_RecognizesReviewedRandomizedPaperLayouts()
    {
        ImageFrame image = ImageCodec.Load(
            Path.Combine(
                TestPaths.BountyDatasets,
                "BountyBoard_FourLiveOneDimmed_01.png"));

        IReadOnlyList<BountyNumberMatch> matches =
            BountyNumberRecognizer.Detect(
                image,
                [
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        263,
                        356),
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        387,
                        412),
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        512,
                        362),
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        636,
                        445),
                ]);

        Assert.Equal(
            [1, 2, 6, 10],
            matches.Select(value =>
                value.Number));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Detect_AcceptsOneMissingRasterPixel(
        int number)
    {
        const int actionX = 315;
        const int actionY = 359;
        const int centerX = actionX - 7;
        const int top = actionY - 97;
        ImageFrame image = CreateFrame();
        DrawGlyph(
            image,
            NumberGlyphs[number - 1],
            centerX,
            top);
        ClearGlyphPixel(
            image,
            NumberGlyphs[number - 1],
            centerX,
            top,
            localX: 2,
            localY: 0);

        BountyNumberMatch match = Assert.Single(
            BountyNumberRecognizer.Detect(
                image,
                [
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        actionX,
                        actionY),
                ]));

        Assert.Equal(number, match.Number);
    }

    [Fact]
    public void Detect_TenDoesNotFallBackToItsExactOnePrefix()
    {
        const int actionX = 315;
        const int actionY = 359;
        const int centerX = actionX - 7;
        const int top = actionY - 97;
        ImageFrame image = CreateFrame();
        DrawGlyph(
            image,
            NumberGlyphs[9],
            centerX,
            top);
        ClearGlyphPixel(
            image,
            NumberGlyphs[9],
            centerX,
            top,
            localX: 13,
            localY: 0);

        BountyNumberMatch match = Assert.Single(
            BountyNumberRecognizer.Detect(
                image,
                [
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        actionX,
                        actionY),
                ]));

        Assert.Equal(10, match.Number);
    }

    [Fact]
    public void Detect_RejectsMoreThanOneUnexpectedSuffixPixel()
    {
        const int actionX = 315;
        const int actionY = 359;
        const int centerX = actionX - 7;
        const int top = actionY - 97;
        ImageFrame image = CreateFrame();
        DrawGlyph(
            image,
            NumberGlyphs[0],
            centerX,
            top);
        SetPixel(
            image,
            centerX + 6,
            top,
            255);
        SetPixel(
            image,
            centerX + 6,
            top + 1,
            255);

        Assert.Empty(
            BountyNumberRecognizer.Detect(
                image,
                [
                    new BountyCardAction(
                        BountyCardActionKind.Reroll,
                        actionX,
                        actionY),
                ]));
    }

    private static ImageFrame CreateFrame() =>
        new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3],
            takeOwnership: true);

    private static void DrawGlyph(
        ImageFrame image,
        IReadOnlyList<string> rows,
        int centerX,
        int top)
    {
        int left = centerX -
            rows[0].Length / 2;
        for (int y = 0; y < rows.Count; y++)
        {
            for (int x = 0;
                 x < rows[y].Length;
                 x++)
            {
                if (rows[y][x] != '#')
                {
                    continue;
                }
                int pixel =
                    ((top + y) * image.Width +
                     left + x) * 3;
                image.Pixels[pixel] = 255;
                image.Pixels[pixel + 1] = 255;
                image.Pixels[pixel + 2] = 255;
            }
        }
    }

    private static void ClearGlyphPixel(
        ImageFrame image,
        IReadOnlyList<string> rows,
        int centerX,
        int top,
        int localX,
        int localY)
    {
        Assert.Equal(
            '#',
            rows[localY][localX]);
        int left = centerX -
            rows[0].Length / 2;
        SetPixel(
            image,
            left + localX,
            top + localY,
            0);
    }

    private static void SetPixel(
        ImageFrame image,
        int x,
        int y,
        byte value)
    {
        int pixel =
            (y * image.Width + x) * 3;
        image.Pixels[pixel] = value;
        image.Pixels[pixel + 1] = value;
        image.Pixels[pixel + 2] = value;
    }
}
