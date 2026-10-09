using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Say.ViewModels;

namespace Say.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private bool _capturingHotkey;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        HotkeyCaptureButton.Content = vm.HotkeyDisplay;
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ModelItemViewModel item)
        {
            _vm.StartDownload(item);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ModelItemViewModel item)
        {
            _vm.CancelDownload(item);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ModelItemViewModel item)
        {
            _vm.DeleteModel(item);
        }
    }

    private void HotkeyCapture_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = HotkeyCaptureButton.IsChecked == true;
        HotkeyCaptureButton.Content = _capturingHotkey ? "Press a key…" : _vm.HotkeyDisplay;
        if (_capturingHotkey)
        {
            Keyboard.Focus(this);
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_capturingHotkey)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key is Key.LeftShift or Key.RightShift
                or Key.LeftCtrl or Key.RightCtrl
                or Key.LeftAlt or Key.RightAlt
                or Key.LWin or Key.RWin
                or Key.System or Key.None)
            {
                e.Handled = true;
                return;
            }

            _vm.SetHotkey(key);
            _capturingHotkey = false;
            HotkeyCaptureButton.IsChecked = false;
            HotkeyCaptureButton.Content = _vm.HotkeyDisplay;
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        _vm.LoadAudioDevices();
    }

    private void OpenModelsFolder_Click(object sender, RoutedEventArgs e)
    {
        _vm.OpenModelsFolder();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
