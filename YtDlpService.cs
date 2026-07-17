using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace YouTube4KDownloader;

public sealed class YtDlpService
{
    private readonly ToolManager _tools;
    private Process? _activeProcess;

    private static readonly Regex ProgressRegex =
        new(@"\[download\]\s+(?<percent>\d+(?:\.\d+)?)%.*?(?:at\s+(?<speed>\S+))?.*?(?:ETA\s+(?<eta>\S+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public YtDlpService(ToolManager tools)
    {
        _tools = tools;
    }

    public async Task<VideoInfo> GetInfoAsync(string url, CancellationToken token)
    {
        var args = new List<string>
        {
            "--encoding", "utf-8",
            "--dump-single-json",
            "--no-playlist",
            "--skip-download",
            "--no-warnings",
            url
        };

        var result = await RunCaptureAsync(args, token);
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;

        return new VideoInfo
        {
            Title = GetString(root, "title"),
            Uploader = GetString(root, "uploader"),
            Thumbnail = GetString(root, "thumbnail"),
            Id = GetString(root, "id"),
            Duration = root.TryGetProperty("duration", out var duration) && duration.ValueKind == JsonValueKind.Number
                ? duration.GetDouble()
                : null
        };
    }

    public async Task DownloadAsync(
        string url,
        string outputDirectory,
        int maxHeight,
        string outputFormat,
        bool playlist,
        bool subtitles,
        bool automaticSubtitles,
        bool thumbnail,
        bool embedMetadata,
        string? cookiesPath,
        IProgress<string> log,
        IProgress<(double Percent, string Status)> progress,
        CancellationToken token)
    {
        var args = new List<string>
        {
            "--encoding", "utf-8",
            "--newline",
            "--windows-filenames",
            "--continue",
            "--retries", "10",
            "--fragment-retries", "10",
            "--concurrent-fragments", "4",
            "--ffmpeg-location", _tools.ToolsDirectory,
            "--paths", outputDirectory,
            "--output", "%(title).180B [%(id)s].%(ext)s"
        };

        if (!playlist)
            args.Add("--no-playlist");
        else
        {
            args.Add("--yes-playlist");
            args.Add("--output");
            args.Add("%(playlist_title,Unknown Playlist)s/%(playlist_index)03d - %(title).170B [%(id)s].%(ext)s");
        }

        if (!string.IsNullOrWhiteSpace(cookiesPath))
        {
            args.Add("--cookies");
            args.Add(cookiesPath);
        }

        if (subtitles)
        {
            args.Add("--write-subs");
            args.Add("--sub-langs");
            args.Add("ko.*,en.*");
            args.Add("--convert-subs");
            args.Add("srt");
        }

        if (automaticSubtitles)
        {
            args.Add("--write-auto-subs");
            args.Add("--sub-langs");
            args.Add("ko.*,en.*");
            args.Add("--convert-subs");
            args.Add("srt");
        }

        if (thumbnail)
        {
            args.Add("--write-thumbnail");
            args.Add("--convert-thumbnails");
            args.Add("jpg");
        }

        if (embedMetadata)
        {
            args.Add("--embed-metadata");
            args.Add("--embed-chapters");
        }

        if (outputFormat is "mp3" or "m4a")
        {
            args.Add("--extract-audio");
            args.Add("--audio-format");
            args.Add(outputFormat);
            args.Add("--audio-quality");
            args.Add("0");
        }
        else
        {
            var formatSelector =
                $"bestvideo[height<={maxHeight}]+bestaudio/best[height<={maxHeight}]";
            args.Add("--format");
            args.Add(formatSelector);
            args.Add("--merge-output-format");
            args.Add(outputFormat);

            if (outputFormat == "mp4")
            {
                args.Add("--remux-video");
                args.Add("mp4");
            }
        }

        args.Add(url);

        var psi = CreateStartInfo(args);
        _activeProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _activeProcess.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            log.Report(e.Data);
            ParseProgress(e.Data, progress);
        };

        _activeProcess.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            log.Report(e.Data);
            ParseProgress(e.Data, progress);
        };

        _activeProcess.Start();
        _activeProcess.BeginOutputReadLine();
        _activeProcess.BeginErrorReadLine();

        using var registration = token.Register(Cancel);

        await _activeProcess.WaitForExitAsync(token);

        if (_activeProcess.ExitCode != 0)
            throw new InvalidOperationException($"yt-dlp가 종료 코드 {_activeProcess.ExitCode}로 실패했습니다.");

        progress.Report((100, LocalizationManager.Get("Completed")));
        _activeProcess.Dispose();
        _activeProcess = null;
    }

    public void Cancel()
    {
        try
        {
            if (_activeProcess is { HasExited: false })
                _activeProcess.Kill(true);
        }
        catch
        {
            // Process may already be exiting.
        }
    }

    private async Task<string> RunCaptureAsync(List<string> args, CancellationToken token)
    {
        var psi = CreateStartInfo(args);
        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(token);
        var errorTask = process.StandardError.ReadToEndAsync(token);

        await process.WaitForExitAsync(token);
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);

        return output;
    }

    private ProcessStartInfo CreateStartInfo(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _tools.YtDlpPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };

        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["LANG"] = "ko_KR.UTF-8";
        psi.Environment["LC_ALL"] = "ko_KR.UTF-8";

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        return psi;
    }

    private static void ParseProgress(string line, IProgress<(double Percent, string Status)> progress)
    {
        var match = ProgressRegex.Match(line);
        if (!match.Success)
            return;

        if (!double.TryParse(match.Groups["percent"].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var percent))
            return;

        var speed = match.Groups["speed"].Success ? match.Groups["speed"].Value : "";
        var eta = match.Groups["eta"].Success ? match.Groups["eta"].Value : "";
        var status = $"{LocalizationManager.Get("DownloadInProgress")} {percent:0.0}%";

        if (!string.IsNullOrWhiteSpace(speed))
            status += $" · {speed}";
        if (!string.IsNullOrWhiteSpace(eta))
            status += $" · {LocalizationManager.Get("RemainingTime")} {eta}";

        progress.Report((percent, status));
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }
}
