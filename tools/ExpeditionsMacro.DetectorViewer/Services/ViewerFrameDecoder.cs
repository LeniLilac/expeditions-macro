using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExpeditionsMacro.Core.Imaging;
using CorePixelFormat =
    ExpeditionsMacro.Core.Imaging.PixelFormat;

namespace ExpeditionsMacro.DetectorViewer.Services;

public sealed record DecodedViewerFrame(
    ImageFrame Image,
    BitmapSource Bitmap)
{
    public long DecodedBytes =>
        (long)Image.Pixels.Length +
        (long)Bitmap.PixelWidth *
        Bitmap.PixelHeight *
        4;
}

public static class ViewerFrameDecoder
{
    private const int MaximumDimension = 16_384;

    public static DecodedViewerFrame Decode(
        byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            throw new InvalidDataException(
                "The frame image is empty.");
        }
        using MemoryStream stream =
            new(bytes, writable: false);
        BitmapDecoder decoder =
            BitmapDecoder.Create(
                stream,
                BitmapCreateOptions
                    .PreservePixelFormat,
                BitmapCacheOption.OnLoad);
        BitmapFrame source =
            decoder.Frames.FirstOrDefault() ??
            throw new InvalidDataException(
                "The image has no decodable frame.");
        if (source.PixelWidth <= 0 ||
            source.PixelHeight <= 0 ||
            source.PixelWidth > MaximumDimension ||
            source.PixelHeight > MaximumDimension)
        {
            throw new InvalidDataException(
                $"The decoded image dimensions {source.PixelWidth} by {source.PixelHeight} are invalid.");
        }

        FormatConvertedBitmap rgb = new(
            source,
            PixelFormats.Rgb24,
            null,
            0);
        int stride =
            checked(rgb.PixelWidth * 3);
        byte[] pixels =
            new byte[checked(
                stride * rgb.PixelHeight)];
        rgb.CopyPixels(
            pixels,
            stride,
            0);
        ImageFrame image = new(
            rgb.PixelWidth,
            rgb.PixelHeight,
            CorePixelFormat.Rgb24,
            pixels,
            takeOwnership: true);

        FormatConvertedBitmap display = new(
            source,
            PixelFormats.Pbgra32,
            null,
            0);
        display.Freeze();
        return new DecodedViewerFrame(
            image,
            display);
    }
}
