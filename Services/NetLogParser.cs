using NetLogViewer.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace NetLogViewer.Services;

public record ParserProgress(
    long BytesRead,
    long TotalBytes,
    int EventsCount,
    int SourcesCount,
    string StatusText
)
{
    public int Percent => TotalBytes > 0 ? (int)Math.Clamp((BytesRead * 100.0) / TotalBytes, 0, 100) : 0;
}

/// <summary>
/// High-performance, streaming parser for Chromium/Edge netlog JSON export files.
/// Designed for low memory consumption, smooth background execution, and robust error recovery.
/// </summary>
public sealed class NetLogParser
{
    public async Task<NetLogSession> ParseFileAsync(
        string filePath,
        IProgress<ParserProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("NetLog file not found.", filePath);

        var fileInfo = new FileInfo(filePath);
        long totalBytes = fileInfo.Length;

        var session = new NetLogSession
        {
            FilePath = filePath,
            FileSizeBytes = totalBytes
        };

        var sw = Stopwatch.StartNew();

        // Run heavy streaming on background thread
        await Task.Run(() =>
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256 * 1024, FileOptions.SequentialScan);
            using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 256 * 1024);

            string? line;
            long bytesReadApprox = 0;
            int eventCounter = 0;
            int lastReportedCount = 0;

            // Step 1: Read and parse Constants
            bool eventsStarted = false;
            while (!eventsStarted && (line = reader.ReadLine()) != null)
            {
                bytesReadApprox += line.Length + 2; // rough line bytes
                string trimmed = line.Trim();

                if (trimmed.StartsWith("{\"constants\":", StringComparison.OrdinalIgnoreCase))
                {
                    ParseConstants(trimmed, session.Constants);
                }

                if (trimmed.Contains("\"events\":", StringComparison.OrdinalIgnoreCase))
                {
                    eventsStarted = true;
                }
            }

            progress?.Report(new ParserProgress(bytesReadApprox, totalBytes, 0, 0, "Constants loaded. Streaming events..."));

            // Step 2: Stream events line by line
            var sourceMap = session.SourceMap;
            var sourcesList = session.Sources;

            while ((line = reader.ReadLine()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bytesReadApprox += line.Length + 2;

                if (line.Length < 10) continue;
                int startIdx = 0;
                while (startIdx < line.Length && char.IsWhiteSpace(line[startIdx])) startIdx++;
                int endIdx = line.Length - 1;
                while (endIdx >= 0 && (char.IsWhiteSpace(line[endIdx]) || line[endIdx] == ',')) endIdx--;

                if (endIdx <= startIdx || line[startIdx] != '{' || line[endIdx] != '}')
                    continue;

                try
                {
                    var span = line.AsMemory(startIdx, endIdx - startIdx + 1);
                    using var doc = JsonDocument.Parse(span);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("source", out var srcElem))
                        continue;
                    if (!srcElem.TryGetProperty("id", out var idElem))
                        continue;

                    int sourceId = idElem.GetInt32();
                    int typeId = srcElem.TryGetProperty("type", out var typeElem) ? typeElem.GetInt32() : 0;
                    ushort eventType = root.TryGetProperty("type", out var evTypeElem) ? (ushort)evTypeElem.GetInt32() : (ushort)0;
                    byte phase = root.TryGetProperty("phase", out var phaseElem) ? (byte)phaseElem.GetInt32() : (byte)0;

                    long timeTicks = 0;
                    if (root.TryGetProperty("time", out var timeElem))
                    {
                        if (timeElem.ValueKind == JsonValueKind.String)
                            long.TryParse(timeElem.GetString(), out timeTicks);
                        else if (timeElem.ValueKind == JsonValueKind.Number)
                            timeTicks = timeElem.GetInt64();
                    }

                    if (!sourceMap.TryGetValue(sourceId, out var source))
                    {
                        source = new NetLogSource(sourceId, typeId)
                        {
                            StartTimeTicks = timeTicks,
                            EndTimeTicks = timeTicks,
                            TypeName = session.Constants.GetSourceTypeName(typeId)
                        };
                        sourceMap[sourceId] = source;
                        sourcesList.Add(source);
                    }
                    else
                    {
                        if (source.TypeId == 0 && typeId != 0)
                        {
                            source.TypeId = typeId;
                            source.TypeName = session.Constants.GetSourceTypeName(typeId);
                        }
                        if (timeTicks > source.EndTimeTicks)
                            source.EndTimeTicks = timeTicks;
                    }

                    string? summary = null;
                    string? rawParamsJson = null;

                    if (root.TryGetProperty("params", out var paramsElem) && paramsElem.ValueKind == JsonValueKind.Object)
                    {
                        rawParamsJson = paramsElem.GetRawText();
                        summary = ExtractParamsSummaryAndDetails(paramsElem, source, session.Constants);
                    }

                    var netLogEvent = new NetLogEvent(sourceId, eventType, phase, timeTicks, summary, rawParamsJson)
                    {
                        EventTypeName = session.Constants.GetEventTypeName(eventType),
                        PhaseName = session.Constants.GetPhaseName(phase),
                        ElapsedFromStartMs = Math.Max(0, timeTicks - source.StartTimeTicks)
                    };

                    source.Events.Add(netLogEvent);
                    eventCounter++;

                    if (eventCounter - lastReportedCount >= 40000)
                    {
                        lastReportedCount = eventCounter;
                        progress?.Report(new ParserProgress(
                            Math.Min(bytesReadApprox, totalBytes),
                            totalBytes,
                            eventCounter,
                            sourcesList.Count,
                            $"Loaded {eventCounter:N0} events ({sourcesList.Count:N0} sources)..."
                        ));
                    }
                }
                catch (JsonException)
                {
                    // Ignore damaged/truncated line and continue
                }
            }

            session.TotalEventsCount = eventCounter;
            sw.Stop();
            session.ParseDuration = sw.Elapsed;

            // Final sort or clean up
            foreach (var s in sourcesList)
            {
                if (string.IsNullOrEmpty(s.TypeName))
                    s.TypeName = session.Constants.GetSourceTypeName(s.TypeId);
            }

            progress?.Report(new ParserProgress(totalBytes, totalBytes, eventCounter, sourcesList.Count, "Processing completed!"));
        }, cancellationToken);

        return session;
    }

    private static void ParseConstants(string line, NetLogConstants constants)
    {
        try
        {
            int constStart = line.IndexOf("\"constants\":", StringComparison.OrdinalIgnoreCase);
            if (constStart < 0) return;
            constStart += "\"constants\":".Length;

            // Look for end of constants object before "events"
            int eventsIdx = line.IndexOf("\"events\":", StringComparison.OrdinalIgnoreCase);
            string constJson;
            if (eventsIdx > constStart)
            {
                int endOfConst = eventsIdx - 1;
                while (endOfConst > constStart && (line[endOfConst] == ',' || char.IsWhiteSpace(line[endOfConst])))
                    endOfConst--;
                constJson = line.Substring(constStart, endOfConst - constStart + 1);
            }
            else
            {
                constJson = line[constStart..];
            }

            constJson = constJson.Trim();
            int lastBrace = constJson.LastIndexOf('}');
            if (lastBrace >= 0)
                constJson = constJson[..(lastBrace + 1)];

            using var doc = JsonDocument.Parse(constJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("clientInfo", out var clientElem) && clientElem.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in clientElem.EnumerateObject())
                    constants.ClientInfo[prop.Name] = prop.Value.ToString();
            }

            if (root.TryGetProperty("logCaptureMode", out var modeElem))
                constants.LogCaptureMode = modeElem.GetString() ?? "Unknown";

            if (root.TryGetProperty("timeTickOffset", out var tickElem))
            {
                if (tickElem.ValueKind == JsonValueKind.Number)
                    constants.TimeTickOffset = tickElem.GetInt64();
                else if (tickElem.ValueKind == JsonValueKind.String && long.TryParse(tickElem.GetString(), out var offset))
                    constants.TimeTickOffset = offset;

                if (constants.TimeTickOffset > 0)
                {
                    try
                    {
                        constants.BaseDateTime = DateTimeOffset.FromUnixTimeMilliseconds(constants.TimeTickOffset);
                    }
                    catch { }
                }
            }

            // Mappings: logSourceType
            if (root.TryGetProperty("logSourceType", out var srcTypesElem) && srcTypesElem.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in srcTypesElem.EnumerateObject())
                {
                    if (prop.Value.TryGetInt32(out int id))
                    {
                        constants.LogSourceTypes[id] = prop.Name;
                        constants.LogSourceTypesByName[prop.Name] = id;
                    }
                }
            }

            // Mappings: logEventTypes
            if (root.TryGetProperty("logEventTypes", out var evTypesElem) && evTypesElem.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in evTypesElem.EnumerateObject())
                {
                    if (prop.Value.TryGetInt32(out int id))
                    {
                        constants.LogEventTypes[id] = prop.Name;
                        constants.LogEventTypesByName[prop.Name] = id;
                    }
                }
            }

            // Mappings: netErrors
            if (root.TryGetProperty("netError", out var errElem) || root.TryGetProperty("netErrors", out errElem))
            {
                if (errElem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in errElem.EnumerateObject())
                    {
                        if (prop.Value.TryGetInt32(out int id))
                            constants.NetErrors[id] = prop.Name;
                    }
                }
            }

            // Active field trials
            if (root.TryGetProperty("activeFieldTrialGroups", out var trialsElem) && trialsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var trial in trialsElem.EnumerateArray())
                {
                    var str = trial.GetString();
                    if (!string.IsNullOrEmpty(str))
                        constants.ActiveFieldTrials.Add(str);
                }
            }
        }
        catch
        {
            // Fallback gracefully if constants parsing encountered edge cases
        }
    }

    private static string? ExtractParamsSummaryAndDetails(JsonElement paramsElem, NetLogSource source, NetLogConstants constants)
    {
        string? summary = null;

        // Check for URL
        if (paramsElem.TryGetProperty("url", out var urlElem) && urlElem.ValueKind == JsonValueKind.String)
        {
            string urlStr = urlElem.GetString() ?? "";
            if (string.IsNullOrEmpty(source.Url))
                source.Url = urlStr;
            summary = urlStr;
        }

        // Check for HTTP Method
        if (paramsElem.TryGetProperty("method", out var methodElem) && methodElem.ValueKind == JsonValueKind.String)
        {
            string mStr = methodElem.GetString() ?? "";
            if (string.IsNullOrEmpty(source.Method))
                source.Method = mStr;
        }

        // Check for host / hostname
        if (paramsElem.TryGetProperty("host", out var hostElem) && hostElem.ValueKind == JsonValueKind.String)
        {
            string hStr = hostElem.GetString() ?? "";
            if (string.IsNullOrEmpty(source.Host))
                source.Host = hStr;
        }
        else if (paramsElem.TryGetProperty("hostname", out var hnameElem) && hnameElem.ValueKind == JsonValueKind.String)
        {
            string hnStr = hnameElem.GetString() ?? "";
            if (string.IsNullOrEmpty(source.Host))
                source.Host = hnStr;
        }

        // Check for net_error
        if (paramsElem.TryGetProperty("net_error", out var netErrorElem))
        {
            int errCode = 0;
            if (netErrorElem.ValueKind == JsonValueKind.Number)
                errCode = netErrorElem.GetInt32();
            else if (netErrorElem.ValueKind == JsonValueKind.String)
                int.TryParse(netErrorElem.GetString(), out errCode);

            if (errCode != 0)
            {
                string errName = constants.GetNetErrorName(errCode);
                source.NetError = $"{errName} ({errCode})";
                summary = $"net_error = {source.NetError}";
            }
        }

        // Check for byte counts
        if (paramsElem.TryGetProperty("byte_count", out var bcElem) && bcElem.TryGetInt64(out long bytes))
        {
            source.BytesReceived += bytes;
            summary = $"{bytes:N0} bytes";
        }
        else if (paramsElem.TryGetProperty("size", out var szElem) && szElem.TryGetInt64(out long size))
        {
            source.BytesReceived += size;
            summary = $"{size:N0} bytes";
        }

        // Check for HTTP headers
        if (paramsElem.TryGetProperty("headers", out var headersElem))
        {
            if (headersElem.ValueKind == JsonValueKind.Array)
            {
                bool isRequest = false;
                bool isResponse = false;

                foreach (var h in headersElem.EnumerateArray())
                {
                    string? hLine = h.GetString();
                    if (string.IsNullOrWhiteSpace(hLine)) continue;

                    if (hLine.StartsWith("HTTP/1.1 ", StringComparison.OrdinalIgnoreCase) ||
                        hLine.StartsWith(":status: ", StringComparison.OrdinalIgnoreCase))
                    {
                        isResponse = true;
                        var parts = hLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && int.TryParse(parts[1], out int sc))
                        {
                            source.StatusCode = sc;
                            source.StatusText = parts.Length > 2 ? parts[2] : "";
                        }
                    }
                    else if (hLine.Contains(" HTTP/1.", StringComparison.OrdinalIgnoreCase))
                    {
                        isRequest = true;
                    }

                    // Parse header name : value
                    int colonIdx = hLine.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        string headerName = hLine[..colonIdx].Trim();
                        string headerValue = hLine[(colonIdx + 1)..].Trim();

                        if (isResponse)
                        {
                            source.ResponseHeaders.Add(new KeyValuePair<string, string>(headerName, headerValue));
                        }
                        else
                        {
                            source.RequestHeaders.Add(new KeyValuePair<string, string>(headerName, headerValue));
                        }
                    }
                }

                if (isResponse && source.StatusCode.HasValue)
                {
                    summary = $"HTTP {source.StatusCode.Value} {source.StatusText}".Trim();
                }
                else if (isRequest && !string.IsNullOrEmpty(source.Method))
                {
                    summary = $"{source.Method} Request Headers ({headersElem.GetArrayLength()} items)";
                }
            }
        }

        // Address / Remote endpoint
        if (paramsElem.TryGetProperty("address", out var addrElem) && addrElem.ValueKind == JsonValueKind.String)
        {
            string addr = addrElem.GetString() ?? "";
            if (string.IsNullOrEmpty(summary))
                summary = $"address: {addr}";
        }

        return summary;
    }
}
