using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Say.Models;
using Say.Services;

namespace Say.ViewModels;

public sealed record AudioDeviceItem(int Number, string Name)
{
    public override string ToString() => Name;
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly ModelManager _models;
    private readonly TranscriptionService _transcription;
    private bool _suppressSelection;

    [ObservableProperty]
    private string _selectedLanguageCode;

    [ObservableProperty]
    private int _threads;

    [ObservableProperty]
    private bool _appendTrailingSpace;

    [ObservableProperty]
    private bool _restoreClipboard;

    [ObservableProperty]
    private int _focusRestoreDelayMs;

    [ObservableProperty]
    private bool _runAtStartup;

    [ObservableProperty]
    private int _hotkeyVk;

    [ObservableProperty]
    private int _selectedDeviceNumber;

    [ObservableProperty]
    private bool _showFirstRunBanner;

    private PasteMethod _pasteMethod;

    public SettingsViewModel(
        SettingsStore store,
        ModelManager models,
        TranscriptionService transcription)
    {
        _store = store;
        _models = models;
        _transcription = transcription;

        var s = store.Current;
        _selectedLanguageCode = s.Language;
        _threads = s.Threads;
        _appendTrailingSpace = s.AppendTrailingSpace;
        _restoreClipboard = s.RestoreClipboard;
        _focusRestoreDelayMs = s.FocusRestoreDelayMs;
        _runAtStartup = s.RunAtStartup;
        _hotkeyVk = s.HotkeyVk;
        _selectedDeviceNumber = s.AudioDeviceNumber;
        _pasteMethod = s.PasteMethod;

        LoadAudioDevices();
        RefreshModels();
    }

    public IReadOnlyList<WhisperLanguage> Languages => WhisperLanguages.All;

    public ObservableCollection<AudioDeviceItem> AudioDevices { get; } = [];

    public ObservableCollection<ModelItemViewModel> Models { get; } = [];

    public string HotkeyDisplay => KeyInterop.KeyFromVirtualKey(HotkeyVk).ToString();

    public bool UseClipboard
    {
        get => _pasteMethod == PasteMethod.Clipboard;
        set
        {
            if (value)
            {
                SetPasteMethod(PasteMethod.Clipboard);
            }
        }
    }

    public bool UseTyping
    {
        get => _pasteMethod == PasteMethod.Type;
        set
        {
            if (value)
            {
                SetPasteMethod(PasteMethod.Type);
            }
        }
    }

    public string VersionText =>
        "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");

    public string RuntimeInfo => TranscriptionService.GetRuntimeInfo();

    public string ModelsFolder => _models.ModelsDirectory;

    public void SetHotkey(Key key)
    {
        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            return;
        }

        HotkeyVk = vk;
        _store.Current.HotkeyVk = vk;
        _store.Save();
        OnPropertyChanged(nameof(HotkeyDisplay));
    }

    public void RefreshModels()
    {
        _suppressSelection = true;
        Models.Clear();

        var selectedId = _store.Current.SelectedModelId;
        foreach (var info in _models.Catalog)
        {
            var item = new ModelItemViewModel(info, _models, OnModelSelected)
            {
                IsSelected = info.Id == selectedId,
            };
            Models.Add(item);
        }

        _suppressSelection = false;
        UpdateFirstRunBanner();
    }

    public void StartDownload(ModelItemViewModel item)
    {
        if (item.IsDownloading || item.IsDownloaded)
        {
            return;
        }

        item.DownloadCts = new CancellationTokenSource();
        item.IsDownloading = true;
        item.Progress = 0;
        var progress = new Progress<double>(p => item.Progress = p * 100);

        _ = RunDownloadAsync(item, progress, item.DownloadCts.Token);
    }

    private async Task RunDownloadAsync(
        ModelItemViewModel item,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await _models.DownloadAsync(item.Info, progress, cancellationToken);
            item.Refresh();

            if (string.IsNullOrEmpty(_store.Current.SelectedModelId))
            {
                item.IsSelected = true;
            }
        }
        catch (OperationCanceledException)
        {
            item.Refresh();
        }
        catch (Exception ex)
        {
            item.SetDownloadFailed("Download failed: " + ex.Message);
        }
        finally
        {
            item.IsDownloading = false;
            item.DownloadCts?.Dispose();
            item.DownloadCts = null;
            item.Refresh();
            UpdateFirstRunBanner();
        }
    }

    public void CancelDownload(ModelItemViewModel item)
    {
        item.DownloadCts?.Cancel();
    }

    public void DeleteModel(ModelItemViewModel item)
    {
        if (!item.IsDownloaded || item.IsDownloading)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Delete {item.Name} ({item.SizeText}) from disk?",
            "Say — delete model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _models.Delete(item.Info);
        }
        catch (Exception ex)
        {
            item.SetDownloadFailed("Delete failed: " + ex.Message);
            return;
        }

        if (_store.Current.SelectedModelId == item.Id)
        {
            _store.Current.SelectedModelId = null;
            _store.Save();
            _transcription.Unload();
        }

        RefreshModels();
    }

    public void LoadAudioDevices()
    {
        AudioDevices.Clear();
        AudioDevices.Add(new AudioDeviceItem(-1, "Default microphone"));
        foreach (var (number, name) in AudioCaptureService.ListDevices())
        {
            AudioDevices.Add(new AudioDeviceItem(number, name));
        }
    }

    public void OpenModelsFolder()
    {
        try
        {
            Directory.CreateDirectory(_models.ModelsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _models.ModelsDirectory,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Non-fatal.
        }
    }

    private void OnModelSelected(ModelItemViewModel item)
    {
        if (_suppressSelection || !item.IsDownloaded)
        {
            return;
        }

        _store.Current.SelectedModelId = item.Id;
        _store.Save();
        _transcription.Unload();

        _suppressSelection = true;
        foreach (var other in Models)
        {
            if (!ReferenceEquals(other, item))
            {
                other.IsSelected = false;
            }
        }

        _suppressSelection = false;
        UpdateFirstRunBanner();
    }

    private void SetPasteMethod(PasteMethod method)
    {
        _pasteMethod = method;
        _store.Current.PasteMethod = method;
        _store.Save();
        OnPropertyChanged(nameof(UseClipboard));
        OnPropertyChanged(nameof(UseTyping));
    }

    private void UpdateFirstRunBanner() =>
        ShowFirstRunBanner = Models.All(m => !m.IsDownloaded);

    partial void OnSelectedLanguageCodeChanged(string value)
    {
        _store.Current.Language = value;
        _store.Save();
    }

    partial void OnThreadsChanged(int value)
    {
        _store.Current.Threads = value;
        _store.Save();
    }

    partial void OnAppendTrailingSpaceChanged(bool value)
    {
        _store.Current.AppendTrailingSpace = value;
        _store.Save();
    }

    partial void OnRestoreClipboardChanged(bool value)
    {
        _store.Current.RestoreClipboard = value;
        _store.Save();
    }

    partial void OnFocusRestoreDelayMsChanged(int value)
    {
        _store.Current.FocusRestoreDelayMs = value;
        _store.Save();
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        _store.Current.RunAtStartup = value;
        _store.Save();
        StartupRegistrar.Apply(value);
    }

    partial void OnHotkeyVkChanged(int value)
    {
        _store.Current.HotkeyVk = value;
        _store.Save();
    }

    partial void OnSelectedDeviceNumberChanged(int value)
    {
        _store.Current.AudioDeviceNumber = value;
        _store.Save();
    }
}
