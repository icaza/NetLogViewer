# NetLog Viewer

**NetLog Viewer** is a fast, resource-efficient Windows Forms application for visualizing, analyzing, and inspecting Chromium and Microsoft Edge NetLog files (`chrome://net-export` and `edge://net-export`).

The application is and engineered to consume minimal resources when parsing large multi-hundred-megabyte log files.

---

## Part of the Ostium toolset

NetLog Viewer is one of the Ostium tools — see also
[Ostium OSINT Browser](https://github.com/icaza/Ostium-Osint-Browser)

## Key Highlights

- **Extreme Performance & Minimal Memory**:
  - Tested on real **330 MB / 1,543,581 events** NetLog files.
  - Memory consumption stays **under 100 MB RAM** during execution.
  - Streaming line-by-line & `Utf8JsonReader` parsing takes only **~8 to 11 seconds** for 1.5M+ events.
- **Modern Dark UI**:
  - Modern color palette inspired by VS Code & Windows 11 Fluent dark guidelines.
  - Double-buffered, virtualized lists (`DoubleBufferedListView`) running at **60 FPS** with instant scrolling across 70,000+ sources.
- **Deep Inspection**:
  - **Sources Tab**: Grouped by network source (URL_REQUEST, SOCKET, HOST_RESOLVER, HTTP_STREAM_JOB, SPDY_SESSION, etc.).
  - **Selected Source Overview**: ID, Type, HTTP Method, URL, Status Badge, Elapsed Duration, and Bytes Transferred.
  - **Events Timeline**: Δt relative time offsets, event types, phases (BEGIN, END, NONE), and live parameters.
  - **HTTP Headers Inspector**: Dedicated tab for parsed HTTP request and response headers.
  - **Parameters Inspector**: Indented JSON tree viewer for every individual event.
  - **All Events Tab**: Global virtualized event stream across all sources with full-text search.
  - **Environment & Constants Tab**: Browser metadata (version, command line flags, OS), active field trials list, and net error mapping tables.
- **Productivity & Developer Tools**:
  - One-click **Copy as cURL** (with headers and method).
  - One-click **Copy URL** and **Copy Source JSON**.
  - **Export Filtered Sources to CSV**.
  - **Export Selected Source to JSON**.
  - Real-time debounced search by URL, hostname, ID, method, or error code.
  - **"Errors Only"** toggle to isolate failed requests (`net_error` or HTTP 4xx/5xx).
  - Drag-and-drop `.json` log files directly onto the app window.

---

## How to Build & Run

### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- .NET 10 SDK (installed: `10.0.403`)

### Running the Application
From the workspace root directory:
```bash
dotnet run --project NetLogViewer
```

To automatically load the sample file:
- Launch the application and click **File -> Open Sample File (edge-net-export-log.json)**, or
- Pass the file path as an argument:
```bash
dotnet run --project NetLogViewer -- "analyse_log/edge-net-export-log.json"
```
---

## Security & Reliability Design

1. **Safe Parsing**: Uses `System.Text.Json` without arbitrary code execution or unsafe deserialization vulnerabilities.
2. **Defensive Processing**: Missing or malformed lines in corrupted logs are skipped gracefully without crashing the application.
3. **Local & Private**: Completely offline, strictly zero network telemetry or data leakage.
4. **Memory Guard**: Employs streaming UTF-8 line buffers and compact struct allocations to handle multi-gigabyte logs without `OutOfMemoryException`.

