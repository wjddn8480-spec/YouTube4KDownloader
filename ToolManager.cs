using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace YouTube4KDownloader;

public sealed class ToolCacheEntry
{
    public string? ETag { get; set; }
    public DateTimeOffset? LastModified { get; set; }
}

public sealed class ToolManager
{
    private static readonly HttpClient Client = CreateClient();
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string ToolsDirectory { get; }
    public string YtDlpPath => Path.Combine(ToolsDirectory, "yt-dlp.exe");
    public string FfmpegPath => Path.Combine(ToolsDirectory, "ffmpeg.exe");
    public string FfprobePath => Path.Combine(ToolsDirectory, "ffprobe.exe");
    public string DenoPath => Path.Combine(ToolsDirectory, "deno.exe");
    private string CachePath => Path.Combine(ToolsDirectory, "update-cache.json");
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("YouTube4KDownloader/1.3.5");
        return client;
    }
    public ToolManager(string? toolsDirectory = null)
    {
        ToolsDirectory = toolsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YouTube4KDownloader", "tools");
        Directory.CreateDirectory(ToolsDirectory);
    }

    public async Task EnsureToolsAsync(IProgress<string>? progress = null, bool forceUpdate = false, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        string? stage = null;
        bool preserveStage = false;
        try
        {
            bool denoMissing = !File.Exists(DenoPath);
            // Installing Deno migrates older builds. Refresh yt-dlp too so it
            // understands the current EJS command-line options.
            bool yt = forceUpdate || !File.Exists(YtDlpPath) || denoMissing;
            bool ff = forceUpdate || !File.Exists(FfmpegPath) || !File.Exists(FfprobePath);
            bool deno = forceUpdate || denoMissing;
            if (!yt && !ff && !deno) return;
            stage = Path.Combine(ToolsDirectory, ".update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            Dictionary<string, ToolCacheEntry> cache;
            try { cache = JsonSerializer.Deserialize<Dictionary<string, ToolCacheEntry>>(File.ReadAllText(CachePath)) ?? new(); }
            catch { cache = new(); }
            var files = new Dictionary<string, string>();
            if (yt)
            {
                progress?.Report(LocalizationManager.Get("UpdatingTools"));
                var path = Path.Combine(stage, "yt-dlp.exe");
                if (await FetchAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", path, File.Exists(YtDlpPath), cache, progress, token))
                {
                    ValidateExecutable(path);
                    files[path] = YtDlpPath;
                }
            }
            if (ff)
            {
                progress?.Report(LocalizationManager.Get("UpdatingTools"));
                var zip = Path.Combine(stage, "ffmpeg.zip");
                if (await FetchAsync("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip", zip,
                    File.Exists(FfmpegPath) && File.Exists(FfprobePath), cache, progress, token))
                {
                    ExtractFfmpeg(zip, stage);
                    files[Path.Combine(stage, "ffmpeg.exe")] = FfmpegPath;
                    files[Path.Combine(stage, "ffprobe.exe")] = FfprobePath;
                }
            }
            if (deno)
            {
                progress?.Report(LocalizationManager.Get("UpdatingTools"));
                var zip = Path.Combine(stage, "deno.zip");
                if (await FetchAsync("https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip", zip,
                    File.Exists(DenoPath), cache, progress, token))
                {
                    ExtractDeno(zip, stage);
                    files[Path.Combine(stage, "deno.exe")] = DenoPath;
                }
            }
            token.ThrowIfCancellationRequested();
            // Commit synchronously: no cancellation or UI re-entry between replacement and rollback.
            try { CommitFiles(files, stage); }
            catch (AggregateException) { preserveStage = true; throw; }
            try
            {
                var temp = Path.Combine(stage, "cache.json");
                File.WriteAllText(temp, JsonSerializer.Serialize(cache));
                File.Move(temp, CachePath, true);
            }
            catch (Exception ex) { progress?.Report(LocalizationManager.Get("UpdateFailed") + ": " + ex.Message); }
            progress?.Report(files.Count == 0 ? LocalizationManager.Get("UpToDate") : LocalizationManager.Get("ToolsUpdated"));
        }
        finally
        {
            if (stage != null && !preserveStage) { try { Directory.Delete(stage, true); } catch { } }
            _gate.Release();
        }
    }

    private static async Task<bool> FetchAsync(string url, string path, bool installed,
        Dictionary<string, ToolCacheEntry> cache, IProgress<string>? progress, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (installed && cache.TryGetValue(url, out var previous))
        {
            if (previous.ETag != null) request.Headers.TryAddWithoutValidation("If-None-Match", previous.ETag);
            else request.Headers.IfModifiedSince = previous.LastModified;
        }
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            if (!installed) throw new InvalidDataException("Server returned 304 for a missing tool.");
            return false;
        }
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        if (length is <= 0 or > 536870912) throw new InvalidDataException("Invalid tool download size.");
        await using (var input = await response.Content.ReadAsStreamAsync(token))
        await using (var output = File.Create(path))
        {
            var buffer = new byte[81920]; long total = 0; int read; int last = -1;
            while ((read = await input.ReadAsync(buffer, token)) > 0)
            {
                total += read;
                if (total > 536870912) throw new InvalidDataException("Tool download exceeds size limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
                int percent = length > 0 ? (int)(total * 100 / length.Value) : -1;
                if (percent >= last + 10) { progress?.Report($"{Path.GetFileName(path)}: {percent}%"); last = percent; }
            }
            if (total < 1024 || (length.HasValue && total != length.Value)) throw new InvalidDataException("Incomplete tool download.");
        }
        cache[url] = new ToolCacheEntry { ETag = response.Headers.ETag?.ToString(), LastModified = response.Content.Headers.LastModified };
        return true;
    }

    public static void ValidateExecutable(string path)
    {
        using var input = File.OpenRead(path);
        using var reader = new BinaryReader(input);
        if (input.Length < 1024 || reader.ReadUInt16() != 0x5a4d) throw new InvalidDataException("Invalid Windows tool executable.");
        input.Position = 0x3c;
        int pe = reader.ReadInt32();
        if (pe < 64 || pe > input.Length - 6) throw new InvalidDataException("Invalid PE header offset.");
        input.Position = pe;
        if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664) throw new InvalidDataException("Expected a Windows x64 executable.");
    }

    public static void ExtractFfmpeg(string zipPath, string directory)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe" })
        {
            var candidates = zip.Entries.Where(e => e.FullName.Replace('\\', '/').EndsWith("/bin/" + name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length != 1 || candidates[0].Length < 1024 || candidates[0].Length > 536870912)
                throw new InvalidDataException("Missing, duplicate, or oversized " + name + " in FFmpeg package.");
            // Extract only the selected files into fixed paths, never archive-provided directories.
            var path = Path.Combine(directory, name);
            candidates[0].ExtractToFile(path, true);
            ValidateExecutable(path);
        }
    }

    public static void ExtractDeno(string zipPath, string directory)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var candidates = zip.Entries.Where(e =>
            string.Equals(Path.GetFileName(e.FullName.Replace('\\', '/')), "deno.exe", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length != 1 || candidates[0].Length < 1024 || candidates[0].Length > 536870912)
            throw new InvalidDataException("Missing, duplicate, or oversized deno.exe in Deno package.");
        var path = Path.Combine(directory, "deno.exe");
        candidates[0].ExtractToFile(path, true);
        ValidateExecutable(path);
    }

    public static void CommitFiles(Dictionary<string, string> files, string stage)
    {
        var changes = new List<(string Target, string? Backup)>();
        try
        {
            foreach (var (source, target) in files)
            {
                string? backup = null;
                if (File.Exists(target))
                {
                    backup = Path.Combine(stage, Guid.NewGuid() + ".backup");
                    File.Copy(target, backup);
                    File.Replace(source, target, null);
                }
                else File.Move(source, target);
                changes.Add((target, backup));
            }
        }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            foreach (var (target, backup) in changes.AsEnumerable().Reverse())
            {
                try { if (backup == null) File.Delete(target); else File.Copy(backup, target, true); }
                catch (Exception rollback) { errors.Add(rollback); }
            }
            if (errors.Count > 1) throw new AggregateException("Tool rollback incomplete. Backups retained at " + stage, errors);
            throw;
        }
    }
}
