using System.Globalization;
namespace YouTube4KDownloader;

public sealed record DownloadProgress(double Percent, string Status, string Speed = "—", string Eta = "—")
{
    public static implicit operator DownloadProgress((double Percent, string Status) value) => new(value.Percent, value.Status);
    public const string Template = "download:__Y4K_PROGRESS__:%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s|%(progress.status)s";
    public static DownloadProgress? Parse(string line)
    {
        const string prefix = "__Y4K_PROGRESS__:";
        if (!line.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var fields = line[prefix.Length..].Split('|');
        if (fields.Length != 4) return null;
        var percentText = fields[0].Trim().TrimEnd('%');
        double percent = double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)
            ? Math.Clamp(n, 0, 99.9) : double.NaN;
        static string Clean(string s) => s.Trim() is "" or "NA" or "N/A" or "Unknown" or "Unknown B/s" ? "—" : s.Trim();
        bool finished = fields[3].Trim() == "finished";
        return new(percent, LocalizationManager.Get("DownloadInProgress"), finished ? "—" : Clean(fields[1]), finished ? "—" : Clean(fields[2]));
    }
}
