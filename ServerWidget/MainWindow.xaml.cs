using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Point = System.Windows.Point;

namespace ServerWidget;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ObservableCollection<ServerViewModel> _viewModels = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _flashTimer;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _appIcon;
    private bool _isFlashState;
    private bool _shouldFlash;
    private Popup? _activePopup;

    public MainWindow()
    {
        InitializeComponent();

        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconPath));
            }
            catch { }
        }

        _settings = AppSettings.Load();
        Opacity = _settings.Opacity;
        Topmost = _settings.AlwaysOnTop;

        ServersContainer.ItemsSource = _viewModels;
        SyncViewModels();

        _appIcon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "Server Status Monitor",
            Visible = true
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show Widget", null, (_, _) => RestoreWindow());
        trayMenu.Items.Add("Settings", null, (_, _) => OpenSettings());
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("Quit", null, (_, _) => Application.Current.Shutdown());
        _notifyIcon.ContextMenuStrip = trayMenu;

        _notifyIcon.DoubleClick += (_, _) => RestoreWindow();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += async (_, _) => await RefreshDataAsync();
        _timer.Start();

        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _flashTimer.Tick += FlashingTimer_Tick;

        Loaded += async (_, _) => await RefreshDataAsync();
        Unloaded += MainWindow_Unloaded;
    }

    private void SyncViewModels()
    {
        _viewModels.Clear();
        foreach (var server in _settings.Servers)
        {
            _viewModels.Add(new ServerViewModel(server));
        }
    }

    private async Task RefreshDataAsync()
    {
        bool hasWarning = false;

        foreach (var vm in _viewModels)
        {
            var (isOnline, queueCount, serverLoad, errorDetails) = await WhmService.CheckQueueAsync(vm.Config);
            vm.UpdateStatus(isOnline, queueCount, serverLoad, errorDetails, _settings.CustomAudioPath, _settings.EnableSoundAlerts);

            if (!isOnline || queueCount >= 1000 || serverLoad >= 10.0)
            {
                hasWarning = true;
            }
        }

        _shouldFlash = hasWarning;
        if (_shouldFlash)
        {
            if (!_flashTimer.IsEnabled) _flashTimer.Start();

            if (_settings.MaximizeOnWorkspaceError && (WindowState == WindowState.Minimized || Visibility == Visibility.Hidden))
            {
                RestoreWindow();
            }
        }
        else
        {
            _flashTimer.Stop();
            _notifyIcon.Icon = _appIcon;
        }
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        ShowCustomMenu(e, null);
    }

    private void ServerBar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement border && border.DataContext is ServerViewModel vm)
        {
            ShowCustomMenu(e, vm);
        }
    }

    private void ShowCustomMenu(MouseButtonEventArgs e, ServerViewModel? vm)
    {
        if (_activePopup != null)
        {
            _activePopup.IsOpen = false;
            _activePopup = null;
        }

        var popup = new Popup
        {
            AllowsTransparency = true,
            StaysOpen = false,
            Placement = PlacementMode.Mouse,
            IsOpen = true
        };

        var menuPanel = new StackPanel
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x26)),
            Width = 160
        };

        var borderContainer = new Border
        {
            Background = menuPanel.Background,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0x3F, 0x46)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(1),
            Child = menuPanel
        };

        popup.Child = borderContainer;
        _activePopup = popup;

        // 1. Minimize to Tray
        AddMenuButton(menuPanel, "Minimize to Tray", () => { popup.IsOpen = false; Hide(); });

        // 2. Settings
        AddMenuButton(menuPanel, "Settings", () => { popup.IsOpen = false; OpenSettings(); });

        if (vm != null)
        {
            AddSeparator(menuPanel);

            // 3. Mute Alarm with Submenu
            AddSubmenuButton(menuPanel, "Mute Alarm", popup, subPanel =>
            {
                var times = new (string Label, string Tag)[]
                {
                    ("1 minute", "1"),
                    ("10 minutes", "10"),
                    ("30 minutes", "30"),
                    ("1 hour", "60"),
                    ("3 hours", "180"),
                    ("5 hours", "300"),
                    ("24 hours", "1440"),
                    ("Always", "Always")
                };

                foreach (var (label, tag) in times)
                {
                    if (label == "Always") AddSeparator(subPanel);

                    string t = tag;
                    AddMenuButton(subPanel, label, () =>
                    {
                        popup.IsOpen = false;
                        if (t == "Always")
                        {
                            vm.Config.MuteUntil = DateTime.MaxValue;
                        }
                        else if (int.TryParse(t, out int mins))
                        {
                            vm.Config.MuteUntil = DateTime.Now.AddMinutes(mins);
                        }
                        _settings.Save();
                    });
                }
            });

            // 4. Unmute
            AddMenuButton(menuPanel, "Unmute", () =>
            {
                popup.IsOpen = false;
                vm.Config.MuteUntil = null;
                _settings.Save();
            });
        }

        AddSeparator(menuPanel);

        // 5. Quit
        AddMenuButton(menuPanel, "Quit", () => { popup.IsOpen = false; Application.Current.Shutdown(); });

        e.Handled = true;
    }

    private void AddMenuButton(StackPanel panel, string text, Action onClick)
    {
        var btn = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(10, 6, 10, 6),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 12
            }
        };

        btn.MouseEnter += (_, _) => btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0x3F, 0x46));
        btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
        btn.MouseLeftButtonUp += (_, _) => onClick();

        panel.Children.Add(btn);
    }

    private void AddSubmenuButton(StackPanel panel, string text, Popup parentPopup, Action<StackPanel> populateSub)
    {
        Popup? subPopup = null;

        var subPanel = new StackPanel
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x26)),
            Width = 140
        };

        var subBorder = new Border
        {
            Background = subPanel.Background,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0x3F, 0x46)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(1),
            Child = subPanel
        };

        populateSub(subPanel);

        var btn = new Grid
        {
            Background = Brushes.Transparent,
            Height = 28
        };

        var txt = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var arrow = new TextBlock
        {
            Text = "▶",
            Foreground = Brushes.White,
            FontSize = 9,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        btn.Children.Add(txt);
        btn.Children.Add(arrow);

        var containerBorder = new Border { Child = btn };

        containerBorder.MouseEnter += (_, _) =>
        {
            containerBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0x3F, 0x46));
            if (subPopup == null)
            {
                subPopup = new Popup
                {
                    AllowsTransparency = true,
                    StaysOpen = false,
                    PlacementTarget = containerBorder,
                    Placement = PlacementMode.Right,
                    IsOpen = true
                };
                subPopup.Child = subBorder;
            }
            else
            {
                subPopup.IsOpen = true;
            }
        };

        containerBorder.MouseLeave += (_, _) =>
        {
            containerBorder.Background = Brushes.Transparent;
        };

        panel.Children.Add(containerBorder);
    }

    private void AddSeparator(StackPanel panel)
    {
        panel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0x3F, 0x46)),
            Margin = new Thickness(4, 2, 4, 2)
        });
    }

    private void FlashingTimer_Tick(object? sender, EventArgs e)
    {
        if (!_shouldFlash) return;

        _isFlashState = !_isFlashState;
        _notifyIcon.Icon = _isFlashState ? SystemIcons.Warning : _appIcon;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private async void OpenSettings()
    {
        var settingsWin = new SettingsWindow(_settings) { Owner = this };
        if (settingsWin.ShowDialog() == true)
        {
            Opacity = _settings.Opacity;
            Topmost = _settings.AlwaysOnTop;
            SyncViewModels();
            await RefreshDataAsync();
        }
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}