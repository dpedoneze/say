using System.IO;
using System.Text;
using Whisper.net;

namespace Say.Services;

public readonly record struct TranscriptionOptions(string Language, int Threads);

public sealed class TranscriptionService : IDisposable
{
    private readonly object _lock = new();
    private WhisperFactory? _factory;
    private string? _modelPath;

    public string? LoadedModelPath
    {
        get
        {
            lock (_lock)
            {
                return _modelPath;
            }
        }
    }

    public void Load(string modelPath)
    {
        lock (_lock)
        {
            if (_factory is not null && string.Equals(_modelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _factory?.Dispose();
            _factory = WhisperFactory.FromPath(modelPath);
            _modelPath = modelPath;
        }
    }

    public void Unload()
    {
        lock (_lock)
        {
            _factory?.Dispose();
            _factory = null;
            _modelPath = null;
        }
    }

    public async Task<string> TranscribeAsync(
        float[] samples,
        TranscriptionOptions options,
        CancellationToken cancellationToken)
    {
        WhisperFactory factory;
        lock (_lock)
        {
            factory = _factory ?? throw new InvalidOperationException("No Whisper model is loaded.");
        }

        var builder = factory.CreateBuilder()
            .WithThreads(Math.Clamp(options.Threads, 1, 32))
            .WithNoContext();

        if (string.IsNullOrEmpty(options.Language) || options.Language == "auto")
        {
            builder = builder.WithLanguageDetection();
        }
        else
        {
            builder = builder.WithLanguage(options.Language);
        }

        return await Task.Run(async () =>
        {
            await using var processor = builder.Build();
            var sb = new StringBuilder();
            await foreach (var segment in processor.ProcessAsync(samples, cancellationToken))
            {
                sb.Append(segment.Text);
            }

            return sb.ToString().Trim();
        }, cancellationToken).ConfigureAwait(false);
    }

    public static string GetRuntimeInfo()
    {
        try
        {
            return WhisperFactory.GetRuntimeInfo() ?? "unknown";
        }
        catch (Exception ex)
        {
            return "unavailable: " + ex.Message;
        }
    }

    public void Dispose() => Unload();
}
