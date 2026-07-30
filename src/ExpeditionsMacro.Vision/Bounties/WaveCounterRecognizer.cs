using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Bounties;

public readonly record struct WaveCounterMatch(
    int Wave,
    double Confidence,
    int Distance,
    int Margin);

public static class WaveCounterRecognizer
{
    private const int CounterX = 389;
    private const int CounterY = 48;
    private const int Width = 16;
    private const int Height = 11;
    private const int BytesPerTemplate = 22;
    private const int MaximumDistance = 25;
    private const int MinimumMargin = 3;

    private static readonly byte[] Templates =
        Convert.FromBase64String(
            "AAAAAIABwANAAkACQALAA4ABAAAAAAAAAAAAAYABwAEAAQABAAEAAQAAAAAAAAAAgAHAA0ACAAOAAcADwAMAAAAAAAAAAIABwAMAA4ADQALAA4ABAAAAAAAAAABAAWABIAHgAeABAAEAAQAAAAAAAAAAgAPAAcAAwAMAA8ADgAEAAAAAAAAAAAADgAHAAMABwAPAA4ABAAAAAAAAAADgAeABgAGAAMAAQABAAAAAAAAAAAAAgAPAA8ADwAPAAsADgAEAAAAAAAAAAIABwAFAA8ADgAHAAcAAAAAAAAAAAAAgA7AHuASgBKAEoAcgAwAAAAAAAAAAQAJgA/ADQAJAAkACQAIAAAAAAAAAACADsAe4BCAGIAOgB6AHAAAAAAAAAACgA7AHOAYgB6AEoAcgAwAAAAAAAAAAoALwAngC4APgAyACIAIAAAAAAAAAAEAOYA9wA0APQAxAD0AGAAAAAAAAAAAgBjADuAGgA6AHoAcgAwAAAAAAAAAA4APwAzgDIAGgAaAAoAAAAAAAAAAAACAHsAe4B6AHoAWgByADAAAAAAAAAAAgA7ADuAagByADoAOgAQAAAAAAAAAAMAZ4D0gJYAkwCXgPeAYAAAAAAAAAAGAE8AaQB8AEYATwBPAEAAAAAAAAAAAwBngPSAlgDDAGeA94DwAAAAAAAAAAMAd4D0gMYA4wCXgPeAYAAAAAAAAAADAF+AXIBOAHsAd4BHgEAAAAAAAAAABgHPAekAbAHmAY8B7wDAAAAAAAAAAAMAx4BkgDYAcwD3gPeAYAAAAAAAAAALAH+AdIBmACMAN4AXgBAAAAAAAAAAAwDngPSA9gDzALeA94BgAAAAAAAAAAMAZ4B0gNYA8wBngHeAMAAAAAAAAAADgGeA9gCXAJSAl4DzAGAAAAAAAAAABwBPAGwAfgBJAE8ARgBAAAAAAAAAAAOAZ4D2AJcAxIBngPMA8AAAAAAAAAADgHeA9gDHAOSAl4DzAGAAAAAAAAAAA4BfgF4ATwB8gHeAQwBAAAAAAAAAAAcBzwHsAG4B6QGPAeYAwAAAAAAAAAADgMeAZgA3AHSA94DzAGAAAAAAAAAAC4B/gHYAZwAkgDeAEwAQAAAAAAAAAAOA54D2APcA9IC3gPMAYAAAAAAAAAADgGeAdgDXAPSAZ4BzADAAAAAAAAAAAoBiwPJAk8CTwJIA8gBgAAAAAAAAAAUARYBkgHeAR4BEAEQAQAAAAAAAAAACgGLA8kCTwMPAYgDyAPAAAAAAAAAAAoBywPJAw8DjwJIA8gBgAAAAAAAAAAKAWsBaQEvAe8ByAEIAQAAAAAAAAAAFAcWB5IBngeeBhAHkAMAAAAAAAAAAAoDCwGJAM8BzwPIA8gBgAAAAAAAAAAqAesByQGPAI8AyABIAEAAAAAAAAAACgOLA8kDzwPPAsgDyAGAAAAAAAAAAAoBiwHJA08DzwGIAcgAwAAAAAAAAAA4AZwDzAJ8AnACfAPYAYAAAAAAAAAAMAF4AdgB+AFgAXgBcAEAAAAAAAAAADgBvAPMAnwDMAG8A9gDwAAAAAAAAAA4AbwDzAM8A7ACfAPYAYAAAAAAAAAAOAF8AWwBPAHwAfwBGAEAAAAAAAAAADgDnAPMAPwD8AM8A9gBgAAAAAAAAAA4AxwBjAD8AfAD/APYAYAAAAAAAAAAOAH8AcwBvACwAPwAWABAAAAAAAAAADgDnAPMA/wD8AL8A9gBgAAAAAAAAAA4AbwBzAN8A/ABvAHYAMAAAAAAAAAAGAGMA8YCTgJeAl4DzAGAAAAAAAAAADABGAGMAdwBPAE8ARgBAAAAAAAAAAAYAYwDxgJOAx4BngPMA8AAAAAAAAAAGAHMA8YDDgOeAl4DzAGAAAAAAAAAABgBbAFmAS4B/gHeAQwBAAAAAAAAAAAwBxgHjAGcB7wGPAeYAwAAAAAAAAAAGAMMAYYAzgHeA94DzAGAAAAAAAAAADgB7AHGAY4AngDeAEwAQAAAAAAAAAAYA4wDxgPOA94C3gPMAYAAAAAAAAAAGAGMAcYDTgPeAZ4BzADAAAAAAAAAAA8BjwPMAkQCRgJCA8IBgAAAAAAAAAAeAR4BmAHIAQwBBAEEAQAAAAAAAAAADwGPA8wCRAMGAYIDwgPAAAAAAAAAAA8BzwPMAwQDhgJCA8IBgAAAAAAAAAAPAW8BbAEkAeYBwgECAQAAAAAAAAAAHgceA5gBiAeMBgQHhAMAAAAAAAAAAA8DDwGMAMQBxgPCA8IBgAAAAAAAAAAvAe8BzAGEAIYAwgBCAEAAAAAAAAAADwOPA8wDxAPGAsIDwgGAAAAAAAAAAA8BjwHMA0QDxgGCAcIAwAAAAAAAAAAcAZ4D3gJeAlYCXgPMAYAAAAAAAAAAOAE8AbwB/AEsATwBGAEAAAAAAAAAABwBngPeAl4DFgGeA8wDwAAAAAAAAAAcAZ4D3gMeA5YCXgPMAYAAAAAAAAAAHAF+AX4BPgH2Ad4BDAEAAAAAAAAAADgHPAO8AbwHrAY8B5gDAAAAAAAAAAAcAx4BngDeAdYD3gPMAYAAAAAAAAAAPAH+Ad4BngCWAN4ATABAAAAAAAAAABwDngPeA94D1gLeA8wBgAAAAAAAAAAcAZ4B3gNeA9YBngHMAMAAAAAAAAAADAGOA9oCTgJMAk4DxgGAAAAAAAAAABgBHAG0AfwBGAEcAQwBAAAAAAAAAAAMAY4D2gJeAwwBjgPGA8AAAAAAAAAADAGOA9oDHgOMAk4DxgGAAAAAAAAAAAwBbgF6AT4B7AHOAQYBAAAAAAAAAAAYBxwDtAG8B5gGHAeMAwAAAAAAAAAADAMOAZoAzgHMA84DxgGAAAAAAAAAACwB7gHaAZ4AjADOAEYAQAAAAAAAAAAMA44D2gPeA8wCzgPGAYAAAAAAAAAADAGOAdoDXgPMAY4BxgDAAAAAAAAAADIGOw9LiUoJSgl6D3IGAAAAAA==");

    public static WaveCounterMatch? Detect(
        ImageFrame image)
    {
        Validate(image);
        Span<byte> observed = stackalloc byte[BytesPerTemplate];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int pixel =
                    ((CounterY + y) * image.Width +
                     CounterX + x) * 3;
                int luminance =
                    (299 * image.Pixels[pixel] +
                     587 * image.Pixels[pixel + 1] +
                     114 * image.Pixels[pixel + 2]) /
                    1000;
                if (luminance > 140)
                {
                    int bit = y * Width + x;
                    observed[bit / 8] |=
                        (byte)(1 << (bit % 8));
                }
            }
        }

        int bestWave = -1;
        int bestDistance = int.MaxValue;
        int secondDistance = int.MaxValue;
        for (int wave = 0; wave <= 100; wave++)
        {
            int distance = 0;
            int offset = wave * BytesPerTemplate;
            for (int index = 0;
                 index < BytesPerTemplate;
                 index++)
            {
                distance +=
                    System.Numerics.BitOperations.PopCount(
                        (uint)(observed[index] ^
                            Templates[offset + index]));
            }
            if (distance < bestDistance)
            {
                secondDistance = bestDistance;
                bestDistance = distance;
                bestWave = wave;
            }
            else if (distance < secondDistance)
            {
                secondDistance = distance;
            }
        }

        int margin = secondDistance - bestDistance;
        WaveCounterMatch? match =
            bestDistance <= MaximumDistance &&
            margin >= MinimumMargin
                ? new WaveCounterMatch(
                    bestWave,
                    Math.Clamp(
                        1d -
                        bestDistance /
                        (double)(Width * Height),
                        0,
                        1),
                    bestDistance,
                    margin)
                : null;
        VisionTrace.Emit(
            "bounty_wave_counter",
            match?.Wave.ToString() ?? "none",
            match?.Confidence ?? 0,
            new
            {
                BestWave = bestWave,
                bestDistance,
                margin,
            });
        return match;
    }

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new ArgumentException(
                "Wave counter detection requires an 808 by 611 RGB client capture.",
                nameof(image));
        }
    }
}
