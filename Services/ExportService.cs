using NetLogViewer.Models;
using System.Text;
using System.Text.Json;

namespace NetLogViewer.Services;

/// <summary>
/// Provides secure export utilities for CSV, JSON, and cURL commands.
/// </summary>
public static class ExportService
{
    public static void ExportSourcesToCsv(string filePath, IEnumerable<NetLogSource> sources)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        writer.WriteLine("SourceId,Type,Method,StatusCode,StatusText,DurationMs,BytesReceived,NetError,Description");

        foreach (var s in sources)
        {
            var line = $"{s.Id}," +
                       $"\"{EscapeCsv(s.TypeName)}\"," +
                       $"\"{EscapeCsv(s.Method ?? "")}\"," +
                       $"{s.StatusCode?.ToString() ?? ""}," +
                       $"\"{EscapeCsv(s.StatusText ?? "")}\"," +
                       $"{s.DurationMs}," +
                       $"{s.BytesReceived}," +
                       $"\"{EscapeCsv(s.NetError ?? "")}\"," +
                       $"\"{EscapeCsv(s.Description)}\"";
            writer.WriteLine(line);
        }
    }

    public static void ExportEventsToCsv(string filePath, IEnumerable<NetLogEvent> events)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        writer.WriteLine("SourceId,ElapsedMs,TimeTicks,EventType,Phase,Summary");

        foreach (var ev in events)
        {
            var line = $"{ev.SourceId}," +
                       $"{ev.ElapsedFromStartMs}," +
                       $"{ev.TimeTicks}," +
                       $"\"{EscapeCsv(ev.EventTypeName)}\"," +
                       $"\"{EscapeCsv(ev.PhaseName)}\"," +
                       $"\"{EscapeCsv(ev.Summary ?? "")}\"";
            writer.WriteLine(line);
        }
    }

    public static string ExportSourceToJson(NetLogSource source)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var exportObj = new
        {
            source.Id,
            source.TypeId,
            source.TypeName,
            source.Method,
            source.Url,
            source.StatusCode,
            source.StatusText,
            source.NetError,
            source.DurationMs,
            source.BytesReceived,
            RequestHeaders = source.RequestHeaders,
            ResponseHeaders = source.ResponseHeaders,
            Events = source.Events.ConvertAll(ev => new
            {
                ev.ElapsedFromStartMs,
                ev.TimeTicks,
                ev.EventType,
                ev.EventTypeName,
                ev.PhaseName,
                ev.Summary
            })
        };

        return JsonSerializer.Serialize(exportObj, options);
    }

    public static string GenerateCurlCommand(NetLogSource source)
    {
        if (string.IsNullOrEmpty(source.Url))
            return "# No URL available for this source";

        var sb = new StringBuilder();
        string method = source.Method?.ToUpperInvariant() ?? "GET";
        sb.Append($"curl -X {method} \"{source.Url}\"");

        foreach (var header in source.RequestHeaders)
        {
            if (header.Key.StartsWith(':')) continue; // pseudo-headers in HTTP/2 / HTTP/3
            sb.Append($" \\\n  -H \"{header.Key}: {header.Value.Replace("\"", "\\\"")}\"");
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}

