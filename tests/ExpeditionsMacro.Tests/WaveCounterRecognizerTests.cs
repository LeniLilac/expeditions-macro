using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Tests;

public sealed class WaveCounterRecognizerTests
{
    [Fact]
    public void EmbeddedTemplates_AreValidAndLoadable()
    {
        ImageFrame frame = new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3],
            takeOwnership: true);

        Exception? error = Record.Exception(
            () => WaveCounterRecognizer.Detect(
                frame));

        Assert.Null(error);
    }
}
