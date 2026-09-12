using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YouTube4KDownloader;

public sealed record ReleaseUpdate(Version Version, string Tag, Uri Package, long Size, string? Sha256);

public sealed class AppUpdateService
{
    public const string Repository = "wjddn8480-spec/YouTube4KDownloader";
    public const string AssetName = "YouTube4KDownloader.exe";
    public static Version CurrentVersion => typeof(AppUpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
    private static readonly HttpClient Client = CreateClient();
    public string? StagingDirectory { get; private set; }
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("YouTube4KDownloader/" + CurrentVersion);
        return client;
    }

    public static Version ParseVersion(string value)
    {
        if (!Regex.IsMatch(value, @"^[vV]?\d+\.\d+\.\d+(\.\d+)?$"))
            throw new InvalidDataException("Release tag must be 1.3.2 or v1.3.2 (no prerelease suffix).");
        var v = Version.Parse(value.TrimStart('v', 'V'));
        return new Version(v.Major, v.Minor, v.Build, Math.Max(0, v.Revision));
    }

    private static Uri AssetUri(JsonElement asset)
    {
        var uri = new Uri(asset.GetProperty("browser_download_url").GetString()!);
        if (uri.Scheme != "https" || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected release asset URL.");
        return uri;
    }

    public static ReleaseUpdate? ParseRelease(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString()!;
        var version = ParseVersion(tag);
        if (version <= current) return null;
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        var package = assets.SingleOrDefault(x => x.GetProperty("name").GetString() == AssetName);
        if (package.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("Latest release requires YouTube4KDownloader.exe. Attach the published EXE to GitHub Releases.");
        var size = package.GetProperty("size").GetInt64();
        if (size < 1024 || size > 512L * 1024 * 1024) throw new InvalidDataException("Invalid update size.");
        string? sha256 = null;
        if (package.TryGetProperty("digest", out var digest) && digest.ValueKind != JsonValueKind.Null)
        {
            var value = digest.GetString();
            if (!string.IsNullOrEmpty(value))
            {
                if (!Regex.IsMatch(value, @"^sha256:[a-fA-F0-9]{64}$"))
                    throw new InvalidDataException("Invalid GitHub asset digest.");
                sha256 = value[7..];
            }
        }
        return new ReleaseUpdate(version, tag, AssetUri(package), size, sha256);
    }

    public async Task<ReleaseUpdate?> CheckAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Client.SendAsync(request, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidDataException("No public latest release was found.");
        response.EnsureSuccessStatusCode();
        return ParseRelease(await response.Content.ReadAsStringAsync(timeout.Token), CurrentVersion);
    }

    public async Task PrepareAsync(ReleaseUpdate release, IProgress<string> progress, CancellationToken token)
    {
        Cleanup();
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YouTube4KDownloader", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        StagingDirectory = dir;
        try
        {
            var exe = Path.Combine(dir, AssetName);
            using (var response = await Client.GetAsync(release.Package, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(token);
                await using var output = File.Create(exe);
                byte[] buffer = new byte[81920];
                long total = 0; int read; int lastPercent = -1;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += read;
                    if (total > release.Size) throw new InvalidDataException("Update exceeds release size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                    int percent = (int)(total * 100 / release.Size);
                    if (percent != lastPercent) { progress.Report($"{release.Tag}: {percent}%"); lastPercent = percent; }
                }
                if (total != release.Size) throw new InvalidDataException("Incomplete update download.");
            }
            if (release.Sha256 is { } expected)
            {
                await using var stream = File.OpenRead(exe);
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum mismatch.");
            }
            var info = FileVersionInfo.GetVersionInfo(exe);
            var fileVersion = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            if (fileVersion != release.Version) throw new InvalidDataException("EXE version does not match the release tag.");
        }
        catch { Cleanup(); throw; }
    }

    public async Task LaunchInstallerAsync(CancellationToken token)
    {
        var dir = StagingDirectory ?? throw new InvalidOperationException("No prepared update.");
        var target = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate application.");
        if (!Path.GetFileName(target).Equals("YouTube4KDownloader.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run the published YouTube4KDownloader.exe to update.");
        // Check permissions before terminating the usable application. Never request elevation automatically.
        var probe = Path.Combine(Path.GetDirectoryName(target)!, Guid.NewGuid() + ".tmp");
        using (File.Create(probe)) { }
        File.Delete(probe);
        var script = Path.Combine(dir, "install.ps1");
        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("YouTube4KDownloader.UpdateInstaller.ps1")!)
        using (var output = File.Create(script)) resource.CopyTo(output);
        var staged = Path.Combine(dir, "YouTube4KDownloader.exe");
        string exeHash;
        using (var input = File.OpenRead(staged)) exeHash = Convert.ToHexString(SHA256.HashData(input));
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script }) info.ArgumentList.Add(arg);
        info.Environment["YTD_UPDATE_JOB"] = JsonSerializer.Serialize(new { ParentId = Environment.ProcessId, Target = target, Stage = dir, Hash = exeHash });
        using var process = Process.Start(info) ?? throw new IOException("Could not start updater.");
        try
        {
            for (int i = 0; i < 100; i++)
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(Path.Combine(dir, "ready"))) { StagingDirectory = null; return; }
                if (process.HasExited) throw new IOException("Updater exited before it was ready. Check PowerShell policy.");
                await Task.Delay(100, token);
            }
            throw new TimeoutException("Updater did not become ready. The application will remain open.");
        }
        catch
        {
            // Parent is still running, so the helper cannot yet have replaced the EXE.
            try { if (!process.HasExited) process.Kill(); } catch { }
            throw;
        }
    }

    public void Cleanup()
    {
        if (StagingDirectory is { } dir) { try { Directory.Delete(dir, true); } catch { } }
        StagingDirectory = null;
    }
}
