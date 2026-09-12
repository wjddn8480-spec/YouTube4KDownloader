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
            IsLive = root.TryGetProperty("is_live", out var live) && live.ValueKind == JsonValueKind.True,
            Duration = root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : null
        };
    }

    public async Task DownloadAsync(string url, string outputDirectory, int maxHeight, string outputFormat,
        bool playlist, bool subtitles, bool automaticSubtitles, bool thumbnail, bool embedMetadata,
        string? cookiesPath, string? browserCookies, string fileNameTemplate,
        IProgress<string> log, IProgress<DownloadProgress> progress, CancellationToken token, ClipSelection? clip = null, double? sourceDuration = null, string encoder = "cpu")
    {
        if (OutputFormats.NeedsPremiereEncoding(outputFormat)) await CheckEncoderAsync(encoder, token);
        if (clip != null)
        {
            if (playlist) throw new ArgumentException(LocalizationManager.Get("IndividualOnly"));
            var sourceInfo = await GetInfoAsync(url, token);
            if (sourceInfo.IsLive) throw new ArgumentException(LocalizationManager.Get("LiveUnsupported"));
            clip.ValidateDuration(sourceInfo.Duration);
            fileNameTemplate = clip.OutputTemplate(fileNameTemplate, outputFormat);
            subtitles = automaticSubtitles = false;
            log.Report(LocalizationManager.Get("Section") + ": " + clip.Description);
        }
        using var estimator = new CompletionEstimator(clip == null ? sourceDuration : clip.End - clip.Start, outputFormat, progress, maxHeight, encoder: encoder);
        using var monitorStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var monitor = estimator.MonitorAsync(monitorStop.Token);
        try
        {
        var downloadedFiles = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        const string finalFilePrefix = "__Y4K_FINAL_FILE__:";

        var args = new List<string>
        {
            "--encoding", "utf-8", "--newline", "--progress", "--no-colors",
            "--progress-delta", "0.2", "--progress-template", DownloadProgress.Template, "--windows-filenames", "--continue",
            "--retries", "10", "--fragment-retries", "10", "--concurrent-fragments", "4",
            "--ffmpeg-location", _tools.ToolsDirectory, "--paths", outputDirectory,
            "--output", string.IsNullOrWhiteSpace(fileNameTemplate) ? "%(title).180B [%(id)s].%(ext)s" : fileNameTemplate,
            "--progress-template", "postprocess:__Y4K_PP__:%(progress.postprocessor)s|%(progress.status)s",
            "--postprocessor-args", $"ffmpeg:-progress \"{estimator.ProgressPath}\" -stats_period 0.5",
            "--print", $"after_move:{finalFilePrefix}%(filepath)s"
        };
        if (clip == null) { args.Add("--print"); args.Add("before_dl:__Y4K_DURATION__:%(duration)s"); }
        if (!playlist) args.Add("--no-playlist");
        else { args.Add("--yes-playlist"); args.Add("--output"); args.Add("%(playlist_title,Unknown Playlist)s/%(playlist_index)03d - %(title).170B [%(id)s].%(ext)s"); }
        if (!string.IsNullOrWhiteSpace(cookiesPath)) { args.Add("--cookies"); args.Add(cookiesPath); }
        else if (!string.IsNullOrWhiteSpace(browserCookies) && browserCookies != "none") { args.Add("--cookies-from-browser"); args.Add(browserCookies); }
        if (subtitles) { args.Add("--write-subs"); args.Add("--sub-langs"); args.Add("ko.*,en.*"); args.Add("--convert-subs"); args.Add("srt"); }
        if (automaticSubtitles) { args.Add("--write-auto-subs"); args.Add("--sub-langs"); args.Add("ko.*,en.*"); args.Add("--convert-subs"); args.Add("srt"); }
        if (thumbnail) { args.Add("--write-thumbnail"); args.Add("--convert-thumbnails"); args.Add("jpg"); }
        if (embedMetadata) { args.Add("--embed-metadata"); if (clip == null) args.Add("--embed-chapters"); }
        args.AddRange(OutputFormats.DownloadArguments(outputFormat, maxHeight));
        if (clip != null) {
            args.AddRange(clip.Arguments);
            args.Add("--downloader-args"); args.Add($"ffmpeg:-progress \"{estimator.ProgressPath}\" -stats_period 0.5");
            estimator.Begin(false, LocalizationManager.Get("Section"));
        }
        args.Add(url);

        var psi = CreateStartInfo(args);
        _activeProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _activeProcess.OutputDataReceived += (_, e) =>
        {
            if (TryCaptureFinalFile(e.Data, finalFilePrefix, downloadedFiles)) return;
            if (!string.IsNullOrWhiteSpace(e.Data)) { log.Report(e.Data); estimator.Line(e.Data); }
        };
        _activeProcess.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) { log.Report(e.Data); estimator.Line(e.Data); } };
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
                estimator.SetDuration(await ProbeDurationAsync(files[i], token));
                estimator.Begin(true, "Premiere");
                log.Report(status);
                var finalPath = await EncodeForPremiereAsync(files[i], log, token, estimator, encoder);
                estimator.Tick(); estimator.Complete();
                log.Report(string.Format(LocalizationManager.Get("PremiereEncodingDone"), finalPath));
            }
        }

        }
        finally { monitorStop.Cancel(); await monitor; }
        progress.Report((100, LocalizationManager.Get("Completed")));
    }

    private readonly HashSet<string> checkedEncoders = new();
    private async Task CheckEncoderAsync(string encoder, CancellationToken token)
    {
        var options = VideoEncoders.Arguments(encoder);
        if(encoder=="cpu" || checkedEncoders.Contains(encoder))return;
        var args = new List<string>{"-hide_banner","-v","error","-f","lavfi","-i","color=size=640x360:rate=1","-frames:v","1","-pix_fmt","yuv420p"};
        args.AddRange(options); args.AddRange(new[]{"-f","null","-"});
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try { await RunToolAsync(_tools.FfmpegPath,args,timeout.Token); }
        catch(OperationCanceledException ex) when(!token.IsCancellationRequested) {throw new InvalidOperationException(LocalizationManager.Get("GpuUnavailable"),ex);}
        catch(Exception ex) when(ex is not OperationCanceledException) { throw new InvalidOperationException(LocalizationManager.Get("GpuUnavailable")+" ("+encoder+")\n"+ex.Message,ex); }
        checkedEncoders.Add(encoder);
    }
    private static async Task RunToolAsync(string file,IEnumerable<string> args,CancellationToken token)
    {
        var psi=new ProcessStartInfo{FileName=file,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)psi.ArgumentList.Add(arg);
        using var process=new Process{StartInfo=psi};process.Start();
        using var reg=token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch{}});
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(token);await stdout;var error=await stderr;
        token.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new InvalidOperationException(error.Length>3000?error[^3000..]:error);
    }

    private static bool TryCaptureFinalFile(string? line, string prefix, ConcurrentDictionary<string, byte> files)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var path = line[prefix.Length..].Trim();
        if (!string.IsNullOrWhiteSpace(path)) files.TryAdd(path, 0);
        return true;
    }

    private async Task<double?> ProbeDurationAsync(string path, CancellationToken token)
    {
        if (!File.Exists(_tools.FfprobePath)) return null;
        var psi = new ProcessStartInfo { FileName = _tools.FfprobePath, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach(var arg in new[]{"-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path}) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi }; process.Start();
        using var registration=token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch{}});
        var stdout=process.StandardOutput.ReadToEndAsync(token); var stderr=process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token); var text=await stdout; await stderr;
        return process.ExitCode==0 ? CompletionEstimator.Seconds(text.Trim()) : null;
    }

    private async Task<string> EncodeForPremiereAsync(string inputPath, IProgress<string> log, CancellationToken token, CompletionEstimator estimator, string encoder)
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
            "-hide_banner", "-y", "-progress", estimator.ProgressPath, "-stats_period", "0.5", "-i", inputPath,
            "-map", "0:v:0", "-map", "0:a:0?",
            "-map_metadata", "0", "-map_chapters", "0",
            "-pix_fmt", "yuv420p", "-profile:v", "high",
            "-fps_mode", "cfr",
            "-c:a", "aac", "-b:a", "320k", "-ar", "48000",
            "-movflags", "+faststart", "-max_muxing_queue_size", "4096",
        }) psi.ArgumentList.Add(arg);
        foreach(var arg in VideoEncoders.Arguments(encoder)) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(tempPath);

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

    private static void HandleLine(string? line, IProgress<string> log, IProgress<DownloadProgress> progress)
    { if (string.IsNullOrWhiteSpace(line)) return; log.Report(line); ParseProgress(line, progress); }

    public void Cancel() { try { if (_activeProcess is { HasExited: false }) _activeProcess.Kill(true); } catch { } }

    private async Task<string> RunCaptureAsync(List<string> args, CancellationToken token)
    {
        var psi = CreateStartInfo(args); using var process = new Process { StartInfo = psi }; process.Start();
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
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

    private static void ParseProgress(string line, IProgress<DownloadProgress> progress)
    {
        var value = DownloadProgress.Parse(line);
        if (value != null) progress.Report(value);
    }
    private static string GetString(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
