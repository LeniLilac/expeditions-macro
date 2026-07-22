using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Imaging;
using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Infrastructure;

public static class ImageCodec
{
    public static Mat ToMat(ImageFrame image)
    {
        OpenCvRuntime.Initialize();
        MatType type = image.Format == PixelFormat.Gray8 ? MatType.CV_8UC1 : MatType.CV_8UC3;
        Mat mat = new(image.Height, image.Width, type);
        Marshal.Copy(image.Pixels, 0, mat.Data, image.Pixels.Length);
        return mat;
    }

    public static ImageFrame FromMat(Mat source, PixelFormat? desiredFormat = null)
    {
        OpenCvRuntime.Initialize();
        using Mat converted = ConvertFormat(source, desiredFormat);
        using Mat continuous = converted.IsContinuous() ? converted.Clone() : converted.Clone();
        PixelFormat format = continuous.Channels() == 1 ? PixelFormat.Gray8 : PixelFormat.Rgb24;
        byte[] pixels = new byte[checked(continuous.Rows * continuous.Cols * continuous.Channels())];
        Marshal.Copy(continuous.Data, pixels, 0, pixels.Length);
        return new ImageFrame(continuous.Cols, continuous.Rows, format, pixels, takeOwnership: true);
    }

    public static ImageFrame Load(string path, PixelFormat format = PixelFormat.Rgb24)
    {
        OpenCvRuntime.Initialize();
        ImreadModes mode = format == PixelFormat.Gray8 ? ImreadModes.Grayscale : ImreadModes.Color;
        using Mat loaded = Cv2.ImRead(path, mode);
        if (loaded.Empty()) throw new InvalidDataException($"Could not read image '{path}'.");
        if (format == PixelFormat.Gray8) return FromMat(loaded, PixelFormat.Gray8);
        using Mat rgb = new();
        Cv2.CvtColor(loaded, rgb, ColorConversionCodes.BGR2RGB);
        return FromMat(rgb, PixelFormat.Rgb24);
    }

    public static void SavePng(string path, ImageFrame image, int compression = 7)
    {
        if (compression is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(compression));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Image has no parent directory."));
        using Mat source = ToMat(image);
        using Mat encoded = new();
        if (image.Format == PixelFormat.Rgb24)
        {
            Cv2.CvtColor(source, encoded, ColorConversionCodes.RGB2BGR);
        }
        else
        {
            source.CopyTo(encoded);
        }
        if (!Cv2.ImWrite(path, encoded, [new ImageEncodingParam(ImwriteFlags.PngCompression, compression)])) throw new IOException($"Could not write image '{path}'.");
    }

    public static byte[] EncodePng(ImageFrame image)
    {
        using Mat source = ToMat(image);
        using Mat encoded = new();
        if (image.Format == PixelFormat.Rgb24) Cv2.CvtColor(source, encoded, ColorConversionCodes.RGB2BGR);
        else source.CopyTo(encoded);
        Cv2.ImEncode(".png", encoded, out byte[] bytes, [new ImageEncodingParam(ImwriteFlags.PngCompression, 7)]);
        return bytes;
    }

    private static Mat ConvertFormat(Mat source, PixelFormat? desiredFormat)
    {
        if (desiredFormat is null || (desiredFormat == PixelFormat.Gray8 && source.Channels() == 1) || (desiredFormat == PixelFormat.Rgb24 && source.Channels() == 3)) return source.Clone();
        Mat converted = new();
        if (desiredFormat == PixelFormat.Gray8 && source.Channels() == 3) Cv2.CvtColor(source, converted, ColorConversionCodes.RGB2GRAY);
        else if (desiredFormat == PixelFormat.Rgb24 && source.Channels() == 1) Cv2.CvtColor(source, converted, ColorConversionCodes.GRAY2RGB);
        else throw new InvalidDataException("Unsupported image format conversion.");
        return converted;
    }
}
