using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace ExpeditionsMacro.Windows;

internal sealed partial class WindowsGraphicsCapture
{
    private sealed partial class CaptureSession
    {
        public ImageFrame Capture()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // The free-threaded pool can remain full between serialized Capture calls.
            // Those queued surfaces can predate camera input even though the caller has
            // already waited for Roblox to settle. Free the pool, then require a frame
            // whose arrival notification occurs after this capture's freshness barrier.
            CaptureFrameQueue.DiscardAll(_framePool.TryGetNextFrame);
            long targetGeneration = _frameArrival.Generation + 1;
            int timeout = _hasCapturedFrame ? FreshFrameTimeoutMilliseconds : InitialFrameTimeoutMilliseconds;
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            int surfaceRecreates = 0;
            bool stabilizingSurface = false;
            bool capturedFreshFrame = false;
            while (DateTime.UtcNow < deadline)
            {
                int remaining = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                if (!_frameArrival.WaitForGeneration(targetGeneration, remaining)) break;
                long availableGeneration = _frameArrival.Generation;
                Direct3D11CaptureFrame? frame = CaptureFrameQueue.TakeLatest(_framePool.TryGetNextFrame);
                if (frame is null)
                {
                    targetGeneration = availableGeneration + 1;
                    continue;
                }

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
                        capturedFreshFrame = true;
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
                    CaptureFrameQueue.DiscardAll(_framePool.TryGetNextFrame);
                    targetGeneration = _frameArrival.Generation + 1;
                    if (!stabilizingSurface)
                    {
                        deadline = DateTime.UtcNow.AddMilliseconds(InitialFrameTimeoutMilliseconds);
                        stabilizingSurface = true;
                    }
                    continue;
                }

                break;
            }

            if (!capturedFreshFrame)
            {
                throw new TimeoutException(
                    $"Windows did not provide a fresh Roblox window frame within {timeout} milliseconds.");
            }
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
    }
}
