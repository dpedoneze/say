using System.Windows.Threading;
using Say.Models;
using Say.ViewModels;

namespace Say.Services;

public sealed class DictationController : IDisposable
{
    private const int PreviewIntervalMs = 850;
    private const int PreviewWindowSeconds = 25;

    private readonly AudioCaptureService _audio;
    private readonly TranscriptionService _transcription;
    private readonly ModelManager _models;
    private readonly PasteService _paste;
    private readonly TargetWindowService _targets;
    private readonly SettingsStore _settings;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly OverlayViewModel _vm;
    private readonly Action<IntPtr> _repositionOverlay;
    private readonly Func<Task> _openSettings;

    private readonly DispatcherTimer _ticker;
    private readonly DispatcherTimer _noticeTimer;

    private DictationState _state = DictationState.Idle;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _finalCts;
    private DictationTarget _target;
    private DateTime _pressStartedUtc;
    private bool _trackingPress;
    private bool _latched;
    private bool _busy;
    private int _lastPreviewSampleCount;
    private string _lastPreviewText = string.Empty;

    public DictationController(
        AudioCaptureService audio,
        TranscriptionService transcription,
        ModelManager models,
        PasteService paste,
        TargetWindowService targets,
        SettingsStore settings,
        GlobalHotkeyService hotkeys,
        OverlayViewModel vm,
        Action<IntPtr> repositionOverlay,
        Func<Task> openSettings)
    {
        _audio = audio;
        _transcription = transcription;
        _models = models;
        _paste = paste;
        _targets = targets;
        _settings = settings;
        _hotkeys = hotkeys;
        _vm = vm;
        _repositionOverlay = repositionOverlay;
        _openSettings = openSettings;

        _hotkeys.PressStarted += OnPressStarted;
        _hotkeys.PressEnded += OnPressEnded;
        _hotkeys.CancelRequested += OnCancel;

        _ticker = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _ticker.Tick += OnTick;

        _noticeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4),
        };
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            _vm.NoticeText = string.Empty;
        };
    }

    public void OnPressStarted()
    {
        if (_busy)
        {
            return;
        }

        switch (PressGestureLogic.OnPress(_state, _latched))
        {
            case PressOutcome.StartTracking:
                _pressStartedUtc = DateTime.UtcNow;
                _trackingPress = true;
                _latched = false;
                _vm.Latched = false;
                StartSession();
                break;
            case PressOutcome.Stop:
                _ = StopAndFinishAsync();
                break;
        }
    }

    public void OnPressEnded()
    {
        if (!_trackingPress)
        {
            return;
        }

        _trackingPress = false;
        var heldMs = (int)(DateTime.UtcNow - _pressStartedUtc).TotalMilliseconds;

        switch (PressGestureLogic.OnRelease(true, heldMs, _state))
        {
            case PressOutcome.Latch:
                _latched = true;
                _vm.Latched = true;
                break;
            case PressOutcome.Stop:
                _ = StopAndFinishAsync();
                break;
        }
    }

    public void OnCancel()
    {
        switch (_state)
        {
            case DictationState.Listening:
                CancelSession();
                break;
            case DictationState.Processing:
                _finalCts?.Cancel();
                break;
        }
    }

    private void StartSession()
    {
        var settings = _settings.Current;

        _target = _targets.Capture();
        _repositionOverlay(_target.Window);

        var model = WhisperModelCatalog.Find(settings.SelectedModelId);

        if (model is null || !_models.IsDownloaded(model))
        {
            ShowNotice("No model yet — opening Settings to download one");
            _ = _openSettings();
            return;
        }

        try
        {
            if (!string.Equals(_transcription.LoadedModelPath, _models.PathFor(model), StringComparison.OrdinalIgnoreCase))
            {
                _transcription.Load(_models.PathFor(model));
            }
        }
        catch (Exception ex)
        {
            ShowNotice("Model failed to load: " + ex.Message);
            return;
        }

        try
        {
            _audio.Start(settings.AudioDeviceNumber);
        }
        catch (Exception ex)
        {
            ShowNotice("Microphone unavailable: " + ex.Message);
            return;
        }

        _lastPreviewSampleCount = 0;
        _lastPreviewText = string.Empty;
        _vm.PreviewText = string.Empty;
        SetState(DictationState.Listening);

        _previewCts = new CancellationTokenSource();
        _ = RunPreviewLoopAsync(settings.Language, settings.Threads, _previewCts.Token);
        _ticker.Start();
    }

    private void CancelSession()
    {
        _previewCts?.Cancel();
        _audio.Stop();
        _ticker.Stop();
        ShowNotice("Cancelled");
        ResetToIdle();
    }

    private async Task StopAndFinishAsync()
    {
        if (_busy || _state != DictationState.Listening)
        {
            return;
        }

        _busy = true;
        _ticker.Stop();
        _previewCts?.Cancel();
        _audio.Stop();

        try
        {
            var settings = _settings.Current;
            var samples = _audio.Snapshot();

            SetState(DictationState.Processing);
            _vm.PreviewText = _lastPreviewText;

            if (samples.Length < AudioCaptureService.SampleRate * 0.25)
            {
                ShowNotice("Nothing recorded");
                return;
            }

            string text;
            _finalCts = new CancellationTokenSource();
            try
            {
                text = await _transcription.TranscribeAsync(
                    samples,
                    new TranscriptionOptions(settings.Language, settings.Threads),
                    _finalCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ShowNotice("Transcription failed: " + ex.Message);
                return;
            }

            text = TextNormalizer.Normalize(text, settings.AppendTrailingSpace);
            if (text.Length == 0)
            {
                ShowNotice("Nothing recognized");
                return;
            }

            _vm.PreviewText = text;
            try
            {
                await _paste.PasteAsync(text, _target, settings);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ShowNotice("Paste failed: " + ex.Message);
                return;
            }

            SetState(DictationState.Done);
            await Task.Delay(900);
        }
        catch (Exception ex)
        {
            ShowNotice("Something went wrong: " + ex.Message);
        }
        finally
        {
            ResetToIdle();
        }
    }

    private async Task RunPreviewLoopAsync(string language, int threads, CancellationToken cancellationToken)
    {
        var options = new TranscriptionOptions(language, threads);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PreviewIntervalMs, cancellationToken);

                var total = _audio.SampleCount;
                if (total < AudioCaptureService.SampleRate * 0.4 || total == _lastPreviewSampleCount)
                {
                    continue;
                }

                var samples = _audio.Snapshot();
                _lastPreviewSampleCount = total;

                var windowSamples = AudioCaptureService.SampleRate * PreviewWindowSeconds;
                var partial = samples.Length > windowSamples;
                if (partial)
                {
                    samples = samples[^windowSamples..];
                }

                string text;
                try
                {
                    text = await _transcription.TranscribeAsync(samples, options, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    continue;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _lastPreviewText = text;
                _vm.PreviewText = partial ? "… " + text : text;
            }
        }
        catch (OperationCanceledException)
        {
            // Session ended.
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_state != DictationState.Listening)
        {
            return;
        }

        _vm.UpdateBars(_audio.Bars);
        var elapsed = _audio.Elapsed;
        _vm.ElapsedText = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
    }

    private void SetState(DictationState state)
    {
        _state = state;
        _vm.State = state;
        _hotkeys.DictationActive = state is DictationState.Listening or DictationState.Processing;
    }

    private void ResetToIdle()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;

        _finalCts?.Cancel();
        _finalCts?.Dispose();
        _finalCts = null;

        _ticker.Stop();
        _audio.Stop();

        _latched = false;
        _vm.Latched = false;
        _trackingPress = false;
        _lastPreviewText = string.Empty;
        _vm.PreviewText = string.Empty;
        _vm.ElapsedText = string.Empty;

        SetState(DictationState.Idle);
        _busy = false;
    }

    private void ShowNotice(string message)
    {
        _vm.NoticeText = message;
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    public void Dispose()
    {
        _hotkeys.PressStarted -= OnPressStarted;
        _hotkeys.PressEnded -= OnPressEnded;
        _hotkeys.CancelRequested -= OnCancel;
        _ticker.Stop();
        _noticeTimer.Stop();
        _audio.Stop();
    }
}
