namespace NetLogViewer.Models;

/// <summary>
/// Represents a network source entity (e.g., URL_REQUEST, SOCKET, HOST_RESOLVER, HTTP_STREAM_JOB).
/// Groups all events belonging to the same source id.
/// </summary>
public sealed class NetLogSource
{
    public int Id { get; init; }
    public int TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public long StartTimeTicks { get; set; }
    public long EndTimeTicks { get; set; }
    public long DurationMs => Math.Max(0, EndTimeTicks - StartTimeTicks);

    public string? Method { get; set; }
    public string? Url { get; set; }
    public string? Host { get; set; }
    public int? StatusCode { get; set; }
    public string? StatusText { get; set; }
    public string? NetError { get; set; }
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }

    public bool HasError => !string.IsNullOrEmpty(NetError) || (StatusCode.HasValue && StatusCode.Value >= 400);

    public List<NetLogEvent> Events { get; } = [];
    public List<KeyValuePair<string, string>> RequestHeaders { get; } = [];
    public List<KeyValuePair<string, string>> ResponseHeaders { get; } = [];

    public string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(Url))
                return Url;
            if (!string.IsNullOrEmpty(Host))
                return Host;
            if (!string.IsNullOrEmpty(TypeName))
                return $"{TypeName} #{Id}";
            return $"Source #{Id}";
        }
    }

    public string StatusDisplay
    {
        get
        {
            if (StatusCode.HasValue)
                return $"{StatusCode.Value} {(StatusText ?? "")}".Trim();
            if (!string.IsNullOrEmpty(NetError))
                return NetError;
            return "OK";
        }
    }

    public NetLogSource(int id, int typeId)
    {
        Id = id;
        TypeId = typeId;
    }
}

