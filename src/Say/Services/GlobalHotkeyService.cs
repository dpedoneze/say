using System.Runtime.InteropServices;
using Say.Interop;

namespace Say.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly SynchronizationContext _syncContext;
    private readonly Func<int> _hotkeyVk;
    private readonly Win32.LowLevelKeyboardProc _proc;
    private IntPtr _hook;
    private bool _hotkeyDown;

    public GlobalHotkeyService(SynchronizationContext syncContext, Func<int> hotkeyVk)
    {
        _syncContext = syncContext;
        _hotkeyVk = hotkeyVk;
        _proc = HookCallback;
    }

    public event Action? PressStarted;

    public event Action? PressEnded;

    public event Action? CancelRequested;

    public bool DictationActive { get; set; }

    public bool IsStarted => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = Win32.SetWindowsHookEx(
            Win32.WH_KEYBOARD_LL, _proc, Win32.GetModuleHandle(null), 0);

        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Failed to install the global keyboard hook (error " + Marshal.GetLastWin32Error() + ").");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var data = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);

                // Allow injected keys (e.g. remapped hotkeys from AutoHotkey); only ignore
                // events injected from lower integrity levels.
                var lowerIlInjected = (data.flags & Win32.LLKHF_LOWER_IL_INJECTED) != 0;

                if (!lowerIlInjected)
                {
                    var message = (int)wParam;
                    var isDown = message is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
                    var isUp = message is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;

                    if (data.vkCode == (uint)_hotkeyVk())
                    {
                        if (isDown && !_hotkeyDown)
                        {
                            _hotkeyDown = true;
                            Post(PressStarted);
                        }
                        else if (isUp && _hotkeyDown)
                        {
                            _hotkeyDown = false;
                            Post(PressEnded);
                        }

                        return (IntPtr)1;
                    }

                    if (data.vkCode == Win32.VK_ESCAPE && DictationActive)
                    {
                        if (isDown)
                        {
                            Post(CancelRequested);
                        }

                        return (IntPtr)1;
                    }
                }
            }
            catch
            {
                // A hook must never throw into the OS.
            }
        }

        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void Post(Action? action)
    {
        if (action is null)
        {
            return;
        }

        _syncContext.Post(_ => action(), null);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
