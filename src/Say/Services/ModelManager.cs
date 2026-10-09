using System.IO;
using System.Net.Http;
using Say.Models;

namespace Say.Services;

public sealed class ModelManager
{
    private static readonly HttpClient Http = CreateClient();

    public ModelManager(string? directory = null)
    {
        ModelsDirectory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Say", "models");
    }

    public string ModelsDirectory { get; }

    public IReadOnlyList<WhisperModelInfo> Catalog => WhisperModelCatalog.All;

    public string PathFor(WhisperModelInfo model) => Path.Combine(ModelsDirectory, model.FileName);

    public bool IsDownloaded(WhisperModelInfo model)
    {
        try
        {
            var info = new FileInfo(PathFor(model));
            return info.Exists && info.Length >= model.ApproxBytes * 0.9;
        }
        catch
        {
            return false;
        }
    }

    public long DownloadedBytes(WhisperModelInfo model)
    {
        try
        {
            var info = new FileInfo(PathFor(model));
            return info.Exists ? info.Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task DownloadAsync(
        WhisperModelInfo model,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ModelsDirectory);
        var finalPath = PathFor(model);
        var partPath = finalPath + ".part";

        try
        {
            using var response = await Http.GetAsync(
                model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? model.ApproxBytes;
            var received = 0L;

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = new FileStream(
                partPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var buffer = new byte[1 << 16];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    progress.Report(total > 0 ? Math.Clamp((double)received / total, 0, 1) : 0);
                }
            }

            File.Move(partPath, finalPath, overwrite: true);
            progress.Report(1);
        }
        catch
        {
            try
            {
                if (File.Exists(partPath))
                {
                    File.Delete(partPath);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }

            throw;
        }
    }

    public void Delete(WhisperModelInfo model)
    {
        var path = PathFor(model);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Say/0.1 (offline dictation)");
        return client;
    }
}
