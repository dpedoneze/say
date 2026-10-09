using System.IO;
using Say.Models;
using Say.Services;

namespace Say.Tests;

public class WhisperModelCatalogTests
{
    [Fact]
    public void Catalog_IdsAreUnique()
    {
        var ids = WhisperModelCatalog.All.Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Catalog_FileNamesAreValidGgmlBinaries()
    {
        foreach (var model in WhisperModelCatalog.All)
        {
            Assert.StartsWith("ggml-", model.FileName);
            Assert.EndsWith(".bin", model.FileName);
            Assert.True(model.ApproxBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(model.Name));
        }
    }

    [Fact]
    public void Catalog_HasExactlyOneRecommendedModel()
    {
        Assert.Single(WhisperModelCatalog.All.Where(m => m.Recommended));
    }

    [Fact]
    public void Find_ReturnsMatchingModel()
    {
        Assert.Equal("small", WhisperModelCatalog.Find("small")?.Id);
        Assert.Null(WhisperModelCatalog.Find("does-not-exist"));
        Assert.Null(WhisperModelCatalog.Find(null));
    }

    [Fact]
    public void DownloadUrl_PointsToHuggingFace()
    {
        var url = WhisperModelCatalog.Find("base")!.DownloadUrl;
        Assert.Equal(
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
            url);
    }
}

public class ModelManagerTests : IDisposable
{
    private readonly string _dir;
    private readonly ModelManager _manager;

    public ModelManagerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "say-model-tests-" + Guid.NewGuid().ToString("N"));
        _manager = new ModelManager(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best effort.
        }
    }

    [Fact]
    public void IsDownloaded_FalseWhenMissing()
    {
        var model = WhisperModelCatalog.Find("tiny")!;
        Assert.False(_manager.IsDownloaded(model));
        Assert.Equal(0, _manager.DownloadedBytes(model));
    }

    [Fact]
    public void IsDownloaded_TrueWhenFileHasExpectedSize()
    {
        var model = WhisperModelCatalog.Find("tiny")!;
        Directory.CreateDirectory(_dir);
        using (var stream = new FileStream(_manager.PathFor(model), FileMode.Create))
        {
            stream.SetLength(model.ApproxBytes);
        }

        Assert.True(_manager.IsDownloaded(model));
        Assert.Equal(model.ApproxBytes, _manager.DownloadedBytes(model));
    }

    [Fact]
    public void IsDownloaded_FalseWhenFileIsTruncated()
    {
        var model = WhisperModelCatalog.Find("tiny")!;
        Directory.CreateDirectory(_dir);
        using (var stream = new FileStream(_manager.PathFor(model), FileMode.Create))
        {
            stream.SetLength(model.ApproxBytes / 2);
        }

        Assert.False(_manager.IsDownloaded(model));
    }

    [Fact]
    public void Delete_RemovesModelFile()
    {
        var model = WhisperModelCatalog.Find("tiny")!;
        Directory.CreateDirectory(_dir);
        using (var stream = new FileStream(_manager.PathFor(model), FileMode.Create))
        {
            stream.SetLength(model.ApproxBytes);
        }

        _manager.Delete(model);

        Assert.False(_manager.IsDownloaded(model));
    }

    [Fact]
    public void PathFor_IsInsideModelsDirectory()
    {
        var model = WhisperModelCatalog.Find("base")!;
        Assert.Equal(Path.Combine(_dir, "ggml-base.bin"), _manager.PathFor(model));
    }
}
