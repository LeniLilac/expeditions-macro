using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Bounties;

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
}
