using System.Runtime.InteropServices;
using Say.Interop;

namespace Say.Services;

public readonly record struct DictationTarget(IntPtr Window, IntPtr Focus);

public sealed class TargetWindowService
{
    public DictationTarget Capture()
    {
        var hwnd = Win32.GetForegroundWindow();
        var focus = IntPtr.Zero;

        if (hwnd != IntPtr.Zero)
        {
            try
            {
                var threadId = Win32.GetWindowThreadProcessId(hwnd, out _);
                var info = new Win32.GUITHREADINFO { cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>() };
                if (Win32.GetGUIThreadInfo(threadId, ref info))
                {
                    focus = info.hwndFocus != IntPtr.Zero ? info.hwndFocus : info.hwndCaret;
                }
            }
            catch
            {
                // Focus capture is best effort.
            }
        }

        return new DictationTarget(hwnd, focus);
    }

    public bool Restore(DictationTarget target)
    {
        if (target.Window == IntPtr.Zero || !Win32.IsWindow(target.Window))
        {
            return false;
        }

        try
        {
            if (Win32.IsIconic(target.Window))
            {
                Win32.ShowWindow(target.Window, Win32.SW_RESTORE);
            }

            var targetThread = Win32.GetWindowThreadProcessId(target.Window, out _);
            var currentThread = Win32.GetCurrentThreadId();
            var attached = false;

            try
            {
                if (targetThread != currentThread)
                {
                    attached = Win32.AttachThreadInput(currentThread, targetThread, true);
                }

                var ok = Win32.SetForegroundWindow(target.Window);

                if (target.Focus != IntPtr.Zero && Win32.IsWindow(target.Focus))
                {
                    Win32.SetFocus(target.Focus);
                }

                return ok;
            }
            finally
            {
                if (attached)
                {
                    Win32.AttachThreadInput(currentThread, targetThread, false);
                }
            }
        }
        catch
        {
            return false;
        }
    }
}
