using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
    private static readonly Regex ProgressRegex = new(@"\[download\]\s+(?<percent>\d+(?:\.\d+)?)%.*?(?:at\s+(?<speed>\S+))?.*?(?:ETA\s+(?<eta>\S+))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public YtDlpService(ToolManager tools) => _tools = tools;

    public async Task<VideoInfo> GetInfoAsync(string url, CancellationToken token)
    {
        var result = await RunCaptureAsync(new()
        {
            "--encoding", "utf-8", "--dump-single-json", "--no-playlist", "--skip-download", "--no-warnings", url
        }, token);
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;
        return new VideoInfo
        {
            Title = GetString(root, "title"), Uploader = GetString(root, "uploader"),
            Thumbnail = GetString(root, "thumbnail"), Id = GetString(root, "id"),
            Duration = root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : null
        };
    }

    public async Task DownloadAsync(string url, string outputDirectory, int maxHeight, string outputFormat,
        bool playlist, bool subtitles, bool automaticSubtitles, bool thumbnail, bool embedMetadata,
        string? cookiesPath, string? browserCookies, string fileNameTemplate,
        IProgress<string> log, IProgress<(double Percent, string Status)> progress, CancellationToken token)
    {
        var downloadedFiles = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        const string finalFilePrefix = "__Y4K_FINAL_FILE__:";

        var args = new List<string>
        {
            "--encoding", "utf-8", "--newline", "--windows-filenames", "--continue",
            "--retries", "10", "--fragment-retries", "10", "--concurrent-fragments", "4",
            "--ffmpeg-location", _tools.ToolsDirectory, "--paths", outputDirectory,
            "--output", string.IsNullOrWhiteSpace(fileNameTemplate) ? "%(title).180B [%(id)s].%(ext)s" : fileNameTemplate,
            "--print", $"after_move:{finalFilePrefix}%(filepath)s"
        };
        if (!playlist) args.Add("--no-playlist");
        else { args.Add("--yes-playlist"); args.Add("--output"); args.Add("%(playlist_title,Unknown Playlist)s/%(playlist_index)03d - %(title).170B [%(id)s].%(ext)s"); }
        if (!string.IsNullOrWhiteSpace(cookiesPath)) { args.Add("--cookies"); args.Add(cookiesPath); }
        else if (!string.IsNullOrWhiteSpace(browserCookies) && browserCookies != "none") { args.Add("--cookies-from-browser"); args.Add(browserCookies); }
        if (subtitles) { args.Add("--write-subs"); args.Add("--sub-langs"); args.Add("ko.*,en.*"); args.Add("--convert-subs"); args.Add("srt"); }
        if (automaticSubtitles) { args.Add("--write-auto-subs"); args.Add("--sub-langs"); args.Add("ko.*,en.*"); args.Add("--convert-subs"); args.Add("srt"); }
        if (thumbnail) { args.Add("--write-thumbnail"); args.Add("--convert-thumbnails"); args.Add("jpg"); }
        if (embedMetadata) { args.Add("--embed-metadata"); args.Add("--embed-chapters"); }
        args.AddRange(OutputFormats.DownloadArguments(outputFormat, maxHeight));
        args.Add(url);

        var psi = CreateStartInfo(args);
        _activeProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _activeProcess.OutputDataReceived += (_, e) =>
        {
            if (TryCaptureFinalFile(e.Data, finalFilePrefix, downloadedFiles)) return;
            HandleLine(e.Data, log, progress);
        };
        _activeProcess.ErrorDataReceived += (_, e) => HandleLine(e.Data, log, progress);
        _activeProcess.Start(); _activeProcess.BeginOutputReadLine(); _activeProcess.BeginErrorReadLine();
        using var registration = token.Register(Cancel);
        await _activeProcess.WaitForExitAsync(token);
        if (_activeProcess.ExitCode != 0) throw new InvalidOperationException($"yt-dlp exit code: {_activeProcess.ExitCode}");
        _activeProcess.Dispose(); _activeProcess = null;

        if (OutputFormats.NeedsPremiereEncoding(outputFormat))
        {
            var files = downloadedFiles.Keys.Where(File.Exists).ToArray();
            if (files.Length == 0)
                throw new InvalidOperationException(LocalizationManager.Get("PremiereSourceNotFound"));

            for (var i = 0; i < files.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var status = string.Format(LocalizationManager.Get("PremiereEncoding"), i + 1, files.Length);
                progress.Report((99, status));
                log.Report(status);
                var finalPath = await EncodeForPremiereAsync(files[i], log, token);
                log.Report(string.Format(LocalizationManager.Get("PremiereEncodingDone"), finalPath));
            }
        }

        progress.Report((100, LocalizationManager.Get("Completed")));
    }

    private static bool TryCaptureFinalFile(string? line, string prefix, ConcurrentDictionary<string, byte> files)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var path = line[prefix.Length..].Trim();
        if (!string.IsNullOrWhiteSpace(path)) files.TryAdd(path, 0);
        return true;
    }

    private async Task<string> EncodeForPremiereAsync(string inputPath, IProgress<string> log, CancellationToken token)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? throw new InvalidOperationException("Invalid output directory.");
        var finalPath = Path.ChangeExtension(inputPath, ".mp4")
            ?? throw new InvalidOperationException("Could not create the Premiere Pro output path.");
        var tempPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(inputPath)}.premiere_tmp_{Guid.NewGuid():N}.mp4");

        var psi = new ProcessStartInfo
        {
            FileName = _tools.FfmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        foreach (var arg in new[]
        {
            "-hide_banner", "-y", "-i", inputPath,
            "-map", "0:v:0", "-map", "0:a:0?",
            "-map_metadata", "0", "-map_chapters", "0",
            "-c:v", "libx264", "-preset", "medium", "-crf", "18",
            "-pix_fmt", "yuv420p", "-profile:v", "high",
            "-fps_mode", "cfr",
            "-c:a", "aac", "-b:a", "320k", "-ar", "48000",
            "-movflags", "+faststart", "-max_muxing_queue_size", "4096",
            tempPath
        }) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(token);
        var errorTask = process.StandardError.ReadToEndAsync(token);
        using var registration = token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        });
        await process.WaitForExitAsync(token);
        var standardOutput = await outputTask;
        var standardError = await errorTask;

        if (process.ExitCode != 0)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            var details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
            throw new InvalidOperationException($"FFmpeg Premiere encode failed ({process.ExitCode}).{Environment.NewLine}{details}");
        }

        if (!File.Exists(tempPath))
            throw new InvalidOperationException("FFmpeg completed but the Premiere Pro output file was not created.");

        if (string.Equals(inputPath, finalPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(tempPath, finalPath, true);
        }
        else
        {
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            File.Delete(inputPath);
        }
        return finalPath;
    }

    private static void HandleLine(string? line, IProgress<string> log, IProgress<(double Percent, string Status)> progress)
    { if (string.IsNullOrWhiteSpace(line)) return; log.Report(line); ParseProgress(line, progress); }

    public void Cancel() { try { if (_activeProcess is { HasExited: false }) _activeProcess.Kill(true); } catch { } }

    private async Task<string> RunCaptureAsync(List<string> args, CancellationToken token)
    {
        var psi = CreateStartInfo(args); using var process = new Process { StartInfo = psi }; process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(token); var errorTask = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token); var output = await outputTask; var error = await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
        return output;
    }

    private ProcessStartInfo CreateStartInfo(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo { FileName = _tools.YtDlpPath, UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false) };
        psi.Environment["PYTHONUTF8"] = "1"; psi.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (var arg in args) psi.ArgumentList.Add(arg); return psi;
    }

    private static void ParseProgress(string line, IProgress<(double Percent, string Status)> progress)
    {
        var m = ProgressRegex.Match(line); if (!m.Success || !double.TryParse(m.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)) return;
        var status = $"{LocalizationManager.Get("DownloadInProgress")} {percent:0.0}%";
        if (m.Groups["speed"].Success) status += $" · {m.Groups["speed"].Value}";
        if (m.Groups["eta"].Success) status += $" · {LocalizationManager.Get("RemainingTime")} {m.Groups["eta"].Value}";
        progress.Report((percent, status));
    }
    private static string GetString(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
