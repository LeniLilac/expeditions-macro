using System.Reflection;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Bounties;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class WaveCounterRecognizerTests
{
    private const int LegacyCounterX = 389;
    private const int LegacyCounterY = 48;
    private const int NoVoiceCounterX = 421;
    private const int NoVoiceCounterY = 28;
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
                        offsetX: offsetX,
                        offsetY: offsetY)));

            Assert.Equal(wave, match.Wave);
        }
    }

    [Theory]
    [InlineData(389, 48)]
    [InlineData(421, 28)]
    public void Detect_RecognizesEveryWaveAtBothTopBarLayouts(
        int counterX,
        int counterY)
    {
        for (int wave = 0; wave <= 100; wave++)
        {
            WaveCounterMatch match =
                Assert.IsType<WaveCounterMatch>(
                    WaveCounterRecognizer.Detect(
                        CreateTemplateFrame(
                            wave,
                            counterX: counterX,
                            counterY: counterY)));

            Assert.Equal(wave, match.Wave);
        }
    }

    [Fact]
    public void Detect_RecognizesReviewedNoVoiceLayout()
    {
        string path = NoVoiceFixturePath();

        WaveCounterMatch match =
            Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(
                    ExpeditionsMacro.Vision
                        .Infrastructure
                        .ImageCodec.Load(path)));

        Assert.Equal(2, match.Wave);
    }

    [Fact]
    public void Detect_RecognizesReviewedLegacyLayout()
    {
        WaveCounterMatch match =
            Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(
                    ExpeditionsMacro.Vision
                        .Infrastructure
                        .ImageCodec.Load(
                            LegacyFixturePath())));

        Assert.Equal(67, match.Wave);
    }

    [Fact]
    public void Detect_IgnoresInactiveAnchorGlyph()
    {
        ImageFrame frame = CreateTemplateFrame(
            wave: 30,
            counterX: NoVoiceCounterX,
            counterY: NoVoiceCounterY);
        DrawTemplate(
            frame,
            wave: 62,
            LegacyCounterX,
            LegacyCounterY);

        WaveCounterMatch match =
            Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(frame));

        Assert.Equal(30, match.Wave);
    }

    [Fact]
    public void Detect_TwoOwnedCounterLayoutsAreAmbiguous()
    {
        ImageFrame frame = CreateTemplateFrame(
            wave: 30,
            counterX: NoVoiceCounterX,
            counterY: NoVoiceCounterY);
        DrawCounterOwner(
            frame,
            LegacyCounterX,
            LegacyCounterY);
        DrawTemplate(
            frame,
            wave: 62,
            LegacyCounterX,
            LegacyCounterY);

        Assert.Null(
            WaveCounterRecognizer.Detect(frame));
    }

    [Fact]
    public void Detect_NoVoiceFixtureRetainsGameplayOwnerEvidence()
    {
        ImageFrame image =
            ExpeditionsMacro.Vision
                .Infrastructure
                .ImageCodec.Load(
                    NoVoiceFixturePath());

        Assert.True(
            StageGameplayHudDetector
                .Detect(image)
                .Visible);
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
        int counterX = LegacyCounterX,
        int counterY = LegacyCounterY,
        int offsetX = 0,
        int offsetY = 0,
        int maximumPixels = int.MaxValue)
    {
        ImageFrame frame = CreateFrame();
        DrawCounterOwner(
            frame,
            counterX,
            counterY);
        DrawTemplate(
            frame,
            wave,
            counterX,
            counterY,
            offsetX,
            offsetY,
            maximumPixels);
        return frame;
    }

    private static void DrawTemplate(
        ImageFrame frame,
        int wave,
        int counterX,
        int counterY,
        int offsetX = 0,
        int offsetY = 0,
        int maximumPixels = int.MaxValue)
    {
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
                    ((counterY + offsetY + y) *
                         frame.Width +
                     counterX + offsetX + x) * 3;
                frame.Pixels[pixel] = 255;
                frame.Pixels[pixel + 1] = 255;
                frame.Pixels[pixel + 2] = 255;
                drawnPixels++;
            }
        }
    }

    private static void DrawCounterOwner(
        ImageFrame frame,
        int counterX,
        int counterY)
    {
        for (int y = counterY;
             y < counterY + 6;
             y++)
        {
            for (int x = counterX - 15;
                 x < counterX - 9;
                 x++)
            {
                SetPixel(
                    frame,
                    x,
                    y,
                    red: 20,
                    green: 60,
                    blue: 150);
            }
        }
        for (int y = counterY + 4;
             y < counterY + 7;
             y++)
        {
            for (int x = counterX + 16;
                 x < counterX + 21;
                 x++)
            {
                SetPixel(
                    frame,
                    x,
                    y,
                    red: 100,
                    green: 100,
                    blue: 100);
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

    private static string NoVoiceFixturePath() =>
        Path.Combine(
            TestPaths.RepositoryRoot,
            "datasets",
            "anime-expeditions",
            "bounties",
            "WaveCounterNoVoice.png");

    private static string LegacyFixturePath() =>
        Path.Combine(
            TestPaths.RepositoryRoot,
            "datasets",
            "anime-expeditions",
            "bounties",
            "WaveCounterLegacy.png");
}
