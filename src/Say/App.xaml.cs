using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using Say.Models;
using Say.Services;
using Say.ViewModels;
using Say.Views;

namespace Say;

public partial class App : Application
{
    private Mutex? _mutex;
    private SettingsStore? _settingsStore;
    private AudioCaptureService? _audio;
    private ModelManager? _models;
    private TranscriptionService? _transcription;
    private TranscriptionService? _previewTranscription;
    private TargetWindowService? _targets;
    private PasteService? _paste;
    private GlobalHotkeyService? _hotkeys;
    private DictationController? _controller;
    private OverlayViewModel? _overlayVm;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private TaskbarIcon? _tray;
    private System.Drawing.Icon? _trayIcon;
    private bool _hideOverlayAfterSession;

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            LogCrash(e.Exception);
            MessageBox.Show(
                "Say hit an unexpected error:\n\n" + e.Exception.Message,
                "Say",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash(e.ExceptionObject as Exception);
    }

    private static void LogCrash(Exception? exception)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Say");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}");
        }
        catch
        {
            // Crash logging must never throw.
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--selftest") || e.Args.Contains("--download") || e.Args.Contains("--record"))
        {
            Environment.Exit(SelfTest.Run(e.Args));
        }

        _mutex = new Mutex(true, @"Local\Say.Dictation.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Say is already running — look for it in the tray.", "Say");
            Shutdown();
            return;
        }

        _settingsStore = new SettingsStore();
        _settingsStore.Load();

        _audio = new AudioCaptureService();
        _models = new ModelManager();
        _transcription = new TranscriptionService();
        _previewTranscription = new TranscriptionService();
        _targets = new TargetWindowService();
        _paste = new PasteService(_targets);

        _overlayVm = new OverlayViewModel();
        _overlay = new OverlayWindow(_overlayVm);

        _hotkeys = new GlobalHotkeyService(
            SynchronizationContext.Current ?? new System.Windows.Threading.DispatcherSynchronizationContext(),
            () => _settingsStore.Current.HotkeyVk);

        _controller = new DictationController(
            _audio,
            _transcription,
            _previewTranscription,
            _models,
            _paste,
            _targets,
            _settingsStore,
            _hotkeys,
            _overlayVm,
            repositionOverlay: hwnd =>
            {
                // If the overlay was hidden when the session starts, it reappears for the
                // session and is hidden again when the session is completed.
                _hideOverlayAfterSession = _overlay is { IsVisible: false };
                _overlay?.Show();
                _overlay?.PositionAtBottomCenter(hwnd);
            },
            openSettings: () =>
            {
                ShowSettings();
                return Task.CompletedTask;
            },
            onSessionCompleted: () =>
            {
                if (_hideOverlayAfterSession)
                {
                    _hideOverlayAfterSession = false;
                    _overlay?.Hide();
                }
            });

        _overlay.Controller = _controller;
        _overlay.Show();

        try
        {
            _hotkeys.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The global hotkey could not be installed: " + ex.Message +
                "\n\nYou can still dictate by clicking the on-screen button.",
                "Say");
        }

        _tray = CreateTrayIcon();

        var selectedModel = WhisperModelCatalog.Find(_settingsStore.Current.SelectedModelId);
        if (selectedModel is null || !_models.IsDownloaded(selectedModel) || e.Args.Contains("--settings"))
        {
            ShowSettings();
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(
            new SettingsViewModel(_settingsStore!, _models!, _transcription!, _previewTranscription!));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private TaskbarIcon CreateTrayIcon()
    {
        var tray = new TaskbarIcon
        {
            ToolTipText = "Say — offline dictation",
        };

        var iconResource = Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/tray.ico", UriKind.Absolute));
        if (iconResource is not null)
        {
            _trayIcon = new System.Drawing.Icon(iconResource.Stream);
            tray.Icon = _trayIcon;
        }

        var menu = new ContextMenu();

        var settingsItem = new MenuItem { Header = "Settings…" };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        var toggleItem = new MenuItem { Header = "Show / hide overlay" };
        toggleItem.Click += (_, _) =>
        {
            if (_overlay is null)
            {
                return;
            }

            if (_overlay.IsVisible)
            {
                _overlay.Hide();
            }
            else
            {
                _overlay.Show();
                _overlay.PositionAtBottomCenter(IntPtr.Zero);
            }
        };
        menu.Items.Add(toggleItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        tray.ContextMenu = menu;
        tray.TrayMouseDoubleClick += (_, _) => ShowSettings();
        tray.ForceCreate();
        return tray;
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _tray = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _controller?.Dispose();
        _hotkeys?.Dispose();
        _audio?.Dispose();
        _transcription?.Dispose();
        _previewTranscription?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _trayIcon?.Dispose();
        _controller?.Dispose();
        _hotkeys?.Dispose();
        _audio?.Dispose();
        _transcription?.Dispose();
        _previewTranscription?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
