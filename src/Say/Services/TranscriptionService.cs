using System.Text;
using Whisper.net;

namespace Say.Services;

public readonly record struct TranscriptionOptions(
    string? Language,
    IReadOnlyList<string> Candidates,
    string? Prompt,
    int Threads);

public sealed class TranscriptionService : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;
    private string? _modelPath;

    public string? LoadedModelPath
    {
        get
        {
            _gate.Wait();
            try
            {
                return _modelPath;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public void Load(string modelPath)
    {
        _gate.Wait();
        try
        {
            if (_factory is not null && string.Equals(_modelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _factory?.Dispose();
            _factory = WhisperFactory.FromPath(modelPath);
            _modelPath = modelPath;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Unload()
    {
        _gate.Wait();
        try
        {
            _factory?.Dispose();
            _factory = null;
            _modelPath = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> TranscribeAsync(
        float[] samples,
        TranscriptionOptions options,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var factory = _factory ?? throw new InvalidOperationException("No Whisper model is loaded.");

            return await Task.Run(async () =>
            {
                var builder = factory.CreateBuilder()
                    .WithThreads(Math.Clamp(options.Threads, 1, 32))
                    .WithNoContext();

                if (!string.IsNullOrEmpty(options.Language))
                {
                    builder = builder.WithLanguage(options.Language);
                }
                else
                {
                    builder = builder.WithLanguageDetection();
                }

                if (!string.IsNullOrWhiteSpace(options.Prompt))
                {
                    builder = builder.WithPrompt(options.Prompt);
                }

                await using var processor = builder.Build();
                var sb = new StringBuilder();
                await foreach (var segment in processor.ProcessAsync(samples, cancellationToken))
                {
                    sb.Append(segment.Text);
                }

                return sb.ToString().Trim();
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> DetectLanguageAsync(
        float[] samples,
        IReadOnlyList<string> candidates,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var factory = _factory ?? throw new InvalidOperationException("No Whisper model is loaded.");

            return await Task.Run(async () =>
            {
                await using var processor = factory.CreateBuilder().Build();
                var (language, _) = processor.DetectLanguageWithProbability(samples, candidates.ToArray());
                return language;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
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

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            _factory?.Dispose();
            _factory = null;
            _modelPath = null;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
