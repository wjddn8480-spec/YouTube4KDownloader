using System.IO;
using System.Text.Json;
namespace YouTube4KDownloader;

public sealed class TimingHistory
{
    public sealed record Sample(double SecondsPerMediaSecond, int Count);
    private static readonly object Gate = new();
    private readonly string path;
    public TimingHistory(string? path = null) => this.path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YouTube4KDownloader", "timing-history.json");
    private Dictionary<string, Sample> Read()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, Sample>>(File.ReadAllText(path)) ?? new(); }
        catch { return new(); }
    }
    public double Predict(string key, double duration, double fallbackRate)
    {
        lock(Gate)
        {
            var data=Read();
            return data.TryGetValue(key,out var s) && s.Count>0 && double.IsFinite(s.SecondsPerMediaSecond) && s.SecondsPerMediaSecond>0
                ? duration*s.SecondsPerMediaSecond : duration/fallbackRate;
        }
    }
    public void Record(string key, double duration, double elapsed)
    {
        if(!double.IsFinite(duration)||!double.IsFinite(elapsed)||duration<5||elapsed<.25)return;
        var ratio=elapsed/duration;
        if(ratio<.00001||ratio>10000)return;
        lock(Gate)
        {
            var data=Read();
            if(data.TryGetValue(key,out var old) && double.IsFinite(old.SecondsPerMediaSecond) && old.SecondsPerMediaSecond>0)
                data[key]=new(old.SecondsPerMediaSecond*.7+ratio*.3, Math.Min(1000,old.Count+1));
            else data[key]=new(ratio,1);
            var temp=path+".tmp";
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(temp,JsonSerializer.Serialize(data)); File.Move(temp,path,true); }
            catch { try {File.Delete(temp);}catch{} }
        }
    }
}

public sealed class SmoothedProcessingRate
{
    private double lastDone, lastElapsed, rate;
    public void Reset() {lastDone=lastElapsed=rate=0;}
    public double Update(double done,double elapsed,double reported)
    {
        if(done<lastDone) Reset();
        if(done>lastDone && elapsed-lastElapsed>=.25)
        {
            double measured=(done-lastDone)/(elapsed-lastElapsed);
            // Favor recent measured throughput without reacting to each output burst.
            rate=rate>0 ? .8*rate+.2*measured : measured;
            lastDone=done;lastElapsed=elapsed;
        }
        if(rate<=0 && reported>0 && double.IsFinite(reported))rate=reported;
        return rate;
    }
}
