using System.Globalization;
using System.Text.RegularExpressions;

namespace YouTube4KDownloader;

public sealed record ClipSelection
{
    public double Start { get; }
    public double End { get; }
    public ClipSelection(double start, double end)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start)
            throw new ArgumentException(LocalizationManager.Get("RangeOrder"));
        Start = start; End = end;
    }
    public static double ParseTime(string value)
    {
        value = value.Trim();
        if (!Regex.IsMatch(value, @"^\d+(?::\d{1,2}){0,2}(?:\.\d{1,3})?$"))
            throw new FormatException(LocalizationManager.Get("TimeFormat"));
        var parts = value.Split(':').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        for (int i = 1; i < parts.Length; i++)
            if (parts[i] >= 60) throw new FormatException(LocalizationManager.Get("TimeComponent"));
        double seconds = parts.Aggregate(0d, (n, p) => n * 60 + p);
        if (!double.IsFinite(seconds)) throw new FormatException(LocalizationManager.Get("TimeFormat"));
        return seconds;
    }
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    public string Range => $"*{Number(Start)}-{Number(End)}";
    public string Description => $"{Number(Start)}–{Number(End)}s ({LocalizationManager.Get("FastCut")})";
    public IEnumerable<string> Arguments => new[] { "--download-sections", Range,
        "--no-force-keyframes-at-cuts" };
    public void ValidateDuration(double? duration)
    {
        if (duration is > 0 && (Start >= duration.Value || End > duration.Value + 0.001))
            throw new ArgumentException(LocalizationManager.Get("RangeDuration"));
    }
    public string OutputTemplate(string template, string format)
    {
        if (string.IsNullOrWhiteSpace(template)) template = "%(title).180B [%(id)s].%(ext)s";
        var suffix = $" [clip-{Number(Start)}-{Number(End)}-fast-{format}]";
        const string ext = ".%(ext)s";
        return template.EndsWith(ext, StringComparison.Ordinal) ? template[..^ext.Length] + suffix + ext : template + suffix + ext;
    }
}
