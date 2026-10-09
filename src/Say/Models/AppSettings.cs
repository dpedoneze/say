namespace Say.Models;

public enum PasteMethod
{
    Clipboard,
    Type,
}

public sealed class AppSettings
{
    public string? SelectedModelId { get; set; }

    public string Language { get; set; } = "auto";

    public int Threads { get; set; } = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);

    public int HotkeyVk { get; set; } = 0x78;

    public bool AppendTrailingSpace { get; set; } = true;

    public PasteMethod PasteMethod { get; set; } = PasteMethod.Clipboard;

    public bool RestoreClipboard { get; set; } = true;

    public int FocusRestoreDelayMs { get; set; } = 140;

    public int AudioDeviceNumber { get; set; } = -1;

    public bool RunAtStartup { get; set; }

    public bool FirstRunDone { get; set; }
}
