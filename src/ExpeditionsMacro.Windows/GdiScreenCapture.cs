using System.ComponentModel;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal static class GdiScreenCapture
{
    public static ImageFrame Capture(ScreenRegion region)
    {
        nint screen = NativeMethods.GetDC(nint.Zero);
        if (screen == nint.Zero) throw new Win32Exception("Windows could not access the desktop for capture.");

        nint memory = nint.Zero;
        nint bitmap = nint.Zero;
        nint previous = nint.Zero;
        try
        {
            memory = NativeMethods.CreateCompatibleDC(screen);
            bitmap = NativeMethods.CreateCompatibleBitmap(screen, region.Width, region.Height);
            if (memory == nint.Zero || bitmap == nint.Zero) throw new Win32Exception("Windows could not allocate a screenshot buffer.");

            previous = NativeMethods.SelectObject(memory, bitmap);
            if (!NativeMethods.BitBlt(memory, 0, 0, region.Width, region.Height, screen, region.X, region.Y, NativeMethods.Srccopy | NativeMethods.CaptureBlt))
            {
                throw new Win32Exception("Windows could not capture the selected screen region.");
            }

            NativeMethods.BitmapInfo info = new()
            {
                Header = new NativeMethods.BitmapInfoHeader
                {
                    Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                    Width = region.Width,
                    Height = -region.Height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0,
                    SizeImage = (uint)checked(region.Width * region.Height * 4),
                },
            };
            byte[] bgra = new byte[checked(region.Width * region.Height * 4)];
            if (NativeMethods.GetDIBits(memory, bitmap, 0, (uint)region.Height, bgra, ref info, NativeMethods.DibRgbColors) == 0)
            {
                throw new Win32Exception("Windows could not read the screenshot pixels.");
            }

            byte[] rgb = new byte[checked(region.Width * region.Height * 3)];
            for (int source = 0, target = 0; source < bgra.Length; source += 4, target += 3)
            {
                rgb[target] = bgra[source + 2];
                rgb[target + 1] = bgra[source + 1];
                rgb[target + 2] = bgra[source];
            }

            return new ImageFrame(region.Width, region.Height, PixelFormat.Rgb24, rgb, takeOwnership: true);
        }
        finally
        {
            if (previous != nint.Zero && memory != nint.Zero) NativeMethods.SelectObject(memory, previous);
            if (bitmap != nint.Zero) NativeMethods.DeleteObject(bitmap);
            if (memory != nint.Zero) NativeMethods.DeleteDC(memory);
            NativeMethods.ReleaseDC(nint.Zero, screen);
        }
    }
}
