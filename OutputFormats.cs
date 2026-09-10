namespace YouTube4KDownloader;

public static class OutputFormats
{
    public const string Mp4 = "mp4";
    public const string Premiere = "mp4_premiere";
    public static bool NeedsPremiereEncoding(string format) => format == Premiere;
    public static string DisplayName(string format) => format switch
    {
        Mp4 => "MP4", Premiere => "MP4 (Premiere Pro)", "mp3" => "MP3", "m4a" => "M4A", _ => format
    };

    public static IEnumerable<string> DownloadArguments(string format, int height)
    {
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        return format switch
        {
            // Preserve native MP4 video and M4A audio; never invoke a video encoder.
            Mp4 => new[] { "--format", $"bestvideo[ext=mp4][height<={height}]+bestaudio[ext=m4a]/best[ext=mp4][height<={height}]", "--merge-output-format", "mp4", "--remux-video", "mp4" },
            Premiere => new[] { "--format", $"bestvideo[height<={height}]+bestaudio/best[height<={height}]", "--merge-output-format", "mkv" },
            "mp3" or "m4a" => new[] { "--extract-audio", "--audio-format", format, "--audio-quality", "0" },
            _ => throw new ArgumentException("Unknown output format.", nameof(format))
        };
    }
}
