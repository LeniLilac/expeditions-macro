using System.ComponentModel;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x5941;
    private readonly object _gate = new();
    private Thread? _thread;
    private CancellationTokenSource? _cancellation;
    private uint _threadId;
    private NativeMethods.HookProc? _fallbackCallback;
    private nint _fallbackHook;
    private bool _f6Down;

    public event EventHandler? F6Pressed;

    public bool IsRegistered { get; private set; }

    public void Start()
    {
        lock (_gate)
        {
            if (_thread is not null) return;
            _cancellation = new CancellationTokenSource();
            ManualResetEventSlim started = new(false);
            Exception? error = null;
            _thread = new Thread(() =>
            {
                try
                {
                    Run(started, value => error = value);
                }
                catch (Exception unexpected)
                {
                    error = unexpected;
                    IsRegistered = false;
                    started.Set();
                }
            })
            {
                IsBackground = true,
                Name = "ExpeditionsMacro.F6",
            };
            _thread.Start();
            if (!started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("F6 listener did not start.");
            if (error is not null)
            {
                _thread = null;
                _cancellation.Dispose();
                _cancellation = null;
                throw error;
            }
        }
    }

    public void Stop()
    {
        Thread? thread;
        lock (_gate)
        {
            thread = _thread;
            if (thread is null) return;
            _cancellation?.Cancel();
            if (_threadId != 0) NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmQuit, 0, nint.Zero);
        }

        thread.Join(TimeSpan.FromSeconds(2));
        lock (_gate)
        {
            _thread = null;
            _cancellation?.Dispose();
            _cancellation = null;
            _threadId = 0;
            IsRegistered = false;
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void Run(ManualResetEventSlim started, Action<Exception> fail)
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        bool ownsHotkey = NativeMethods.RegisterHotKey(nint.Zero, HotkeyId, 0, NativeMethods.VkF6);
        if (!ownsHotkey)
        {
            _fallbackCallback = (code, wParam, lParam) =>
            {
                if (code == NativeMethods.HcAction)
                {
                    NativeMethods.KeyboardHookData data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
                    if (data.VirtualKey == NativeMethods.VkF6)
                    {
                        if ((uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
                        {
                            if (!_f6Down)
                            {
                                _f6Down = true;
                                F6Pressed?.Invoke(this, EventArgs.Empty);
                            }
                        }
                        else if ((uint)wParam is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
                        {
                            _f6Down = false;
                        }
                    }
                }

                return NativeMethods.CallNextHookEx(_fallbackHook, code, wParam, lParam);
            };
            _fallbackHook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WhKeyboardLl,
                _fallbackCallback,
                NativeMethods.GetModuleHandle(null),
                0);
            if (_fallbackHook == nint.Zero)
            {
                fail(new Win32Exception("Windows could not start the F6 listener."));
                started.Set();
                return;
            }
        }

        IsRegistered = true;
        started.Set();
        try
        {
            while (_cancellation?.IsCancellationRequested == false)
            {
                while (NativeMethods.PeekMessage(out NativeMethods.Message message, nint.Zero, 0, 0, NativeMethods.PmRemove))
                {
                    if (message.Value == NativeMethods.WmQuit) return;
                    if (ownsHotkey && message.Value == NativeMethods.WmHotkey && (int)message.WParam == HotkeyId) F6Pressed?.Invoke(this, EventArgs.Empty);
                }
                Thread.Sleep(15);
            }
        }
        finally
        {
            if (ownsHotkey) NativeMethods.UnregisterHotKey(nint.Zero, HotkeyId);
            if (_fallbackHook != nint.Zero) NativeMethods.UnhookWindowsHookEx(_fallbackHook);
            _fallbackHook = nint.Zero;
            _fallbackCallback = null;
            _f6Down = false;
            IsRegistered = false;
        }
    }
}
