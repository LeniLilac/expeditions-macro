using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Tests;

public sealed class BountyNoGoldRecognizerTests
{
    private const int TemplateWidth = 173;
    private const int TemplateHeight = 9;
    private const int TemplateLeft = 318;
    private const int TemplateTop = 40;

    private static readonly byte[] Template =
        Expand(
            "EQAAAICIYRhwIBAAAABQAIFAAAAAcAIAAACQcRwHDwQiAAAACiQACAAAAmom4TDGM9u2Mbh43nHmXMEfGc8l51HmJzzvfSSKIvafj33u3S+wp+P/5HnL7ITlvY1s28a4s5FNvNgFkmTsnSw5iP2QjLERRVGw9za2iRG7wZKevZ9kBuEcEuc84jgOnJuHHOHCJVDS8ZyTSAQAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAIAA");

    [Fact]
    public void Score_AcceptsOwnedRedAlertBanner()
    {
        ImageFrame frame = CreateFrame();
        DrawBackdrop(frame);
        DrawTemplate(frame);

        Assert.True(
            BountyNoGoldRecognizer.Score(frame) >=
            0.90);
    }

    [Fact]
    public void Score_RejectsMatchingTextWithoutRedAlertBanner()
    {
        ImageFrame frame = CreateFrame();
        DrawTemplate(frame);

        Assert.Equal(
            0,
            BountyNoGoldRecognizer.Score(frame));
    }

    [Fact]
    public void Score_RejectsRedAlertWithoutMatchingText()
    {
        ImageFrame frame = CreateFrame();
        DrawBackdrop(frame);

        Assert.Equal(
            0,
            BountyNoGoldRecognizer.Score(frame));
    }

    private static ImageFrame CreateFrame() =>
        new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3],
            takeOwnership: true);

    private static void DrawBackdrop(
        ImageFrame frame)
    {
        for (int y = TemplateTop - 4;
             y < TemplateTop + TemplateHeight + 4;
             y++)
        {
            for (int x = TemplateLeft - 6;
                 x < TemplateLeft + TemplateWidth + 6;
                 x++)
            {
                SetPixel(
                    frame,
                    x,
                    y,
                    red: 135,
                    green: 35,
                    blue: 30);
            }
        }
    }

    private static void DrawTemplate(
        ImageFrame frame)
    {
        for (int y = 0; y < TemplateHeight; y++)
        {
            for (int x = 0; x < TemplateWidth; x++)
            {
                if (Template[
                        y * TemplateWidth + x] == 0)
                {
                    continue;
                }
                SetPixel(
                    frame,
                    TemplateLeft + x,
                    TemplateTop + y,
                    red: 255,
                    green: 255,
                    blue: 255);
            }
        }
    }

    private static void SetPixel(
        ImageFrame frame,
        int x,
        int y,
        byte red,
        byte green,
        byte blue)
    {
        int pixel =
            (y * frame.Width + x) * 3;
        frame.Pixels[pixel] = red;
        frame.Pixels[pixel + 1] = green;
        frame.Pixels[pixel + 2] = blue;
    }

    private static byte[] Expand(
        string encoded)
    {
        byte[] packed =
            Convert.FromBase64String(encoded);
        byte[] pixels =
            new byte[
                TemplateWidth * TemplateHeight];
        for (int bit = 0;
             bit < pixels.Length;
             bit++)
        {
            if ((packed[bit / 8] &
                 (1 << (bit % 8))) != 0)
            {
                pixels[bit] = 255;
            }
        }
        return pixels;
    }
}
