using CommunityToolkit.Mvvm.ComponentModel;
using Say.Models;
using Say.Services;

namespace Say.ViewModels;

public partial class ModelItemViewModel : ObservableObject
{
    private readonly ModelManager _models;
    private readonly Action<ModelItemViewModel> _onSelected;

    public ModelItemViewModel(
        WhisperModelInfo info,
        ModelManager models,
        Action<ModelItemViewModel> onSelected)
    {
        Info = info;
        _models = models;
        _onSelected = onSelected;
        Refresh();
    }

    public WhisperModelInfo Info { get; }

    public string Id => Info.Id;

    public string Name => Info.Name;

    public string Note => Info.Note;

    public string SizeText => FormatBytes(Info.ApproxBytes);

    public bool IsRecommended => Info.Recommended;

    [ObservableProperty]
    private bool _isDownloaded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isPreviewRole;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public bool CanDownload => !IsDownloaded && !IsDownloading;

    public bool CanCancel => IsDownloading;

    public bool CanDelete => IsDownloaded && !IsDownloading;

    public bool CanSelect => IsDownloaded;

    public CancellationTokenSource? DownloadCts { get; set; }

    public void Refresh()
    {
        IsDownloaded = _models.IsDownloaded(Info);
        UpdateStatus();
        RaiseCanFlags();
    }

    public void SetDownloadFailed(string message)
    {
        StatusText = message;
        OnPropertyChanged(nameof(StatusText));
    }

    public static string FormatBytes(long bytes) =>
        bytes >= 1_000_000_000
            ? $"{bytes / 1_073_741_824.0:0.0} GB"
            : $"{bytes / 1_048_576.0:0} MB";

    private void UpdateStatus()
    {
        StatusText = IsDownloading
            ? $"Downloading… {Progress:0}%"
            : IsDownloaded
                ? $"Downloaded · {SizeText}"
                : "Not downloaded";
        OnPropertyChanged(nameof(StatusText));
    }

    private void RaiseCanFlags()
    {
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanSelect));
    }

    partial void OnIsDownloadingChanged(bool value)
    {
        UpdateStatus();
        RaiseCanFlags();
    }

    partial void OnProgressChanged(double value) => UpdateStatus();

    partial void OnIsDownloadedChanged(bool value)
    {
        UpdateStatus();
        RaiseCanFlags();
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _onSelected(this);
        }
    }
}
