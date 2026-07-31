using System.Reflection;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Tests;

public sealed class WaveCounterRecognizerTests
{
    private const int CounterX = 389;
    private const int CounterY = 48;
    private const int TemplateWidth = 16;
    private const int TemplateHeight = 11;
    private const int BytesPerTemplate = 22;

    [Fact]
    public void EmbeddedTemplates_AreValidAndLoadable()
    {
        Exception? error = Record.Exception(
            () => WaveCounterRecognizer.Detect(
                CreateFrame()));

        Assert.Null(error);
    }

    [Fact]
    public void Detect_BlankCounterIsUnknown()
    {
        Assert.Null(
            WaveCounterRecognizer.Detect(
                CreateFrame()));
    }

    [Fact]
    public void Detect_PartialGlyphIsUnknown()
    {
        Assert.Null(
            WaveCounterRecognizer.Detect(
                CreateTemplateFrame(
                    wave: 1,
                    maximumPixels: 6)));
    }

    [Fact]
    public void Detect_RecognizesEveryEmbeddedWave()
    {
        for (int wave = 0; wave <= 100; wave++)
        {
            WaveCounterMatch match = Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(
                    CreateTemplateFrame(wave)));

            Assert.Equal(wave, match.Wave);
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    public void Detect_ToleratesOnePixelCounterPhase(
        int offsetX,
        int offsetY)
    {
        for (int wave = 0; wave <= 100; wave++)
        {
            WaveCounterMatch match = Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(
                    CreateTemplateFrame(
                        wave,
                        offsetX,
                        offsetY)));

            Assert.Equal(wave, match.Wave);
        }
    }

    private static ImageFrame CreateFrame() =>
        new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3],
            takeOwnership: true);

    private static ImageFrame CreateTemplateFrame(
        int wave,
        int offsetX = 0,
        int offsetY = 0,
        int maximumPixels = int.MaxValue)
    {
        ImageFrame frame = CreateFrame();
        byte[] templates = EmbeddedTemplates();
        int templateOffset =
            wave * BytesPerTemplate;
        int drawnPixels = 0;
        for (int y = 0; y < TemplateHeight; y++)
        {
            for (int x = 0; x < TemplateWidth; x++)
            {
                int bit = y * TemplateWidth + x;
                if ((templates[
                         templateOffset +
                         bit / 8] &
                     (1 << (bit % 8))) == 0)
                {
                    continue;
                }
                if (drawnPixels >= maximumPixels)
                {
                    continue;
                }
                int pixel =
                    ((CounterY + offsetY + y) *
                         frame.Width +
                     CounterX + offsetX + x) * 3;
                frame.Pixels[pixel] = 255;
                frame.Pixels[pixel + 1] = 255;
                frame.Pixels[pixel + 2] = 255;
                drawnPixels++;
            }
        }
        return frame;
    }

    private static byte[] EmbeddedTemplates()
    {
        FieldInfo? field =
            typeof(WaveCounterRecognizer)
                .GetField(
                    "Templates",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);
        Assert.NotNull(field);
        return Assert.IsType<byte[]>(
            field.GetValue(null));
    }
}
