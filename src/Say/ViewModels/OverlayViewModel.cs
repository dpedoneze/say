using CommunityToolkit.Mvvm.ComponentModel;
using Say.Models;

namespace Say.ViewModels;

public partial class OverlayViewModel : ObservableObject
{
    private readonly double[] _bars = new double[9];

    [ObservableProperty]
    private DictationState _state = DictationState.Idle;

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private string _noticeText = string.Empty;

    [ObservableProperty]
    private string _elapsedText = string.Empty;

    [ObservableProperty]
    private string _detectedLanguage = string.Empty;

    [ObservableProperty]
    private bool _latched;

    [ObservableProperty]
    private bool _hasPreview;

    public double[] Bars => _bars;

    public bool IsIdle => State == DictationState.Idle;

    public bool IsListening => State == DictationState.Listening;

    public bool IsProcessing => State == DictationState.Processing;

    public bool IsDone => State == DictationState.Done;

    public string StatusText => State switch
    {
        DictationState.Idle => "Say",
        DictationState.Listening => DetectedLanguage.Length > 0
            ? $"Listening · {DetectedLanguage}"
            : "Listening",
        DictationState.Processing => "Transcribing…",
        DictationState.Done => "Pasted",
        _ => string.Empty,
    };

    public string HintText => State switch
    {
        DictationState.Idle when NoticeText.Length > 0 => NoticeText,
        DictationState.Idle => "F9 · tap to toggle, hold to talk",
        DictationState.Listening when Latched => "tap F9 again to finish · Esc cancels",
        DictationState.Listening => "release to finish · Esc cancels",
        DictationState.Processing => "Esc cancels",
        _ => string.Empty,
    };

    public void UpdateBars(float[] levels)
    {
        for (var i = 0; i < _bars.Length; i++)
        {
            var level = i < levels.Length ? Math.Clamp(levels[i], 0, 1) : 0;
            var target = 4 + level * 24;
            _bars[i] = (_bars[i] * 0.5) + (target * 0.5);
        }

        OnPropertyChanged("Item[]");
    }

    partial void OnStateChanged(DictationState value)
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsListening));
        OnPropertyChanged(nameof(IsProcessing));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HintText));
    }

    partial void OnLatchedChanged(bool value) => OnPropertyChanged(nameof(HintText));

    partial void OnDetectedLanguageChanged(string value) => OnPropertyChanged(nameof(StatusText));

    partial void OnNoticeTextChanged(string value) => OnPropertyChanged(nameof(HintText));

    partial void OnPreviewTextChanged(string value) => HasPreview = !string.IsNullOrEmpty(value);
}
