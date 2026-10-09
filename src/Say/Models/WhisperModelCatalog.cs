namespace Say.Models;

public sealed record WhisperModelInfo(
    string Id,
    string Name,
    string FileName,
    long ApproxBytes,
    string Note,
    bool Recommended = false)
{
    public string DownloadUrl => WhisperModelCatalog.BaseUrl + FileName;
}

public static class WhisperModelCatalog
{
    public const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    public static readonly IReadOnlyList<WhisperModelInfo> All =
    [
        new("tiny", "Tiny", "ggml-tiny.bin", 77_691_713L, "Fastest, lowest accuracy"),
        new("base", "Base", "ggml-base.bin", 147_951_465L, "Fast, decent accuracy"),
        new("small", "Small", "ggml-small.bin", 487_601_967L, "Great balance of speed and accuracy", Recommended: true),
        new("small-q5_1", "Small (Q5)", "ggml-small-q5_1.bin", 190_085_487L, "Quantized small: less RAM, slightly less accurate"),
        new("medium-q5_0", "Medium (Q5)", "ggml-medium-q5_0.bin", 539_212_467L, "Quantized medium: high accuracy, CPU friendly"),
        new("large-v3-turbo-q5_0", "Large v3 Turbo (Q5)", "ggml-large-v3-turbo-q5_0.bin", 574_041_195L, "Quantized turbo: best accuracy for the size"),
        new("large-v3", "Large v3", "ggml-large-v3.bin", 3_095_033_483L, "Maximum accuracy, slow on CPU"),
    ];

    public static WhisperModelInfo? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(m => m.Id == id);
}
