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
