namespace NetLogViewer.Models;

/// <summary>
/// Holds constants, client info, and dictionary mappings parsed from the netlog file.
/// </summary>
public sealed class NetLogConstants
{
    public Dictionary<string, string> ClientInfo { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, string> LogSourceTypes { get; } = [];
    public Dictionary<string, int> LogSourceTypesByName { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, string> LogEventTypes { get; } = [];
    public Dictionary<string, int> LogEventTypesByName { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, string> LogEventPhases { get; } = [];
    public Dictionary<int, string> NetErrors { get; } = [];
    public Dictionary<int, string> AddressFamily { get; } = [];
    public Dictionary<int, string> DnsQueryType { get; } = [];
    public Dictionary<int, string> LoadState { get; } = [];
    public List<string> ActiveFieldTrials { get; } = [];

    public string LogCaptureMode { get; set; } = "Unknown";
    public long TimeTickOffset { get; set; }
    public DateTimeOffset? BaseDateTime { get; set; }

    public NetLogConstants()
    {
        // Standard default mappings in case the file doesn't define some
        LogEventPhases[0] = "PHASE_NONE";
        LogEventPhases[1] = "PHASE_BEGIN";
        LogEventPhases[2] = "PHASE_END";

        // Net error 0 is always OK
        NetErrors[0] = "OK";
    }

    public string GetSourceTypeName(int typeId)
    {
        return LogSourceTypes.TryGetValue(typeId, out var name) ? name : $"SOURCE_{typeId}";
    }

    public string GetEventTypeName(int typeId)
    {
        return LogEventTypes.TryGetValue(typeId, out var name) ? name : $"EVENT_{typeId}";
    }

    public string GetPhaseName(byte phase)
    {
        return LogEventPhases.TryGetValue(phase, out var name) ? name : phase switch
        {
            1 => "PHASE_BEGIN",
            2 => "PHASE_END",
            _ => "PHASE_NONE"
        };
    }

    public string GetNetErrorName(int errorCode)
    {
        return NetErrors.TryGetValue(errorCode, out var name) ? name : $"NET_ERROR_{errorCode}";
    }
}

