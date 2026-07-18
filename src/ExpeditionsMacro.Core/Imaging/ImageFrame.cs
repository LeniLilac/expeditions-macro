using ExpeditionsMacro.Core.Geometry;

namespace ExpeditionsMacro.Core.Imaging;

public enum PixelFormat
{
    Gray8 = 1,
    Rgb24 = 3,
}

public sealed class ImageFrame
{
    public ImageFrame(int width, int height, PixelFormat format, byte[] pixels, bool takeOwnership = false)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "An image must have positive dimensions.");
        }

        ArgumentNullException.ThrowIfNull(pixels);
        int expected = checked(width * height * (int)format);
        if (pixels.Length != expected)
        {
            throw new ArgumentException($"Expected {expected} bytes, received {pixels.Length}.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Format = format;
        Pixels = takeOwnership ? pixels : pixels.ToArray();
    }

    public int Width { get; }

    public int Height { get; }

    public PixelFormat Format { get; }

    public byte[] Pixels { get; }

    public int Channels => (int)Format;

    public ImageFrame Clone() => new(Width, Height, Format, Pixels);

    public ImageFrame Crop(ScreenRegion region)
    {
        if (!region.FitsWithin(Width, Height))
        {
            throw new ArgumentOutOfRangeException(nameof(region), "The crop falls outside the image.");
        }

        int channels = Channels;
        int rowBytes = checked(region.Width * channels);
        byte[] cropped = new byte[checked(rowBytes * region.Height)];
        for (int row = 0; row < region.Height; row++)
        {
            int sourceOffset = checked(((region.Y + row) * Width + region.X) * channels);
            Buffer.BlockCopy(Pixels, sourceOffset, cropped, row * rowBytes, rowBytes);
        }

        return new ImageFrame(region.Width, region.Height, Format, cropped, takeOwnership: true);
    }
}
