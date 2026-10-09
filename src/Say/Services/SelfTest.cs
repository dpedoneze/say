using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Say.Interop;
using Say.Models;

namespace Say.Services;

internal static class SelfTest
{
    public static int Run(string[] args)
    {
        PrepareConsole();

        try
        {
            var downloadId = GetArgValue(args, "--download");
            if (downloadId is not null)
            {
                return RunDownload(downloadId);
            }

            var recordSeconds = GetArgValue(args, "--record");
            if (recordSeconds is not null)
            {
                return RunRecord(
                    double.Parse(recordSeconds, System.Globalization.CultureInfo.InvariantCulture),
                    args);
            }

            var store = new SettingsStore();
            store.Load();

            var modelPath = GetArgValue(args, "--model");            if (modelPath is null)
            {
                var manager = new ModelManager();
                var selected = WhisperModelCatalog.Find(store.Current.SelectedModelId);
                if (selected is not null && manager.IsDownloaded(selected))
                {
                    modelPath = manager.PathFor(selected);
                }
                else
                {
                    modelPath = manager.Catalog
                        .Where(manager.IsDownloaded)
                        .Select(manager.PathFor)
                        .FirstOrDefault();
                }
            }

            if (modelPath is null)
            {
                Log("SKIP: no Whisper model downloaded. Download one in Settings first, or pass --model <path>.");
                return 2;
            }

            Log($"Model: {modelPath}");

            var wavPath = GetArgValue(args, "--wav");
            if (wavPath is null)
            {
                wavPath = Path.Combine(Path.GetTempPath(), "say-selftest.wav");
                Log("Synthesizing speech with Windows SAPI...");
                SynthesizeSpeech(wavPath);
            }

            var samples = LoadWavAs16kMono(wavPath);
            Log($"Audio: {samples.Length / (double)AudioCaptureService.SampleRate:0.0}s at 16 kHz mono");
            if (samples.Length < 8000)
            {
                Log("FAIL: audio shorter than 0.5s.");
                return 1;
            }

            Log($"Runtime: {TranscriptionService.GetRuntimeInfo()}");

            using var transcription = new TranscriptionService();
            transcription.Load(modelPath);

            var text = transcription
                .TranscribeAsync(
                    samples,
                    BuildOptions(store.Current),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Log($"Transcription: \"{text}\"");

            var vocabulary = VocabularyCorrector.Parse(string.Join('\n', store.Current.Vocabulary));
            var corrected = VocabularyCorrector.Apply(text, vocabulary);
            if (!string.Equals(corrected, text, StringComparison.Ordinal))
            {
                Log($"Corrected: \"{corrected}\"");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Log("FAIL: empty transcription.");
                return 1;
            }

            Log("PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Log("FAIL: " + ex);
            return 1;
        }
    }

    private static int RunRecord(double seconds, string[] args)
    {
        var store = new SettingsStore();
        store.Load();

        Log("Input devices:");
        foreach (var (number, name) in AudioCaptureService.ListDevices())
        {
            Log($"  [{number}] {name}");
        }

        using var audio = new AudioCaptureService();
        try
        {
            audio.Start(store.Current.AudioDeviceNumber);
        }
        catch (Exception ex)
        {
            Log("FAIL: microphone unavailable: " + ex.Message);
            return 1;
        }

        Log($"Recording {seconds:0.#}s (device {store.Current.AudioDeviceNumber})...");
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
            var bars = audio.Bars;
            var level = bars.Max();
            Log($"  level {level:0.00} | {string.Join(string.Empty, bars.Select(b => b > 0.6 ? '#' : b > 0.3 ? '+' : b > 0.1 ? '.' : ' '))}");
        }

        audio.Stop();
        var samples = audio.Snapshot();

        if (samples.Length == 0)
        {
            Log("FAIL: no samples captured.");
            return 1;
        }

        double peak = 0;
        double sumSquares = 0;
        foreach (var sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
            sumSquares += sample * sample;
        }

        var rms = Math.Sqrt(sumSquares / samples.Length);
        Log($"Captured {samples.Length / (double)AudioCaptureService.SampleRate:0.0}s | peak {peak:0.000} | rms {rms:0.0000}");

        var wavPath = Path.Combine(Path.GetTempPath(), "say-record.wav");
        WriteWav16kMono(wavPath, samples);
        Log("Saved: " + wavPath);

        if (peak < 0.005)
        {
            Log("FAIL: audio is silent (check microphone / volume).");
            return 1;
        }

        var modelPath = GetArgValue(args, "--model");
        var manager = new ModelManager();
        var selected = WhisperModelCatalog.Find(store.Current.SelectedModelId);
        modelPath ??= selected is not null && manager.IsDownloaded(selected)
            ? manager.PathFor(selected)
            : manager.Catalog.Where(manager.IsDownloaded).Select(manager.PathFor).FirstOrDefault();

        if (modelPath is not null)
        {
            using var transcription = new TranscriptionService();
            transcription.Load(modelPath);
            var text = transcription
                .TranscribeAsync(
                    samples,
                    BuildOptions(store.Current),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Log($"Transcription: \"{text}\"");
        }

        Log("PASS");
        return 0;
    }

    private static void WriteWav16kMono(string path, float[] samples)
    {
        using var writer = new WaveFileWriter(path, new WaveFormat(AudioCaptureService.SampleRate, 16, 1));
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)Math.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        writer.Write(bytes, 0, bytes.Length);
    }

    private static int RunDownload(string id)
    {
        var manager = new ModelManager();
        var model = WhisperModelCatalog.Find(id)
            ?? WhisperModelCatalog.All.FirstOrDefault(m => m.Recommended);

        if (model is null)
        {
            Log($"FAIL: unknown model id \"{id}\".");
            return 1;
        }

        if (manager.IsDownloaded(model))
        {
            Log($"Already downloaded: {manager.PathFor(model)}");
            return 0;
        }

        Log($"Downloading {model.Name} ({ModelItemSize(model.ApproxBytes)})...");
        var lastReported = -1;
        var progress = new SynchronousProgress(p =>
        {
            var percent = (int)(p * 100);
            if (percent >= lastReported + 5 || percent == 100)
            {
                lastReported = percent;
                Log($"  {percent}%");
            }
        });

        manager.DownloadAsync(model, progress, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Log("Downloaded: " + manager.PathFor(model));
        return 0;
    }

    private static string ModelItemSize(long bytes) =>
        bytes >= 1_000_000_000
            ? $"{bytes / 1_073_741_824.0:0.0} GB"
            : $"{bytes / 1_048_576.0:0} MB";

    private sealed class SynchronousProgress(Action<double> handler) : IProgress<double>
    {
        public void Report(double value) => handler(value);
    }

    private static TranscriptionOptions BuildOptions(AppSettings settings)
    {
        var vocabulary = VocabularyCorrector.Parse(string.Join('\n', settings.Vocabulary));
        return new TranscriptionOptions(
            LanguageSelection.FixedLanguage(settings),
            LanguageSelection.Candidates(settings),
            VocabularyCorrector.BuildPrompt(vocabulary),
            settings.Threads);
    }

    private static void PrepareConsole()
    {
        try
        {
            Win32.AttachConsole(Win32.ATTACH_PARENT_PROCESS);
            var stdout = Console.OpenStandardOutput();
            Console.SetOut(new StreamWriter(stdout) { AutoFlush = true });
        }
        catch
        {
            // Without a console, results go to the log file only.
        }

        try
        {
            File.WriteAllText(LogPath, string.Empty);
        }
        catch
        {
            // Logging is best effort.
        }
    }

    public static string LogPath { get; } = Path.Combine(Path.GetTempPath(), "say-selftest.log");

    private static void Log(string message)
    {
        var line = "[selftest] " + message;
        Console.WriteLine(line);

        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // Logging is best effort.
        }
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void SynthesizeSpeech(string path)
    {
        var voiceType = Type.GetTypeFromProgID("SAPI.SpVoice")
            ?? throw new InvalidOperationException("Windows SAPI voice is not available on this system.");
        var streamType = Type.GetTypeFromProgID("SAPI.SpFileStream")
            ?? throw new InvalidOperationException("Windows SAPI file stream is not available on this system.");

        dynamic voice = Activator.CreateInstance(voiceType)!;
        dynamic stream = Activator.CreateInstance(streamType)!;

        // SSFMCreateForWrite = 3
        stream.Open(path, 3, false);
        try
        {
            try
            {
                dynamic voices = voice.GetVoices();
                for (var i = 0; i < voices.Count; i++)
                {
                    dynamic candidate = voices.Item(i);
                    string language = candidate.GetAttribute("Language");
                    if (language.StartsWith("409", StringComparison.OrdinalIgnoreCase))
                    {
                        voice.Voice = candidate;
                        break;
                    }
                }
            }
            catch
            {
                // Use the default voice.
            }

            voice.AudioOutputStream = stream;
            voice.Speak("The quick brown fox jumps over the lazy dog. This is an offline dictation test.");
        }
        finally
        {
            stream.Close();
        }
    }

    private static float[] LoadWavAs16kMono(string path)
    {
        using var reader = new WaveFileReader(path);
        ISampleProvider provider = reader.ToSampleProvider();

        if (provider.WaveFormat.Channels == 2)
        {
            provider = new StereoToMonoSampleProvider(provider);
        }

        if (provider.WaveFormat.SampleRate != AudioCaptureService.SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, AudioCaptureService.SampleRate);
        }

        var samples = new List<float>();
        var buffer = new float[AudioCaptureService.SampleRate];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                samples.Add(buffer[i]);
            }
        }

        return samples.ToArray();
    }
}
