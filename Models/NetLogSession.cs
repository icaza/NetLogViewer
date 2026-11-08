namespace NetLogViewer.Models;

/// <summary>
/// Contains the complete parsed session for a NetLog file.
/// </summary>
public sealed class NetLogSession
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public long FileSizeBytes { get; init; }
    public TimeSpan ParseDuration { get; set; }
    public NetLogConstants Constants { get; init; } = new();

    public List<NetLogSource> Sources { get; init; } = [];
    public Dictionary<int, NetLogSource> SourceMap { get; init; } = [];

    public int TotalEventsCount { get; set; }
    public int ErrorSourcesCount => Sources.Count(s => s.HasError);

    public string FileSizeFormatted
    {
        get
        {
            if (FileSizeBytes >= 1024 * 1024 * 1024)
                return $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB";
            if (FileSizeBytes >= 1024 * 1024)
                return $"{FileSizeBytes / (1024.0 * 1024):F1} MB";
            if (FileSizeBytes >= 1024)
                return $"{FileSizeBytes / 1024.0:F1} KB";
            return $"{FileSizeBytes} B";
        }
    }
}

