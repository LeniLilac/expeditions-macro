using System.ComponentModel;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    public const int DefaultVirtualKey = 0x75;
    private const int HotkeyId = 0x5941;
    private readonly object _gate = new();
    private Thread? _thread;
    private CancellationTokenSource? _cancellation;
    private uint _threadId;
    private NativeMethods.HookProc? _fallbackCallback;
    private nint _fallbackHook;
    private bool _hotkeyDown;
    private int _virtualKey = DefaultVirtualKey;

    public event EventHandler? Pressed;

    public event EventHandler? BindingChanged;

    public bool IsRegistered { get; private set; }

    public int VirtualKey
    {
        get
        {
            lock (_gate) return _virtualKey;
        }
    }

    public string DisplayName => GetDisplayName(VirtualKey);

    public static bool IsSupportedVirtualKey(int virtualKey) => virtualKey switch
    {
        >= 0x30 and <= 0x39 => true, // Top-row digits.
        >= 0x41 and <= 0x5A => true, // Letters.
        >= 0x60 and <= 0x6B => true, // Numpad digits, multiply, and add.
        >= 0x6D and <= 0x6F => true, // Numpad subtract, decimal, and divide.
        >= 0x70 and <= 0x7A => true, // F1-F11.
        >= 0x7C and <= 0x87 => true, // F13-F24; F12 stays reserved.
        >= 0xBA and <= 0xC0 => true, // Common OEM punctuation keys.
        >= 0xDB and <= 0xDE => true,
        0xE2 => true, // OEM 102-key punctuation key.
        _ => false,
    };

    public static string GetDisplayName(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x60 and <= 0x69) return $"Num {virtualKey - 0x60}";
        if (virtualKey is >= 0x70 and <= 0x87 && virtualKey != 0x7B) return $"F{virtualKey - 0x6F}";

        return virtualKey switch
        {
            0x6A => "Num *",
            0x6B => "Num +",
            0x6D => "Num -",
            0x6E => "Num .",
            0x6F => "Num /",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            0xE2 => "\\",
            _ => "Unsupported",
        };
    }

    public void Configure(int virtualKey)
    {
        ValidateVirtualKey(virtualKey);
        bool changed;
        lock (_gate)
        {
            if (_thread is not null) throw new InvalidOperationException("Stop the global hotkey listener before configuring it.");
            changed = _virtualKey != virtualKey;
            _virtualKey = virtualKey;
        }
        if (changed) BindingChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Rebind(int virtualKey)
    {
        ValidateVirtualKey(virtualKey);
        int previous;
        bool running;
        lock (_gate)
        {
            previous = _virtualKey;
            running = _thread is not null;
        }
        if (previous == virtualKey) return;
        if (!running)
        {
            Configure(virtualKey);
            return;
        }

        Stop();
        try
        {
            SetVirtualKey(virtualKey);
            Start();
        }
        catch (Exception rebindError)
        {
            try
            {
                Stop();
                SetVirtualKey(previous);
                Start();
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("The new hotkey failed and the previous hotkey could not be restored.", rebindError, rollbackError);
            }
            throw;
        }
        BindingChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_thread is not null) return;
            int virtualKey = _virtualKey;
            string displayName = GetDisplayName(virtualKey);
            _cancellation = new CancellationTokenSource();
            ManualResetEventSlim started = new(false);
            Exception? error = null;
            _thread = new Thread(() =>
            {
                try
                {
                    Run(virtualKey, displayName, started, value => error = value);
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
                Name = $"ExpeditionsMacro.{displayName}",
            };
            _thread.Start();
            if (!started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException($"{displayName} listener did not start.");
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

    private void Run(int virtualKey, string displayName, ManualResetEventSlim started, Action<Exception> fail)
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        bool ownsHotkey = NativeMethods.RegisterHotKey(nint.Zero, HotkeyId, NativeMethods.ModNoRepeat, (uint)virtualKey);
        if (!ownsHotkey)
        {
            _fallbackCallback = (code, wParam, lParam) =>
            {
                if (code == NativeMethods.HcAction)
                {
                    NativeMethods.KeyboardHookData data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
                    if (data.VirtualKey == (uint)virtualKey)
                    {
                        if ((uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
                        {
                            if (!_hotkeyDown)
                            {
                                _hotkeyDown = true;
                                Pressed?.Invoke(this, EventArgs.Empty);
                            }
                        }
                        else if ((uint)wParam is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
                        {
                            _hotkeyDown = false;
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
                fail(new Win32Exception($"Windows could not start the {displayName} listener."));
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
                    if (ownsHotkey && message.Value == NativeMethods.WmHotkey && (int)message.WParam == HotkeyId) Pressed?.Invoke(this, EventArgs.Empty);
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
            _hotkeyDown = false;
            IsRegistered = false;
        }
    }

    private void SetVirtualKey(int virtualKey)
    {
        lock (_gate)
        {
            if (_thread is not null) throw new InvalidOperationException("Stop the global hotkey listener before configuring it.");
            _virtualKey = virtualKey;
        }
    }

    private static void ValidateVirtualKey(int virtualKey)
    {
        if (!IsSupportedVirtualKey(virtualKey))
        {
            throw new ArgumentOutOfRangeException(
                nameof(virtualKey),
                "Choose a letter, number, punctuation key, numpad key, F1-F11, or F13-F24. F12 is reserved by Windows debuggers.");
        }
    }
}
