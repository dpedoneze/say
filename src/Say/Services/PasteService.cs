using System.Runtime.InteropServices;
using System.Windows;
using Say.Interop;
using Say.Models;

namespace Say.Services;

public sealed class PasteService
{
    private readonly TargetWindowService _targets;

    public PasteService(TargetWindowService targets)
    {
        _targets = targets;
    }

    public async Task PasteAsync(
        string text,
        DictationTarget target,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (settings.PasteMethod == PasteMethod.Type)
        {
            _targets.Restore(target);
            await Task.Delay(settings.FocusRestoreDelayMs, cancellationToken);
            SendTextTyped(text);
            return;
        }

        string? savedClipboard = null;
        if (settings.RestoreClipboard)
        {
            savedClipboard = TryGetClipboardText();
        }

        if (!TrySetClipboardText(text))
        {
            // Clipboard was locked by another app: fall back to typing.
            _targets.Restore(target);
            await Task.Delay(settings.FocusRestoreDelayMs, cancellationToken);
            SendTextTyped(text);
            return;
        }

        _targets.Restore(target);
        await Task.Delay(settings.FocusRestoreDelayMs, cancellationToken);
        SendCtrlV();

        if (savedClipboard is not null)
        {
            await Task.Delay(600, cancellationToken);
            TrySetClipboardText(savedClipboard);
        }
    }

    private static string? TryGetClipboardText()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch
            {
                Thread.Sleep(40);
            }
        }

        return null;
    }

    private static bool TrySetClipboardText(string text)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch
            {
                Thread.Sleep(40);
            }
        }

        return false;
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyInput(Win32.VK_CONTROL, down: true),
            KeyInput(Win32.VK_V, down: true),
            KeyInput(Win32.VK_V, down: false),
            KeyInput(Win32.VK_CONTROL, down: false),
        };

        Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendTextTyped(string text)
    {
        foreach (var ch in text)
        {
            var inputs = new[]
            {
                UnicodeInput(ch, down: true),
                UnicodeInput(ch, down: false),
            };

            Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
            Thread.Sleep(2);
        }
    }

    private static Win32.INPUT KeyInput(ushort vk, bool down) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        u = new Win32.INPUTUNION
        {
            ki = new Win32.KEYBDINPUT
            {
                wVk = vk,
                dwFlags = down ? 0 : Win32.KEYEVENTF_KEYUP,
            },
        },
    };

    private static Win32.INPUT UnicodeInput(char ch, bool down) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        u = new Win32.INPUTUNION
        {
            ki = new Win32.KEYBDINPUT
            {
                wScan = ch,
                dwFlags = Win32.KEYEVENTF_UNICODE | (down ? 0 : Win32.KEYEVENTF_KEYUP),
            },
        },
    };
}
