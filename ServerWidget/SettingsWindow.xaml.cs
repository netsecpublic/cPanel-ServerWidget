using System;
using System.IO;
using System.Media;
using System.Windows;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace ServerWidget;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }

    public SettingsWindow(AppSettings settings)
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

        Settings = settings;

        OpacitySlider.Value = Settings.Opacity;
        OpacityText.Text = $"{(int)(Settings.Opacity * 100)}%";
        AudioPathTextBox.Text = Settings.CustomAudioPath;
        SoundAlertsCheckBox.IsChecked = Settings.EnableSoundAlerts;
        AlwaysOnTopCheckBox.IsChecked = Settings.AlwaysOnTop;
        MaximizeOnErrorCheckBox.IsChecked = Settings.MaximizeOnWorkspaceError;
        ServersGrid.ItemsSource = Settings.Servers;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityText != null)
        {
            OpacityText.Text = $"{(int)(e.NewValue * 100)}%";
        }

        if (Settings != null)
        {
            Settings.Opacity = e.NewValue;
            if (Owner != null) Owner.Opacity = e.NewValue;
        }
    }

    private void BrowseAudio_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "WAV Audio Files (*.wav)|*.wav|All Files (*.*)|*.*",
            Title = "Select Offline Sound Alert"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            AudioPathTextBox.Text = openFileDialog.FileName;
        }
    }

    private void TestAudio_Click(object sender, RoutedEventArgs e)
    {
        string path = AudioPathTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                using var player = new SoundPlayer(path);
                player.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not play sound file:\n{ex.Message}", "Audio Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            SystemSounds.Hand.Play();
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        Settings.Servers.Add(new ServerConfig
        {
            Name = $"Server {Settings.Servers.Count + 1}",
            Host = "example.com/queue.php",
            Port = 443
        });
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ServersGrid.SelectedItem is ServerConfig selected)
        {
            Settings.Servers.Remove(selected);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Settings.CustomAudioPath = AudioPathTextBox.Text.Trim();
        Settings.EnableSoundAlerts = SoundAlertsCheckBox.IsChecked ?? true;
        Settings.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked ?? true;
        Settings.MaximizeOnWorkspaceError = MaximizeOnErrorCheckBox.IsChecked ?? true;
        Settings.Save();
        DialogResult = true;
        Close();
    }
}