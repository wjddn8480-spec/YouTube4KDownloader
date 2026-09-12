using System.IO;
using System.Diagnostics;
using System.Globalization;
namespace YouTube4KDownloader;

// Estimates describe the current video, including its pending post-processing.
public sealed class CompletionEstimator : IDisposable
{
    private readonly object gate = new();
    private readonly IProgress<DownloadProgress> output;
    private readonly bool premiere;
    private readonly double rate;
    private readonly TimingHistory history;
    private readonly string profile;
    private readonly string primaryPost;
    private string stageKey = "";
    private bool stageHasProgress;
    private double lastDone, lastAdvance, downloadPercent = -1, smoothedEta = double.NaN;
    private readonly SmoothedProcessingRate smoother = new();
    private readonly Stopwatch downloadClock = Stopwatch.StartNew();
    private double lastDownloadTime;
    private double EncodeBudget => history.Predict("premiere:" + profile, duration, 1);

    private double duration, percent;
    private string phase = "", speed = "—", transferEta = "—";
    private bool processing, encoding;
    private DateTime stageStart;
    private readonly Stopwatch stage = new();
    public string ProgressPath { get; } = Path.Combine(Path.GetTempPath(), "y4k-progress-" + Guid.NewGuid().ToString("N") + ".txt");
    public CompletionEstimator(double? seconds, string format, IProgress<DownloadProgress> output, int maxHeight = 2160, TimingHistory? history = null, string encoder = "cpu")
    {
        duration = seconds.GetValueOrDefault(); this.output = output;
        this.history = history ?? new TimingHistory(); profile = format + ":" + maxHeight + (encoder=="cpu"?"":":"+encoder);
        primaryPost = format is "mp3" or "m4a" ? "ExtractAudio" : "Merger";
        premiere = OutputFormats.NeedsPremiereEncoding(format);
        rate = format is "mp3" or "m4a" ? 20 : 80;
    }
    public static double? Seconds(string text)
    {
        var parts = text.Trim().Split(':'); double result = 0;
        foreach (var p in parts) { if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n < 0) return null; result = result * 60 + n; }
        return double.IsFinite(result) ? result : null;
    }
    public static string Time(double? seconds) => seconds is >= 0 && double.IsFinite(seconds.Value)
        ? "≈ " + TimeSpan.FromSeconds(Math.Min(seconds.Value, 8640000)).ToString(@"d\.hh\:mm\:ss").TrimStart('0').TrimStart('.') : LocalizationManager.Get("Estimating");
    private double Budget => duration > 0 ? Math.Max(2, history.Predict("post:" + profile + ":" + primaryPost, duration, rate)) + (premiere ? EncodeBudget : 0) : double.NaN;
    public void Line(string line)
    {
        lock (gate)
        {
            if (line.StartsWith("__Y4K_DURATION__:") && Seconds(line[17..]) is double d) duration = d;
            if (line.StartsWith("__Y4K_PP__:"))
            {
                var fields = line[11..].Split('|');
                if (fields.Length >= 2 && fields[1].Trim() == "started") Begin(false, fields[0]);
                else if(fields.Length >= 2 && fields[1].Trim() == "finished") Complete();
                return;
            }
            var v = DownloadProgress.Parse(line);
            if (v == null) return;
            processing = false; phase = v.Status; speed = v.Speed; transferEta = v.Eta;
            percent = double.IsNaN(v.Percent) ? double.NaN : v.Percent * .7;
            var remaining = line.TrimEnd().EndsWith("|finished") ? 0 : Seconds(v.Eta);
            double now = downloadClock.Elapsed.TotalSeconds;
            if (remaining.HasValue)
            {
                if (v.Percent < downloadPercent - 1 || !double.IsFinite(smoothedEta) || remaining==0) smoothedEta=remaining.Value;
                else { double predicted=Math.Max(0,smoothedEta-(now-lastDownloadTime)); smoothedEta=.7*predicted+.3*remaining.Value; }
                remaining=smoothedEta;
            }
            else smoothedEta=double.NaN;
            downloadPercent=v.Percent; lastDownloadTime=now;
            output.Report(new(percent, phase, speed, Time(remaining.HasValue && double.IsFinite(Budget) ? remaining + Budget : null)));
        }
    }
    public void Begin(bool isEncoding, string name)
    {
        lock (gate)
        {
            processing = true; encoding = isEncoding; stage.Restart(); stageStart = DateTime.UtcNow;
            stageKey = isEncoding ? "premiere:"+profile : "post:"+profile+":"+name;
            stageHasProgress=false; lastDone=lastAdvance=0; smoother.Reset();
            phase = isEncoding ? LocalizationManager.Get("Reencoding") : LocalizationManager.Get("Processing");
            percent = isEncoding ? 80 : 70; speed = "—";
            Tick();
        }
    }
    public void Complete()
    {
        lock(gate)
        {
            if(processing) Tick();
            // Only completed FFmpeg work is learned; skips, failures and cancellation are excluded.
            if(processing && stageHasProgress && duration>0 && lastDone>=duration*.95)
                history.Record(stageKey,duration,stage.Elapsed.TotalSeconds);
            processing=false;
        }
    }
    public void SetDuration(double? value) { lock(gate) { if(value is > 0) duration=value.Value; } }
    public void Tick()
    {
        lock (gate)
        {
            if (!processing) return;
            double done = 0, multiplier = 0;
            bool ended = false;
            try
            {
                if (File.GetLastWriteTimeUtc(ProgressPath) >= stageStart.AddMilliseconds(-250))
                {
                    using var stream = new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    // FFmpeg appends blocks. Read only the most recent tail.
                    if (stream.Length > 16384) stream.Seek(-16384, SeekOrigin.End);
                    using var reader = new StreamReader(stream);
                    foreach (var line in reader.ReadToEnd().Split('\n'))
                    {
                        var kv=line.Trim().Split('=',2); if(kv.Length!=2)continue;
                        if(kv[0]=="out_time_us" && Seconds(kv[1]) is double us) done=us/1000000;
                        if(kv[0]=="speed" && Seconds(kv[1].TrimEnd('x')) is double x) multiplier=x;
                        if(kv[0]=="progress") ended=kv[1]=="end";
                    }
                }
            } catch(IOException) { } catch(UnauthorizedAccessException) { }
            double elapsed=stage.Elapsed.TotalSeconds;
            if(done>lastDone) {lastDone=done;lastAdvance=elapsed;stageHasProgress=true;}
            multiplier=smoother.Update(done,elapsed,multiplier);
            double? remaining=null;
            if(duration>0)
            {
                var fraction=Math.Clamp(done/duration,0,1);
                percent=(encoding?80:70)+fraction*(encoding?19.9:premiere?9.9:29.9);
                double initial=history.Predict(stageKey,duration,encoding?1:rate);
                double estimate=multiplier>0 ? Math.Max(0,duration-done)/multiplier : initial-elapsed;
                // End of media timestamps can precede faststart/file flushing.
                remaining=estimate>0 ? estimate+(premiere&&!encoding?EncodeBudget:0) : null;
                if(ended || (stageHasProgress && elapsed-lastAdvance>5)) remaining=null;
            }
            if(ended) phase=encoding?LocalizationManager.Get("Finalizing"):LocalizationManager.Get("Finalizing");
            output.Report(new(percent,phase,multiplier>0?$"{multiplier:0.00}x":"—",Time(remaining)));
        }
    }
    public async Task MonitorAsync(CancellationToken token)
    {
        try { while(true) { await Task.Delay(500,token); Tick(); } } catch(OperationCanceledException) when(token.IsCancellationRequested) { }
    }
    public void Dispose() { try { File.Delete(ProgressPath); } catch { } }
}
