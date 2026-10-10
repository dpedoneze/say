namespace Say.Models;

public enum PasteMethod
{
    Clipboard,
    Type,
}

public sealed class AppSettings
{
    public string? SelectedModelId { get; set; }

    public string? PreviewModelId { get; set; }

    /// <summary>Legacy single-language setting; migrated into <see cref="Languages"/>.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>
    /// Empty: auto-detect across all languages. One entry: fixed language.
    /// Two or more: auto-detect restricted to those candidates.
    /// </summary>
    public List<string> Languages { get; set; } = [];

    /// <summary>Names and terms to bias the model toward and to correct near-misses for.</summary>
    public List<string> Vocabulary { get; set; } = [];

    public int Threads { get; set; } = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);

    public int HotkeyVk { get; set; } = 0x78;

    public bool AppendTrailingSpace { get; set; } = true;

    public PasteMethod PasteMethod { get; set; } = PasteMethod.Clipboard;

    public bool RestoreClipboard { get; set; } = true;

    public int FocusRestoreDelayMs { get; set; } = 140;

    public int AudioDeviceNumber { get; set; } = -1;

    public bool RunAtStartup { get; set; }

    public bool StartWithOverlayHidden { get; set; }

    public bool FirstRunDone { get; set; }
}
