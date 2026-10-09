using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Say.Interop;
using Say.Models;
using Say.Services;
using Say.ViewModels;

namespace Say.Views;

public partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _vm;

    public OverlayWindow(OverlayViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
        Closed += (_, _) => _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    public DictationController? Controller { get; set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var exStyle = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
            exStyle |= Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW;
            Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        PositionAtBottomCenter(IntPtr.Zero);
    }

    public void PositionAtBottomCenter(IntPtr anchorWindow)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var monitor = anchorWindow != IntPtr.Zero
            ? Win32.MonitorFromWindow(anchorWindow, Win32.MONITOR_DEFAULTTONEAREST)
            : Win32.MonitorFromPoint(new Win32.POINT { X = 0, Y = 0 }, Win32.MONITOR_DEFAULTTOPRIMARY);

        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var dpi = 96u;
        if (Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
        {
            dpi = dpiX;
        }

        var scale = dpi / 96.0;
        var physicalWidth = (int)Math.Ceiling(Width * scale);
        var physicalHeight = (int)Math.Ceiling(Height * scale);
        var x = info.rcWork.Left + ((info.rcWork.Right - info.rcWork.Left) - physicalWidth) / 2;
        var y = info.rcWork.Bottom - physicalHeight;

        Win32.SetWindowPos(
            hwnd, IntPtr.Zero, x, y, 0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    private void Pill_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Pill.CaptureMouse();
        Controller?.OnPressStarted();
        e.Handled = true;
    }

    private void Pill_MouseUp(object sender, MouseButtonEventArgs e)
    {
        Pill.ReleaseMouseCapture();
        Controller?.OnPressEnded();
        e.Handled = true;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OverlayViewModel.HasPreview):
                if (_vm.HasPreview)
                {
                    ShowPreview();
                }
                else
                {
                    HidePreview();
                }

                break;

            case nameof(OverlayViewModel.PreviewText):
                Dispatcher.BeginInvoke(DispatcherPriority.Background, () => PreviewScroll.ScrollToEnd());
                break;

            case nameof(OverlayViewModel.State):
                AnimatePillForState();
                UpdateGlow();
                UpdateSpinner();
                break;
        }
    }

    private void ShowPreview()
    {
        PreviewPanel.Visibility = Visibility.Visible;
        PreviewPanel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
        PreviewTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }

    private void HidePreview()
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) =>
        {
            if (!_vm.HasPreview)
            {
                PreviewPanel.Visibility = Visibility.Collapsed;
            }
        };

        PreviewPanel.BeginAnimation(OpacityProperty, fade);
        PreviewTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(12, TimeSpan.FromMilliseconds(150)));
    }

    private void AnimatePillForState()
    {
        var target = _vm.State switch
        {
            DictationState.Listening => 470.0,
            DictationState.Processing => 430.0,
            _ => 272.0,
        };

        Pill.BeginAnimation(
            WidthProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }

    private void UpdateGlow()
    {
        if (_vm.IsListening)
        {
            GlowRing.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0.25, 0.75, TimeSpan.FromMilliseconds(900))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                });
        }
        else
        {
            GlowRing.BeginAnimation(UIElement.OpacityProperty, null);
            GlowRing.Opacity = 0;
        }
    }

    private void UpdateSpinner()
    {
        if (_vm.IsProcessing)
        {
            MicRotate.BeginAnimation(
                System.Windows.Media.RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                });
        }
        else
        {
            MicRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
            MicRotate.Angle = 0;
        }
    }
}
