using System.ComponentModel;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace ExpeditionsMacro.Windows;

internal sealed class WindowsGraphicsCapture : IDisposable
{
    private static readonly Guid GraphicsCaptureItemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid Direct3D11Texture2DId = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
    private readonly object _gate = new();
    private CaptureSession? _active;
    private bool _disposed;

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(nint window, in Guid iid);

        nint CreateForMonitor(nint monitor, in Guid iid);
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        nint GetInterface(in Guid iid);
    }

    [DllImport("d3d11.dll", PreserveSig = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(
        nint dxgiDevice,
        out nint graphicsDevice);

    public ImageFrame CaptureClient(
        nint window,
        ClientBounds client,
        WindowBounds windowBounds,
        WindowBounds extendedFrameBounds)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active is null || !_active.Matches(window, client, windowBounds, extendedFrameBounds))
            {
                _active?.Dispose();
                _active = CaptureSession.Create(window, client, windowBounds, extendedFrameBounds);
            }

            try
            {
                return _active.Capture();
            }
            catch (CaptureSurfaceChangedException)
            {
                _active.Dispose();
                _active = CaptureSession.Create(window, client, windowBounds, extendedFrameBounds);
                return _active.Capture();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _active?.Dispose();
            _active = null;
        }
    }

    internal static ScreenRegion ResolveClientCrop(
        int surfaceWidth,
        int surfaceHeight,
        ClientBounds client,
        WindowBounds windowBounds,
        WindowBounds extendedFrameBounds)
    {
        if (surfaceWidth == client.Width && surfaceHeight == client.Height)
        {
            return new ScreenRegion(0, 0, client.Width, client.Height);
        }

        foreach (WindowBounds candidate in new[] { extendedFrameBounds, windowBounds })
        {
            if (Math.Abs(candidate.Width - surfaceWidth) > 2 || Math.Abs(candidate.Height - surfaceHeight) > 2) continue;
            int x = client.X - candidate.X;
            int y = client.Y - candidate.Y;
            if (x >= 0 && y >= 0 && x + client.Width <= surfaceWidth && y + client.Height <= surfaceHeight)
            {
                return new ScreenRegion(x, y, client.Width, client.Height);
            }
        }

        throw new InvalidOperationException(
            $"Windows captured Roblox at {surfaceWidth} by {surfaceHeight}, but its {client.Width} by {client.Height} client area could not be mapped into that surface.");
    }

    internal static ImageFrame ConvertScRgbRgba16ToRgb(
        byte[] rgba16,
        int surfaceWidth,
        int surfaceHeight,
        ScreenRegion crop)
    {
        if (rgba16.Length != checked(surfaceWidth * surfaceHeight * 8))
        {
            throw new ArgumentException("The FP16 RGBA buffer has an unexpected length.", nameof(rgba16));
        }
        if (crop.X < 0 || crop.Y < 0 || crop.Right > surfaceWidth || crop.Bottom > surfaceHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(crop), "The client crop must fit inside the captured surface.");
        }

        byte[] rgb = new byte[checked(crop.Width * crop.Height * 3)];
        int target = 0;
        for (int y = crop.Y; y < crop.Bottom; y++)
        {
            int source = checked((y * surfaceWidth + crop.X) * 8);
            for (int x = 0; x < crop.Width; x++, source += 8, target += 3)
            {
                float red = ReadFiniteHalf(rgba16, source);
                float green = ReadFiniteHalf(rgba16, source + 2);
                float blue = ReadFiniteHalf(rgba16, source + 4);
                ToneMapScRgb(ref red, ref green, ref blue);
                rgb[target] = LinearToSrgbByte(red);
                rgb[target + 1] = LinearToSrgbByte(green);
                rgb[target + 2] = LinearToSrgbByte(blue);
            }
        }
        return new ImageFrame(crop.Width, crop.Height, PixelFormat.Rgb24, rgb, takeOwnership: true);
    }

    private static float ReadFiniteHalf(byte[] source, int offset)
    {
        ushort bits = (ushort)(source[offset] | source[offset + 1] << 8);
        float value = (float)BitConverter.UInt16BitsToHalf(bits);
        return float.IsFinite(value) ? Math.Max(0f, value) : 0f;
    }

    private static void ToneMapScRgb(ref float red, ref float green, ref float blue)
    {
        // Windows Graphics Capture exposes HDR/WCG window pixels as linear scRGB,
        // where 1.0 is SDR reference white (80 nits) and HDR highlights can exceed 1.0.
        // Keep ordinary SDR pixels on the standard transfer curve and only
        // compress values that enter the highlight shoulder. This avoids changing the
        // detector input on SDR systems while preventing Auto HDR highlights from clipping.
        const float shoulderStart = 0.8f;
        const float scRgbAt1000Nits = 12.5f;
        const float shoulderScale = 2f;

        float luminance = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
        if (luminance > shoulderStart)
        {
            float capped = Math.Min(luminance, scRgbAt1000Nits);
            float numerator = 1f - MathF.Exp(-(capped - shoulderStart) / shoulderScale);
            float denominator = 1f - MathF.Exp(-(scRgbAt1000Nits - shoulderStart) / shoulderScale);
            float mapped = shoulderStart + (1f - shoulderStart) * numerator / denominator;
            float scale = mapped / luminance;
            red *= scale;
            green *= scale;
            blue *= scale;
        }

        float maximum = Math.Max(red, Math.Max(green, blue));
        if (maximum > 1f)
        {
            float scale = 1f / maximum;
            red *= scale;
            green *= scale;
            blue *= scale;
        }
    }

    private static byte LinearToSrgbByte(float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        float encoded = clamped <= 0.0031308f
            ? 12.92f * clamped
            : 1.055f * MathF.Pow(clamped, 1f / 2.4f) - 0.055f;
        return (byte)Math.Clamp((int)MathF.Round(encoded * 255f), 0, 255);
    }

    private static GraphicsCaptureItem CreateItem(nint window)
    {
        IGraphicsCaptureItemInterop interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        nint itemPointer = interop.CreateForWindow(window, GraphicsCaptureItemId);
        if (itemPointer == nint.Zero) throw new Win32Exception("Windows could not create a Roblox window capture target.");
        try
        {
            return GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    private static IDirect3DDevice CreateWinRtDevice(ID3D11Device device)
    {
        using IDXGIDevice dxgiDevice = device.QueryInterface<IDXGIDevice>();
        int result = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out nint graphicsDevicePointer);
        Marshal.ThrowExceptionForHR(result);
        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevicePointer);
        }
        finally
        {
            Marshal.Release(graphicsDevicePointer);
        }
    }

    private static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        IDirect3DDxgiInterfaceAccess access = surface.As<IDirect3DDxgiInterfaceAccess>();
        nint texturePointer = access.GetInterface(Direct3D11Texture2DId);
        if (texturePointer == nint.Zero) throw new InvalidOperationException("Windows returned a capture frame without a Direct3D 11 texture.");
        return new ID3D11Texture2D(texturePointer);
    }

    private sealed class CaptureSession : IDisposable
    {
        private const int InitialFrameTimeoutMilliseconds = 3000;
        private const int FreshFrameTimeoutMilliseconds = 350;
        private readonly object _frameGate = new();
        private readonly AutoResetEvent _frameReady = new(false);
        private readonly nint _window;
        private readonly ClientBounds _client;
        private readonly WindowBounds _windowBounds;
        private readonly WindowBounds _extendedFrameBounds;
        private readonly ScreenRegion _clientCrop;
        private readonly ID3D11Device _device;
        private readonly ID3D11DeviceContext _context;
        private readonly IDirect3DDevice _winRtDevice;
        private readonly GraphicsCaptureItem _item;
        private readonly Direct3D11CaptureFramePool _framePool;
        private readonly GraphicsCaptureSession _captureSession;
        private ImageFrame? _latest;
        private Exception? _captureError;
        private long _requestedGeneration = 1;
        private long _completedGeneration;
        private int _processing;
        private bool _disposed;

        private CaptureSession(
            nint window,
            ClientBounds client,
            WindowBounds windowBounds,
            WindowBounds extendedFrameBounds,
            ID3D11Device device,
            ID3D11DeviceContext context,
            IDirect3DDevice winRtDevice,
            GraphicsCaptureItem item,
            ScreenRegion clientCrop,
            Direct3D11CaptureFramePool framePool,
            GraphicsCaptureSession captureSession)
        {
            _window = window;
            _client = client;
            _windowBounds = windowBounds;
            _extendedFrameBounds = extendedFrameBounds;
            _device = device;
            _context = context;
            _winRtDevice = winRtDevice;
            _item = item;
            _clientCrop = clientCrop;
            _framePool = framePool;
            _captureSession = captureSession;
            _framePool.FrameArrived += FrameArrived;
            _captureSession.StartCapture();
        }

        public static CaptureSession Create(
            nint window,
            ClientBounds client,
            WindowBounds windowBounds,
            WindowBounds extendedFrameBounds)
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                throw new PlatformNotSupportedException("Windows Graphics Capture is unavailable. Expeditions Macro requires Windows 10 version 1903 or later for reliable Roblox capture.");
            }

            var deviceResult = D3D11.D3D11CreateDevice(
                adapter: null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0],
                out ID3D11Device device,
                out ID3D11DeviceContext context);
            deviceResult.CheckError();
            IDirect3DDevice winRtDevice = CreateWinRtDevice(device);
            GraphicsCaptureItem item = CreateItem(window);
            ScreenRegion clientCrop = ResolveClientCrop(item.Size.Width, item.Size.Height, client, windowBounds, extendedFrameBounds);
            Direct3D11CaptureFramePool framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                winRtDevice,
                DirectXPixelFormat.R16G16B16A16Float,
                2,
                item.Size);
            GraphicsCaptureSession session = framePool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;
            return new CaptureSession(
                window,
                client,
                windowBounds,
                extendedFrameBounds,
                device,
                context,
                winRtDevice,
                item,
                clientCrop,
                framePool,
                session);
        }

        public bool Matches(
            nint window,
            ClientBounds client,
            WindowBounds windowBounds,
            WindowBounds extendedFrameBounds) =>
            !_disposed &&
            _window == window &&
            client.Width == _client.Width &&
            client.Height == _client.Height &&
            client.X - extendedFrameBounds.X == _client.X - _extendedFrameBounds.X &&
            client.Y - extendedFrameBounds.Y == _client.Y - _extendedFrameBounds.Y &&
            windowBounds.Width == _windowBounds.Width &&
            windowBounds.Height == _windowBounds.Height &&
            extendedFrameBounds.Width == _extendedFrameBounds.Width &&
            extendedFrameBounds.Height == _extendedFrameBounds.Height;

        public ImageFrame Capture()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            long requested = Interlocked.Increment(ref _requestedGeneration);
            int timeout = Volatile.Read(ref _completedGeneration) == 0
                ? InitialFrameTimeoutMilliseconds
                : FreshFrameTimeoutMilliseconds;
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            while (Volatile.Read(ref _completedGeneration) < requested && DateTime.UtcNow < deadline)
            {
                _frameReady.WaitOne(Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds));
            }

            lock (_frameGate)
            {
                if (_captureError is CaptureSurfaceChangedException surfaceChanged) throw surfaceChanged;
                if (_captureError is not null) throw new InvalidOperationException("Windows could not capture the Roblox window.", _captureError);
                if (_latest is null) throw new TimeoutException("Windows did not provide a Roblox window frame within three seconds.");
                return _latest.Clone();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _framePool.FrameArrived -= FrameArrived;
            _captureSession.Dispose();
            _framePool.Dispose();
            _frameReady.Set();
            bool drained = SpinWait.SpinUntil(() => Volatile.Read(ref _processing) == 0, TimeSpan.FromSeconds(2));
            if (!drained)
            {
                // A GPU readback still owns these objects. Finish cleanup after its callback
                // releases the D3D context instead of racing it during app shutdown.
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    SpinWait.SpinUntil(() => Volatile.Read(ref _processing) == 0);
                    DisposeCaptureResources();
                });
                return;
            }

            DisposeCaptureResources();
        }

        private void DisposeCaptureResources()
        {
            _frameReady.Dispose();
            (_winRtDevice as IDisposable)?.Dispose();
            _context.Dispose();
            _device.Dispose();
        }

        private void FrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            if (_disposed || Volatile.Read(ref _completedGeneration) >= Volatile.Read(ref _requestedGeneration)) return;
            if (Interlocked.Exchange(ref _processing, 1) != 0) return;
            try
            {
                using Direct3D11CaptureFrame? frame = sender.TryGetNextFrame();
                if (frame is null) return;
                if (frame.ContentSize.Width != _item.Size.Width || frame.ContentSize.Height != _item.Size.Height)
                {
                    throw new CaptureSurfaceChangedException();
                }

                byte[] surfacePixels = ReadSurfacePixels(frame.Surface, frame.ContentSize.Width, frame.ContentSize.Height);
                ImageFrame captured = ConvertScRgbRgba16ToRgb(
                    surfacePixels,
                    frame.ContentSize.Width,
                    frame.ContentSize.Height,
                    _clientCrop);
                lock (_frameGate)
                {
                    _latest = captured;
                    _captureError = null;
                    Volatile.Write(ref _completedGeneration, Volatile.Read(ref _requestedGeneration));
                }
            }
            catch (Exception error)
            {
                lock (_frameGate)
                {
                    _captureError = error;
                    Volatile.Write(ref _completedGeneration, Volatile.Read(ref _requestedGeneration));
                }
            }
            finally
            {
                Volatile.Write(ref _processing, 0);
                _frameReady.Set();
            }
        }

        private byte[] ReadSurfacePixels(IDirect3DSurface surface, int width, int height)
        {
            using ID3D11Texture2D source = GetTexture(surface);
            Texture2DDescription sourceDescription = source.Description;
            if (sourceDescription.Format != Format.R16G16B16A16_Float ||
                sourceDescription.Width != width ||
                sourceDescription.Height != height)
            {
                throw new InvalidOperationException(
                    $"Windows returned an unexpected capture texture ({sourceDescription.Format}, {sourceDescription.Width} by {sourceDescription.Height}).");
            }

            Texture2DDescription stagingDescription = new(
                sourceDescription.Format,
                sourceDescription.Width,
                sourceDescription.Height,
                sourceDescription.ArraySize,
                sourceDescription.MipLevels,
                BindFlags.None,
                ResourceUsage.Staging,
                CpuAccessFlags.Read,
                sourceDescription.SampleDescription.Count,
                sourceDescription.SampleDescription.Quality,
                ResourceOptionFlags.None);
            using ID3D11Texture2D staging = _device.CreateTexture2D(stagingDescription);
            _context.CopyResource(staging, source);
            MappedSubresource mapped = _context.Map(staging, 0, MapMode.Read);
            try
            {
                int rowBytes = checked(width * 8);
                byte[] pixels = new byte[checked(rowBytes * height)];
                for (int row = 0; row < height; row++)
                {
                    nint sourceRow = IntPtr.Add(mapped.DataPointer, checked((int)(row * mapped.RowPitch)));
                    Marshal.Copy(sourceRow, pixels, row * rowBytes, rowBytes);
                }
                return pixels;
            }
            finally
            {
                _context.Unmap(staging, 0);
            }
        }
    }

    private sealed class CaptureSurfaceChangedException : Exception;
}
