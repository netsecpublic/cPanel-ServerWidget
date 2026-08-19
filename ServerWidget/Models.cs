using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media;

using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace ServerWidget;

public class ServerConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Server";
    public string Host { get; set; } = "example.com/queue.php";
    public int Port { get; set; } = 443;
    public string ApiToken { get; set; } = "";

    // Mute tracking (stores expiration time, null if not muted)
    public DateTime? MuteUntil { get; set; } = null;
}

public class AppSettings
{
    public double Opacity { get; set; } = 0.9;
    public bool EnableSoundAlerts { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool MaximizeOnWorkspaceError { get; set; } = true;
    public string CustomAudioPath { get; set; } = "";
    public ObservableCollection<ServerConfig> Servers { get; set; } = new();

    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? GetDefaults();
            }
            catch { }
        }
        return GetDefaults();
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    private static AppSettings GetDefaults()
    {
        var settings = new AppSettings();
        settings.Servers.Add(new ServerConfig { Name = "cp3", Host = "example.com/queue.php", Port = 443, ApiToken = "secret123" });
        settings.Servers.Add(new ServerConfig { Name = "cp5", Host = "example.com/queue.php", Port = 443, ApiToken = "secret123" });
        return settings;
    }
}

public class ServerViewModel : INotifyPropertyChanged
{
    private string _name = "";
    private string _queueText = "Queue: --";
    private string _loadText = "Load: --";
    private bool _isOnline;
    private Brush _barBackground = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
    private Brush _statusColor = Brushes.Gray;

    public ServerConfig Config { get; }

    public ServerViewModel(ServerConfig config)
    {
        Config = config;
        Name = config.Name;
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string QueueText
    {
        get => _queueText;
        set { _queueText = value; OnPropertyChanged(); }
    }

    public string LoadText
    {
        get => _loadText;
        set { _loadText = value; OnPropertyChanged(); }
    }

    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; OnPropertyChanged(); }
    }

    public Brush BarBackground
    {
        get => _barBackground;
        set { _barBackground = value; OnPropertyChanged(); }
    }

    public Brush StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    public void UpdateStatus(bool isOnline, int queueCount, double serverLoad, string errorDetails = "", string customAudioPath = "", bool enableSoundAlerts = true)
    {
        IsOnline = isOnline;
        bool isMuted = Config.MuteUntil.HasValue && Config.MuteUntil.Value > DateTime.Now;

        if (!isOnline)
        {
            QueueText = string.IsNullOrWhiteSpace(errorDetails) ? "Offline" : errorDetails;
            LoadText = "";
            StatusColor = Brushes.Red;
            BarBackground = new SolidColorBrush(Color.FromRgb(0x7A, 0x1C, 0x1C));

            if (enableSoundAlerts && !isMuted)
            {
                PlayOfflineAlert(customAudioPath);
            }
        }
        else
        {
            QueueText = queueCount == -1 ? "Q: --" : $"Q: {queueCount}";
            LoadText = $"Load: {serverLoad:0.00}";

            if (queueCount >= 1000 || serverLoad >= 10.0)
            {
                StatusColor = Brushes.Orange;
                BarBackground = new SolidColorBrush(Color.FromRgb(0x8C, 0x4A, 0x00));

                if (enableSoundAlerts && !isMuted)
                {
                    PlayOfflineAlert(customAudioPath);
                }
            }
            else
            {
                StatusColor = Brushes.LimeGreen;
                BarBackground = new SolidColorBrush(Color.FromRgb(0x1B, 0x4D, 0x1F));
            }
        }
    }

    private static void PlayOfflineAlert(string customAudioPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(customAudioPath) && File.Exists(customAudioPath))
            {
                using var player = new System.Media.SoundPlayer(customAudioPath);
                player.Play();
            }
            else
            {
                System.Media.SystemSounds.Hand.Play();
            }
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}