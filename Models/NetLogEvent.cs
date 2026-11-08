namespace NetLogViewer.Models;

/// <summary>
/// Represents a single logged event in the netlog file.
/// Optimized for minimal memory usage while providing quick summary information.
/// </summary>
public sealed class NetLogEvent
{
    public int SourceId { get; init; }
    public ushort EventType { get; init; }
    public byte Phase { get; init; }
    public long TimeTicks { get; init; }
    public long ElapsedFromStartMs { get; set; }
    public string? Summary { get; init; }
    public string? RawParamsJson { get; init; }

    public string EventTypeName { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;

    public NetLogEvent(int sourceId, ushort eventType, byte phase, long timeTicks, string? summary, string? rawParamsJson)
    {
        SourceId = sourceId;
        EventType = eventType;
        Phase = phase;
        TimeTicks = timeTicks;
        Summary = summary;
        RawParamsJson = rawParamsJson;
    }
}

