using System;

namespace YouTube4KDownloader;

public sealed class VideoInfo
{
    public string Title { get; set; } = "";
    public string Uploader { get; set; } = "";
    public string Thumbnail { get; set; } = "";
    public double? Duration { get; set; }
    public string Id { get; set; } = "";
}

public sealed class DownloadQueueItem
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public double Progress { get; set; }
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
