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
    public string OutputFolder { get; set; } = "";
    public string FileNameTemplate { get; set; } = "%(title).180B [%(id)s].%(ext)s";
    public string BrowserCookies { get; set; } = "none";
    public bool OpenFolderAfterComplete { get; set; }
    public string? ManualLanguage { get; set; }
}
