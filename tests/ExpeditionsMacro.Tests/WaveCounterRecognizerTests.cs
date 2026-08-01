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
    private const int TypeThreeCounterX = 386;
    private const int TypeThreeCounterY = 28;
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
    [InlineData(LegacyCounterX, LegacyCounterY, -1, 0)]
    [InlineData(LegacyCounterX, LegacyCounterY, 1, 0)]
    [InlineData(LegacyCounterX, LegacyCounterY, 0, -1)]
    [InlineData(LegacyCounterX, LegacyCounterY, 0, 1)]
    [InlineData(NoVoiceCounterX, NoVoiceCounterY, -1, 0)]
    [InlineData(NoVoiceCounterX, NoVoiceCounterY, 1, 0)]
    [InlineData(NoVoiceCounterX, NoVoiceCounterY, 0, -1)]
    [InlineData(NoVoiceCounterX, NoVoiceCounterY, 0, 1)]
    [InlineData(TypeThreeCounterX, TypeThreeCounterY, -1, 0)]
    [InlineData(TypeThreeCounterX, TypeThreeCounterY, 1, 0)]
    [InlineData(TypeThreeCounterX, TypeThreeCounterY, 0, -1)]
    [InlineData(TypeThreeCounterX, TypeThreeCounterY, 0, 1)]
    public void Detect_ToleratesOnePixelCounterPhase(
        int counterX,
        int counterY,
        int offsetX,
        int offsetY)
    {
        for (int wave = 0; wave <= 100; wave++)
        {
            WaveCounterMatch match = Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(
                    CreateTemplateFrame(
                        wave,
                        counterX,
                        counterY,
                        offsetX: offsetX,
                        offsetY: offsetY)));

            Assert.Equal(wave, match.Wave);
        }
    }

    [Theory]
    [InlineData(389, 48)]
    [InlineData(421, 28)]
    [InlineData(386, 28)]
    public void Detect_RecognizesEveryWaveAtAllTopBarLayouts(
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
    public void Detect_RecognizesReviewedTypeThreeLayout()
    {
        WaveCounterMatch match =
            Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(
                    ExpeditionsMacro.Vision
                        .Infrastructure
                        .ImageCodec.Load(
                            TypeThreeFixturePath())));

        Assert.Equal(37, match.Wave);
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
    public void Detect_RecognizesReviewedBrightNoVoiceLayout()
    {
        WaveCounterMatch match =
            Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(
                    ExpeditionsMacro.Vision
                        .Infrastructure
                        .ImageCodec.Load(
                            BrightNoVoiceFixturePath())));

        Assert.Equal(1, match.Wave);
    }

    [Fact]
    public void Detect_RecognizesTranslucentPillAgainstBrightScene()
    {
        ImageFrame frame = CreateFrame();
        DrawPillBands(
            frame,
            NoVoiceCounterX,
            NoVoiceCounterY,
            background: 190,
            rail: 110);
        DrawCounterOwner(
            frame,
            NoVoiceCounterX,
            NoVoiceCounterY);
        DrawTemplate(
            frame,
            wave: 30,
            NoVoiceCounterX,
            NoVoiceCounterY);

        WaveCounterMatch match =
            Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(frame));

        Assert.Equal(30, match.Wave);
    }

    [Fact]
    public void Detect_RejectsBadgeAndLabelWithoutPillContrast()
    {
        ImageFrame frame = CreateFrame();
        DrawPillBands(
            frame,
            NoVoiceCounterX,
            NoVoiceCounterY,
            background: 150,
            rail: 150);
        DrawCounterOwner(
            frame,
            NoVoiceCounterX,
            NoVoiceCounterY);
        DrawTemplate(
            frame,
            wave: 30,
            NoVoiceCounterX,
            NoVoiceCounterY);

        Assert.Null(
            WaveCounterOwnerDetector.Detect(frame));
    }

    [Theory]
    [InlineData(
        NoVoiceCounterX,
        NoVoiceCounterY,
        TypeThreeCounterX,
        TypeThreeCounterY)]
    [InlineData(
        TypeThreeCounterX,
        TypeThreeCounterY,
        NoVoiceCounterX,
        NoVoiceCounterY)]
    [InlineData(
        LegacyCounterX,
        LegacyCounterY,
        TypeThreeCounterX,
        TypeThreeCounterY)]
    [InlineData(
        TypeThreeCounterX,
        TypeThreeCounterY,
        LegacyCounterX,
        LegacyCounterY)]
    public void Detect_IgnoresInactiveAnchorGlyph(
        int activeX,
        int activeY,
        int inactiveX,
        int inactiveY)
    {
        ImageFrame frame = CreateTemplateFrame(
            wave: 30,
            counterX: activeX,
            counterY: activeY);
        DrawTemplate(
            frame,
            wave: 62,
            inactiveX,
            inactiveY);

        WaveCounterMatch match =
            Assert.IsType<WaveCounterMatch>(
                WaveCounterRecognizer.Detect(frame));

        Assert.Equal(30, match.Wave);
    }

    [Theory]
    [InlineData(
        NoVoiceCounterX,
        NoVoiceCounterY,
        LegacyCounterX,
        LegacyCounterY)]
    [InlineData(
        TypeThreeCounterX,
        TypeThreeCounterY,
        LegacyCounterX,
        LegacyCounterY)]
    [InlineData(
        TypeThreeCounterX,
        TypeThreeCounterY,
        NoVoiceCounterX,
        NoVoiceCounterY)]
    public void Detect_TwoOwnedCounterLayoutsAreAmbiguous(
        int firstX,
        int firstY,
        int secondX,
        int secondY)
    {
        ImageFrame frame = CreateTemplateFrame(
            wave: 30,
            counterX: firstX,
            counterY: firstY);
        DrawCounterOwner(
            frame,
            secondX,
            secondY);
        DrawTemplate(
            frame,
            wave: 62,
            secondX,
            secondY);

        Assert.Null(
            WaveCounterRecognizer.Detect(frame));
    }

    [Theory]
    [InlineData("WaveCounterNoVoice.png")]
    [InlineData("WaveCounterType3.png")]
    [InlineData("WaveCounterNoVoiceBrightScene.png")]
    public void Detect_FieldFixturesRetainGameplayOwnerEvidence(
        string fixture)
    {
        ImageFrame image =
            ExpeditionsMacro.Vision
                .Infrastructure
                .ImageCodec.Load(
                    FixturePath(fixture));

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

    private static void DrawPillBands(
        ImageFrame frame,
        int counterX,
        int counterY,
        byte background,
        byte rail)
    {
        FillRegion(
            frame,
            counterX - 3,
            counterY - 8,
            width: 30,
            height: 5,
            background);
        FillRegion(
            frame,
            counterX - 3,
            counterY - 3,
            width: 30,
            height: 5,
            rail);
        FillRegion(
            frame,
            counterX - 3,
            counterY + 9,
            width: 30,
            height: 5,
            rail);
        FillRegion(
            frame,
            counterX - 3,
            counterY + 14,
            width: 30,
            height: 5,
            background);
    }

    private static void FillRegion(
        ImageFrame frame,
        int left,
        int top,
        int width,
        int height,
        byte value)
    {
        for (int y = top;
             y < top + height;
             y++)
        {
            for (int x = left;
                 x < left + width;
                 x++)
            {
                SetPixel(
                    frame,
                    x,
                    y,
                    value,
                    value,
                    value);
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
        FixturePath("WaveCounterNoVoice.png");

    private static string LegacyFixturePath() =>
        FixturePath("WaveCounterLegacy.png");

    private static string TypeThreeFixturePath() =>
        FixturePath("WaveCounterType3.png");

    private static string BrightNoVoiceFixturePath() =>
        FixturePath(
            "WaveCounterNoVoiceBrightScene.png");

    private static string FixturePath(string fixture) =>
        Path.Combine(
            TestPaths.RepositoryRoot,
            "datasets",
            "anime-expeditions",
            "bounties",
            fixture);
}
