using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.App.Services;

public static class BitmapSourceFactory
{
    public static BitmapSource Create(ImageFrame frame)
    {
        System.Windows.Media.PixelFormat format = frame.Format == ExpeditionsMacro.Core.Imaging.PixelFormat.Gray8 ? PixelFormats.Gray8 : PixelFormats.Rgb24;
        int stride = checked(frame.Width * frame.Channels);
        BitmapSource source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, format, null, frame.Pixels, stride);
        source.Freeze();
        return source;
    }
}
