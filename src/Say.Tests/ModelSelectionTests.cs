using Say.Models;
using Say.Services;

namespace Say.Tests;

public class ModelSelectionTests
{
    private static readonly WhisperModelInfo Final = WhisperModelCatalog.Find("small")!;
    private static readonly WhisperModelInfo Preview = WhisperModelCatalog.Find("tiny")!;

    [Fact]
    public void ResolvePreview_NoPreviewModel_UsesFinal()
    {
        var result = ModelSelection.ResolvePreview(Final, previewModel: null, _ => true);
        Assert.Equal(Final, result);
    }

    [Fact]
    public void ResolvePreview_PreviewNotDownloaded_UsesFinal()
    {
        var result = ModelSelection.ResolvePreview(Final, Preview, m => m != Preview);
        Assert.Equal(Final, result);
    }

    [Fact]
    public void ResolvePreview_DownloadedPreview_UsesPreview()
    {
        var result = ModelSelection.ResolvePreview(Final, Preview, _ => true);
        Assert.Equal(Preview, result);
    }

    [Fact]
    public void ResolvePreview_NoFinalModel_ReturnsNull()
    {
        var result = ModelSelection.ResolvePreview(null, Preview, m => m != Preview);
        Assert.Null(result);
    }
}
