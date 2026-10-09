using Say.Models;

namespace Say.Services;

public static class ModelSelection
{
    public static WhisperModelInfo? ResolvePreview(
        WhisperModelInfo? finalModel,
        WhisperModelInfo? previewModel,
        Func<WhisperModelInfo, bool> isDownloaded)
    {
        if (previewModel is not null && isDownloaded(previewModel))
        {
            return previewModel;
        }

        return finalModel;
    }
}
