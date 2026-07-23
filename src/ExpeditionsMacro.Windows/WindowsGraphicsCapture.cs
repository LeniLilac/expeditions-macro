using System.ComponentModel;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics;
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
                _active = null;
                throw;
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
        ScreenRegion crop) =>
        CaptureSurfaceConverter.ConvertScRgbRgba16ToRgb(rgba16, surfaceWidth, surfaceHeight, crop);

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

    private static (ID3D11Device Device, ID3D11DeviceContext Context) CreateCaptureDevice()
    {
        FeatureLevel[] featureLevels =
        [
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0,
        ];
        try
        {
            var hardwareResult = D3D11.D3D11CreateDevice(
                adapter: null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                featureLevels,
                out ID3D11Device hardwareDevice,
                out ID3D11DeviceContext hardwareContext);
            hardwareResult.CheckError();
            return (hardwareDevice, hardwareContext);
        }
        catch
        {
            // Integrated graphics is a hardware device and normally takes the path
            // above. WARP keeps capture available on CPU-only VMs and remote systems
            // that expose Windows Graphics Capture without a usable D3D11 adapter.
            var warpResult = D3D11.D3D11CreateDevice(
                adapter: null,
                DriverType.Warp,
                DeviceCreationFlags.BgraSupport,
                featureLevels,
                out ID3D11Device warpDevice,
                out ID3D11DeviceContext warpContext);
            warpResult.CheckError();
            return (warpDevice, warpContext);
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
        private const int SurfaceRecreateAttempts = 4;
        private readonly object _lifecycleGate = new();
        private readonly CaptureFrameArrivalGate _frameArrival = new();
        private readonly nint _window;
        private readonly ClientBounds _client;
        private readonly WindowBounds _windowBounds;
        private readonly WindowBounds _extendedFrameBounds;
        private ScreenRegion _clientCrop;
        private int _surfaceWidth;
        private int _surfaceHeight;
        private readonly ID3D11Device _device;
        private readonly ID3D11DeviceContext _context;
        private ID3D11Texture2D _latestTexture;
        private readonly IDirect3DDevice _winRtDevice;
        private readonly GraphicsCaptureItem _item;
        private readonly Direct3D11CaptureFramePool _framePool;
        private readonly GraphicsCaptureSession _captureSession;
        private long _lastConsumedGeneration;
        private int _processing;
        private bool _hasCapturedFrame;
        private bool _disposed;

        private CaptureSession(
            nint window,
            ClientBounds client,
            WindowBounds windowBounds,
            WindowBounds extendedFrameBounds,
            ID3D11Device device,
            ID3D11DeviceContext context,
            ID3D11Texture2D latestTexture,
            IDirect3DDevice winRtDevice,
            GraphicsCaptureItem item,
            ScreenRegion clientCrop,
            int surfaceWidth,
            int surfaceHeight,
            Direct3D11CaptureFramePool framePool,
            GraphicsCaptureSession captureSession)
        {
            _window = window;
            _client = client;
            _windowBounds = windowBounds;
            _extendedFrameBounds = extendedFrameBounds;
            _device = device;
            _context = context;
            _latestTexture = latestTexture;
            _winRtDevice = winRtDevice;
            _item = item;
            _clientCrop = clientCrop;
            _surfaceWidth = surfaceWidth;
            _surfaceHeight = surfaceHeight;
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

            (ID3D11Device device, ID3D11DeviceContext context) = CreateCaptureDevice();
            IDirect3DDevice winRtDevice = CreateWinRtDevice(device);
            GraphicsCaptureItem item = CreateItem(window);
            var surfaceSize = item.Size;
            int surfaceWidth = surfaceSize.Width;
            int surfaceHeight = surfaceSize.Height;
            ScreenRegion clientCrop = ResolveClientCrop(surfaceWidth, surfaceHeight, client, windowBounds, extendedFrameBounds);
            ID3D11Texture2D latestTexture = CaptureTextureFactory.Create(device, surfaceWidth, surfaceHeight);
            Direct3D11CaptureFramePool framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                winRtDevice,
                DirectXPixelFormat.R16G16B16A16Float,
                2,
                surfaceSize);
            GraphicsCaptureSession session = framePool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;
            return new CaptureSession(
                window,
                client,
                windowBounds,
                extendedFrameBounds,
                device,
                context,
                latestTexture,
                winRtDevice,
                item,
                clientCrop,
                surfaceWidth,
                surfaceHeight,
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
            long targetGeneration = _lastConsumedGeneration + 1;
            int timeout = _hasCapturedFrame ? FreshFrameTimeoutMilliseconds : InitialFrameTimeoutMilliseconds;
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            int surfaceRecreates = 0;
            bool stabilizingSurface = false;
            while (DateTime.UtcNow < deadline)
            {
                int remaining = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                if (!_frameArrival.WaitForGeneration(targetGeneration, remaining)) break;
                long availableGeneration = _frameArrival.Generation;
                Direct3D11CaptureFrame? frame = CaptureFrameQueue.TakeLatest(_framePool.TryGetNextFrame);
                if (frame is null)
                {
                    _lastConsumedGeneration = availableGeneration;
                    targetGeneration = availableGeneration + 1;
                    continue;
                }
                _lastConsumedGeneration = availableGeneration;

                int changedWidth = 0;
                int changedHeight = 0;
                using (frame)
                {
                    if (frame.ContentSize.Width != _surfaceWidth || frame.ContentSize.Height != _surfaceHeight)
                    {
                        changedWidth = frame.ContentSize.Width;
                        changedHeight = frame.ContentSize.Height;
                    }
                    else
                    {
                        using ID3D11Texture2D source = GetTexture(frame.Surface);
                        Texture2DDescription sourceDescription = source.Description;
                        if (sourceDescription.Format != Format.R16G16B16A16_Float ||
                            sourceDescription.Width != frame.ContentSize.Width ||
                            sourceDescription.Height != frame.ContentSize.Height)
                        {
                            throw new InvalidOperationException(
                                $"Windows returned an unexpected capture texture ({sourceDescription.Format}, {sourceDescription.Width} by {sourceDescription.Height}).");
                        }

                        _context.CopyResource(_latestTexture, source);
                        _hasCapturedFrame = true;
                    }
                }

                if (changedWidth > 0 && changedHeight > 0)
                {
                    surfaceRecreates++;
                    if (surfaceRecreates > SurfaceRecreateAttempts)
                    {
                        throw new CaptureSurfaceChangedException(
                            _surfaceWidth,
                            _surfaceHeight,
                            changedWidth,
                            changedHeight);
                    }
                    RecreateSurface(changedWidth, changedHeight);
                    _lastConsumedGeneration = _frameArrival.Generation;
                    targetGeneration = _lastConsumedGeneration + 1;
                    if (!stabilizingSurface)
                    {
                        deadline = DateTime.UtcNow.AddMilliseconds(InitialFrameTimeoutMilliseconds);
                        stabilizingSurface = true;
                    }
                    continue;
                }

                break;
            }

            if (!_hasCapturedFrame) throw new TimeoutException("Windows did not provide a Roblox window frame within three seconds.");
            byte[] surfacePixels = ReadTexturePixels(_latestTexture, _surfaceWidth, _surfaceHeight);
            return ConvertScRgbRgba16ToRgb(
                surfacePixels,
                _surfaceWidth,
                _surfaceHeight,
                _clientCrop);
        }

        private void RecreateSurface(int surfaceWidth, int surfaceHeight)
        {
            ScreenRegion clientCrop;
            try
            {
                clientCrop = ResolveClientCrop(
                    surfaceWidth,
                    surfaceHeight,
                    _client,
                    _windowBounds,
                    _extendedFrameBounds);
            }
            catch (InvalidOperationException error)
            {
                throw new CaptureSurfaceChangedException(
                    _surfaceWidth,
                    _surfaceHeight,
                    surfaceWidth,
                    surfaceHeight,
                    error);
            }

            ID3D11Texture2D latestTexture = CaptureTextureFactory.Create(_device, surfaceWidth, surfaceHeight);
            try
            {
                _framePool.Recreate(
                    _winRtDevice,
                    DirectXPixelFormat.R16G16B16A16Float,
                    2,
                    new SizeInt32(surfaceWidth, surfaceHeight));
            }
            catch
            {
                latestTexture.Dispose();
                throw;
            }

            ID3D11Texture2D previousTexture = _latestTexture;
            _latestTexture = latestTexture;
            _clientCrop = clientCrop;
            _surfaceWidth = surfaceWidth;
            _surfaceHeight = surfaceHeight;
            _hasCapturedFrame = false;
            previousTexture.Dispose();
        }

        public void Dispose()
        {
            lock (_lifecycleGate)
            {
                if (_disposed) return;
                _disposed = true;
                _framePool.FrameArrived -= FrameArrived;
            }
            _captureSession.Dispose();
            _framePool.Dispose();
            _frameArrival.Wake();
            bool drained = SpinWait.SpinUntil(() => Volatile.Read(ref _processing) == 0, TimeSpan.FromSeconds(2));
            if (!drained)
            {
                // An already-dispatched frame notification is still leaving the callback.
                // Finish cleanup after it releases the arrival gate.
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
            _frameArrival.Dispose();
            (_winRtDevice as IDisposable)?.Dispose();
            _latestTexture.Dispose();
            _context.Dispose();
            _device.Dispose();
        }

        private void FrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            lock (_lifecycleGate)
            {
                if (_disposed) return;
                Interlocked.Increment(ref _processing);
            }
            try
            {
                // CreateFreeThreaded raises this event on the frame pool's internal worker.
                // Some Windows 10 builds reject WinRT surface access there with
                // RPC_E_WRONG_THREAD. Keep the callback notification-only and consume the
                // frame on the serialized Capture call instead.
                _frameArrival.Notify();
            }
            finally
            {
                Interlocked.Decrement(ref _processing);
            }
        }

        private byte[] ReadTexturePixels(ID3D11Texture2D source, int width, int height)
        {
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
}
