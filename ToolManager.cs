using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace YouTube4KDownloader;

public sealed class ToolManager
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(20)
    };

    public string ToolsDirectory { get; }
    public string YtDlpPath => Path.Combine(ToolsDirectory, "yt-dlp.exe");
    public string FfmpegPath => Path.Combine(ToolsDirectory, "ffmpeg.exe");
    public string FfprobePath => Path.Combine(ToolsDirectory, "ffprobe.exe");

    public ToolManager()
    {
        ToolsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YouTube4KDownloader",
            "tools");
        Directory.CreateDirectory(ToolsDirectory);
    }

    public async Task EnsureToolsAsync(IProgress<string>? progress = null, bool forceUpdate = false)
    {
        if (forceUpdate || !File.Exists(YtDlpPath))
        {
            progress?.Report(LocalizationManager.Get("DownloadingYtDlp"));
            await DownloadFileAsync(
                "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
                YtDlpPath);
        }

        if (forceUpdate || !File.Exists(FfmpegPath) || !File.Exists(FfprobePath))
        {
            progress?.Report(LocalizationManager.Get("DownloadingFfmpeg"));
            await DownloadAndExtractFfmpegAsync();
        }

        progress?.Report(LocalizationManager.Get("ToolsReady"));
    }

    private static async Task DownloadFileAsync(string url, string destination)
    {
        var temp = destination + ".download";
        if (File.Exists(temp))
            File.Delete(temp);

        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using (var input = await response.Content.ReadAsStreamAsync())
        await using (var output = File.Create(temp))
        {
            await input.CopyToAsync(output);
        }

        File.Move(temp, destination, true);
    }

    private async Task DownloadAndExtractFfmpegAsync()
    {
        var zipPath = Path.Combine(ToolsDirectory, "ffmpeg.zip");
        var extractPath = Path.Combine(ToolsDirectory, "ffmpeg_extract");

        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);

        await DownloadFileAsync(
            "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            zipPath);

        Directory.CreateDirectory(extractPath);
        ZipFile.ExtractToDirectory(zipPath, extractPath, true);

        var ffmpeg = Directory.EnumerateFiles(extractPath, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
        var ffprobe = Directory.EnumerateFiles(extractPath, "ffprobe.exe", SearchOption.AllDirectories).FirstOrDefault();

        if (ffmpeg is null || ffprobe is null)
            throw new InvalidOperationException("FFmpeg 압축파일에서 실행 파일을 찾지 못했습니다.");

        File.Copy(ffmpeg, FfmpegPath, true);
        File.Copy(ffprobe, FfprobePath, true);

        File.Delete(zipPath);
        Directory.Delete(extractPath, true);
    }
}
