using System.Runtime.InteropServices;

namespace ExpeditionsMacro.Windows.Interop;

internal static partial class NativeMethods
{
    internal const int SwpNoZOrder = 0x0004;
    internal const int SwpNoActivate = 0x0010;
    internal const int SwpFrameChanged = 0x0020;
    internal const uint MonitorDefaultToNearest = 0x00000002;
    internal const uint WmHotkey = 0x0312;
    internal const uint ModNoRepeat = 0x4000;
    internal const uint WmQuit = 0x0012;
    internal const uint PmRemove = 0x0001;
    internal const int WhKeyboardLl = 13;
    internal const int WhMouseLl = 14;
    internal const int HcAction = 0;
    internal const uint WmKeyDown = 0x0100;
    internal const uint WmKeyUp = 0x0101;
    internal const uint WmSysKeyDown = 0x0104;
    internal const uint WmSysKeyUp = 0x0105;
    internal const uint WmLButtonDown = 0x0201;
    internal const int VkLeftControl = 0xA2;
    internal const uint KeyeventfKeyUp = 0x0002;
    internal const uint MouseeventfMove = 0x0001;
    internal const uint MouseeventfLeftDown = 0x0002;
    internal const uint MouseeventfLeftUp = 0x0004;
    internal const uint MouseeventfRightDown = 0x0008;
    internal const uint MouseeventfRightUp = 0x0010;
    internal const uint MouseeventfWheel = 0x0800;
    internal const int WheelDelta = 120;
    internal const uint InputMouse = 0;
    internal const uint Srccopy = 0x00CC0020;
    internal const uint CaptureBlt = 0x40000000;
    internal const uint DibRgbColors = 0;
    internal const uint CryptprotectUiForbidden = 0x1;

    internal delegate bool EnumWindowsProc(nint window, nint parameter);
    internal delegate nint HookProc(int code, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Window;
        internal uint Value;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal Point Position;
        internal uint Private;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        internal uint Size;
        internal Rect Monitor;
        internal Rect Work;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        internal int Dx;
        internal int Dy;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] internal MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        internal uint Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint SizeImage;
        internal int XPelsPerMeter;
        internal int YPelsPerMeter;
        internal uint ClrUsed;
        internal uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        internal BitmapInfoHeader Header;
        internal uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData
    {
        internal uint VirtualKey;
        internal uint ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseHookData
    {
        internal Point Position;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DataBlob
    {
        internal int Length;
        internal nint Data;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    internal static partial int GetWindowTextLength(nint window);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetWindowText(nint window, [Out] char[] text, int maximumCount);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(nint window, out Rect rectangle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ClientToScreen(nint window, ref Point point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint window, out Rect rectangle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsZoomed(nint window);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromWindow(nint window, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BringWindowToTop(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    internal static partial void mouse_event(uint flags, int dx, int dy, uint data, nuint extraInfo);

    [LibraryImport("user32.dll")]
    internal static partial void keybd_event(byte virtualKey, byte scanCode, uint flags, nuint extraInfo);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint window, int id);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static partial nint SetWindowsHookEx(int hookId, HookProc callback, nint module, uint threadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    internal static partial nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    internal static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(nint window, nint deviceContext);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateCompatibleDC(nint deviceContext);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateCompatibleBitmap(nint deviceContext, int width, int height);

    [LibraryImport("gdi32.dll")]
    internal static partial nint SelectObject(nint deviceContext, nint value);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(nint destination, int xDestination, int yDestination, int width, int height, nint source, int xSource, int ySource, uint operation);

    [LibraryImport("gdi32.dll")]
    internal static partial int GetDIBits(nint deviceContext, nint bitmap, uint start, uint lines, [Out] byte[] bits, ref BitmapInfo info, uint usage);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint value);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(nint deviceContext);

    [LibraryImport("crypt32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CryptProtectData(ref DataBlob input, string description, ref DataBlob entropy, nint reserved, nint prompt, uint flags, out DataBlob output);

    [LibraryImport("crypt32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CryptUnprotectData(ref DataBlob input, nint description, ref DataBlob entropy, nint reserved, nint prompt, uint flags, out DataBlob output);

    [LibraryImport("kernel32.dll")]
    internal static partial nint LocalFree(nint memory);
}
