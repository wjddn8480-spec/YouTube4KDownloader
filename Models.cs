using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YouTube4KDownloader;

public sealed class VideoInfo
{
    public string Title { get; set; } = "";
    public string Uploader { get; set; } = "";
    public string Thumbnail { get; set; } = "";
    public double? Duration { get; set; }
    public bool IsLive { get; set; }
    public string Id { get; set; } = "";
}

public sealed class DownloadQueueItem : INotifyPropertyChanged
{
    public ClipSelection? Clip { get; set; }
    public bool SectionsAssigned { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _url = "", _title = "", _status = "", _speed = "—", _eta = "—";
    private double _progress;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public string Url { get => _url; set => Set(ref _url, value); }
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Speed { get => _speed; set => Set(ref _speed, value); }
    public string Eta { get => _eta; set => Set(ref _eta, value); }
    public double Progress { get => _progress; set { Set(ref _progress, value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressText))); } }
    public string ProgressText => double.IsNaN(Progress) ? "—" : $"{Progress:0.0}%";
}

public sealed class DownloadHistoryItem
{
    public DateTime DownloadedAt { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Format { get; set; } = "";
    public string Folder { get; set; } = "";
    public bool Success { get; set; }
}

public sealed class AppSettings
{
    public string VideoEncoder { get; set; } = "cpu";
    public string OutputFormat { get; set; } = OutputFormats.Mp4;
    public bool StandardMp4DefaultApplied { get; set; }
    public bool AutoToolUpdateEnabled { get; set; } = true;
    public DateTime LastToolUpdateCheckUtc { get; set; }
    public bool AutoUpdateEnabled { get; set; } = true;
    public string OutputFolder { get; set; } = "";
    public string FileNameTemplate { get; set; } = "%(title).180B [%(id)s].%(ext)s";
    public string BrowserCookies { get; set; } = "none";
    public bool OpenFolderAfterComplete { get; set; }
    public bool AutoClipboardEnabled { get; set; } = true;
    public bool AutoQueueFromClipboard { get; set; } = true;
    public bool AutoStartAfterAdd { get; set; } = false;
    public string? ManualLanguage { get; set; }
}
