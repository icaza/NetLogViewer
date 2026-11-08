using NetLogViewer.Models;
using NetLogViewer.Services;
using NetLogViewer.UI.Controls;
using NetLogViewer.UI.Forms;
using NetLogViewer.UI.Theme;
using System.Diagnostics;
using System.Text.Json;

namespace NetLogViewer.Forms;

public sealed class MainForm : Form
{
    private readonly NetLogParser _parser = new();
    private NetLogSession? _session;
    private CancellationTokenSource? _cts;

    // Filtered lists
    private List<NetLogSource> _filteredSources = [];
    private NetLogSource? _selectedSource;
    private List<NetLogEvent> _selectedSourceEvents = [];
    private List<NetLogEvent> _allFilteredEvents = [];

    // Sorting state
    private int _sortColumn = 0;
    private bool _sortAscending = true;

    // UI Controls
    private MenuStrip _menuStrip = null!;
    private ToolStrip _toolStrip = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripProgressBar _statusProgress = null!;
    private ToolStripStatusLabel _statusFilterCount = null!;
    private ToolStripStatusLabel _statusMemory = null!;
    private ToolStripStatusLabel _statusFileInfo = null!;

    // Header controls
    private ToolStripTextBox _txtSearch = null!;
    private ToolStripComboBox _cmbTypeFilter = null!;
    private ToolStripButton _btnErrorsOnly = null!;
    private ToolStripButton _btnClearFilter = null!;

    // Tabs
    private DarkTabControl _mainTabs = null!;
    private TabPage _tabSources = null!;
    private TabPage _tabAllEvents = null!;
    private TabPage _tabConstants = null!;

    // Sources tab controls
    private SplitContainer _sourcesSplit = null!;
    private DoubleBufferedListView _lvSources = null!;
    private Panel _pnlSourceDetail = null!;
    private Label _lblDetailTitle = null!;
    private Label _lblDetailBadges = null!;
    private Button _btnCopyUrl = null!;
    private Button _btnCopyCurl = null!;
    private Button _btnCopyJson = null!;
    private DarkTabControl _detailTabs = null!;
    private TabPage _tabSourceTimeline = null!;
    private TabPage _tabSourceHeaders = null!;
    private TabPage _tabSourceParams = null!;

    // Detail sub-controls
    private DoubleBufferedListView _lvSourceEvents = null!;
    private DoubleBufferedListView _lvHeaders = null!;
    private TextBox _txtEventParams = null!;

    // All Events tab controls
    private DoubleBufferedListView _lvAllEvents = null!;

    // Constants tab controls
    private TableLayoutPanel _constantsLayout = null!;
    private TextBox _txtClientInfo = null!;
    private ListBox _lstFieldTrials = null!;
    private DoubleBufferedListView _lvConstantsTable = null!;

    // Timers
    private readonly System.Windows.Forms.Timer _searchDebounceTimer = new();
    private readonly System.Windows.Forms.Timer _memoryTimer = new();

    public MainForm()
    {
        InitializeUi();
        DarkTheme.ApplyDarkThemeToControl(this);

        _searchDebounceTimer.Interval = 250;
        _searchDebounceTimer.Tick += (s, e) =>
        {
            _searchDebounceTimer.Stop();
            ApplySourcesFilter();
        };

        _memoryTimer.Interval = 2000;
        _memoryTimer.Tick += (s, e) => UpdateMemoryUsage();
        _memoryTimer.Start();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTheme.EnableImmersiveDarkMode(this);
    }

    private void InitializeUi()
    {
        Text = "NetLog Viewer - [No file loaded]";
        Size = new Size(1340, 840);
        MinimumSize = new Size(960, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DarkTheme.Background;
        ForeColor = DarkTheme.TextPrimary;
        Font = DarkTheme.MainFont;
        AllowDrop = true;

        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;

        // 1. MenuStrip
        _menuStrip = new MenuStrip
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = DarkTheme.Surface,
            ForeColor = DarkTheme.TextPrimary
        };

        var fileMenu = new ToolStripMenuItem("&File");
        var miOpen = new ToolStripMenuItem("&Open NetLog JSON...", null, async (s, e) => await PromptOpenFileAsync())
        {
            ShortcutKeys = Keys.Control | Keys.O
        };
        var miSample = new ToolStripMenuItem("Open Sample File (edge-net-export-log.json)", null, async (s, e) => await LoadSampleFileAsync());
        var miExportCsv = new ToolStripMenuItem("Export &Filtered Sources to CSV...", null, (s, e) => ExportFilteredSourcesCsv());
        var miExportJson = new ToolStripMenuItem("Export &Selected Source to JSON...", null, (s, e) => ExportSelectedSourceJson());
        var miExit = new ToolStripMenuItem("E&xit", null, (s, e) => Close());

        fileMenu.DropDownItems.AddRange([miOpen, miSample, new ToolStripSeparator(), miExportCsv, miExportJson, new ToolStripSeparator(), miExit]);

        var filterMenu = new ToolStripMenuItem("&Filter");
        var miToggleErrors = new ToolStripMenuItem("&Toggle Errors Only", null, (s, e) =>
        {
            _btnErrorsOnly.Checked = !_btnErrorsOnly.Checked;
            ApplySourcesFilter();
        })
        {
            ShortcutKeys = Keys.Control | Keys.E
        };

        var miClearFilters = new ToolStripMenuItem("&Clear Filters", null, (s, e) => ResetFilters())
        {
            ShortcutKeyDisplayString = "Esc",
            ShortcutKeys = Keys.Control | Keys.R
        };

        filterMenu.DropDownItems.AddRange([miToggleErrors, miClearFilters]);

        var helpMenu = new ToolStripMenuItem("&Help");
        var miAbout = new ToolStripMenuItem("&About NetLog Viewer", null, (s, e) => new AboutForm().ShowDialog(this))
        {
            ShortcutKeys = Keys.F1
        };
        helpMenu.DropDownItems.Add(miAbout);

        _menuStrip.Items.AddRange([fileMenu, filterMenu, helpMenu]);

        // 2. ToolStrip
        _toolStrip = new ToolStrip
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = DarkTheme.Surface,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(6, 4, 6, 4)
        };

        var btnOpen = new ToolStripButton("Open File...", null, async (s, e) => await PromptOpenFileAsync())
        {
            ForeColor = DarkTheme.TextBright,
            Font = DarkTheme.HeaderFont
        };

        var sep1 = new ToolStripSeparator();

        var lblSearch = new ToolStripLabel("Search:")
        {
            ForeColor = DarkTheme.TextSecondary
        };

        _txtSearch = new ToolStripTextBox
        {
            Width = 260,
            BackColor = DarkTheme.Card,
            ForeColor = DarkTheme.TextBright,
            BorderStyle = BorderStyle.FixedSingle,
            ToolTipText = "Filter by URL, host, type, ID, or error keyword"
        };
        _txtSearch.TextChanged += (s, e) =>
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        };

        var lblType = new ToolStripLabel("Type:")
        {
            ForeColor = DarkTheme.TextSecondary
        };

        _cmbTypeFilter = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 170,
            BackColor = DarkTheme.Card,
            ForeColor = DarkTheme.TextBright,
            FlatStyle = FlatStyle.Flat
        };
        _cmbTypeFilter.Items.Add("All Source Types");
        _cmbTypeFilter.SelectedIndex = 0;
        _cmbTypeFilter.SelectedIndexChanged += (s, e) => ApplySourcesFilter();

        _btnErrorsOnly = new ToolStripButton("Errors Only", null, (s, e) =>
        {
            _btnErrorsOnly.Checked = !_btnErrorsOnly.Checked;
            ApplySourcesFilter();
        })
        {
            CheckOnClick = true,
            ForeColor = DarkTheme.WarningYellow
        };

        _btnClearFilter = new ToolStripButton("Reset", null, (s, e) => ResetFilters())
        {
            ForeColor = DarkTheme.TextSecondary
        };

        _toolStrip.Items.AddRange([
            btnOpen, sep1,
            lblSearch, _txtSearch,
            lblType, _cmbTypeFilter,
            _btnErrorsOnly, _btnClearFilter
        ]);

        // 3. StatusStrip
        _statusStrip = new StatusStrip
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = DarkTheme.Surface,
            ForeColor = DarkTheme.TextSecondary
        };

        _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusProgress = new ToolStripProgressBar
        {
            Width = 140,
            Visible = false,
            Style = ProgressBarStyle.Continuous
        };
        _statusFilterCount = new ToolStripStatusLabel("0 sources") { BorderSides = ToolStripStatusLabelBorderSides.Left, BorderStyle = Border3DStyle.Etched };
        _statusMemory = new ToolStripStatusLabel("RAM: --") { BorderSides = ToolStripStatusLabelBorderSides.Left, BorderStyle = Border3DStyle.Etched };
        _statusFileInfo = new ToolStripStatusLabel("No file") { BorderSides = ToolStripStatusLabelBorderSides.Left, BorderStyle = Border3DStyle.Etched };

        _statusStrip.Items.AddRange([_statusLabel, _statusProgress, _statusFilterCount, _statusMemory, _statusFileInfo]);

        // 4. Main Tabs
        _mainTabs = new DarkTabControl { Dock = DockStyle.Fill };

        _tabSources = new TabPage("Sources");
        _tabAllEvents = new TabPage("All Events");
        _tabConstants = new TabPage("Environment & Constants");

        BuildSourcesTab();
        BuildAllEventsTab();
        BuildConstantsTab();

        _mainTabs.TabPages.AddRange([_tabSources, _tabAllEvents, _tabConstants]);

        Controls.Add(_mainTabs);
        Controls.Add(_statusStrip);
        Controls.Add(_toolStrip);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
    }

    private void BuildSourcesTab()
    {
        _sourcesSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 680,
            SplitterWidth = 5,
            BackColor = DarkTheme.Border
        };

        // Master List of Sources
        _lvSources = new DoubleBufferedListView
        {
            Dock = DockStyle.Fill,
            VirtualMode = true
        };

        _lvSources.Columns.Add("ID", 65, HorizontalAlignment.Left);
        _lvSources.Columns.Add("Type", 120, HorizontalAlignment.Left);
        _lvSources.Columns.Add("Method", 65, HorizontalAlignment.Left);
        _lvSources.Columns.Add("Status", 95, HorizontalAlignment.Left);
        _lvSources.Columns.Add("Duration", 85, HorizontalAlignment.Right);
        _lvSources.Columns.Add("Bytes", 85, HorizontalAlignment.Right);
        _lvSources.Columns.Add("Description / URL", 450, HorizontalAlignment.Left);

        _lvSources.RetrieveVirtualItem += LvSources_RetrieveVirtualItem;
        _lvSources.SelectedIndexChanged += LvSources_SelectedIndexChanged;
        _lvSources.ColumnClick += LvSources_ColumnClick;

        _sourcesSplit.Panel1.Controls.Add(_lvSources);

        // Details Panel
        _pnlSourceDetail = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.Background,
            Padding = new Padding(8)
        };

        // Header card
        var cardHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = DarkTheme.Card,
            Padding = new Padding(12)
        };

        _lblDetailTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Font = DarkTheme.HeaderFont,
            ForeColor = DarkTheme.TextBright,
            Text = "Select a source to view details"
        };

        _lblDetailBadges = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Font = DarkTheme.SmallFont,
            ForeColor = DarkTheme.TextSecondary,
            Text = ""
        };

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            FlowDirection = FlowDirection.LeftToRight
        };

        _btnCopyUrl = new Button { Text = "Copy URL", AutoSize = true, Height = 24 };
        _btnCopyUrl.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_selectedSource?.Url))
            {
                Clipboard.SetText(_selectedSource.Url);
                _statusLabel.Text = "URL copied to clipboard.";
            }
        };

        _btnCopyCurl = new Button { Text = "Copy as cURL", AutoSize = true, Height = 24 };
        _btnCopyCurl.Click += (s, e) =>
        {
            if (_selectedSource != null)
            {
                Clipboard.SetText(ExportService.GenerateCurlCommand(_selectedSource));
                _statusLabel.Text = "cURL command copied to clipboard.";
            }
        };

        _btnCopyJson = new Button { Text = "Copy Source JSON", AutoSize = true, Height = 24 };
        _btnCopyJson.Click += (s, e) =>
        {
            if (_selectedSource != null)
            {
                Clipboard.SetText(ExportService.ExportSourceToJson(_selectedSource));
                _statusLabel.Text = "Source JSON copied to clipboard.";
            }
        };

        actionsPanel.Controls.AddRange([_btnCopyUrl, _btnCopyCurl, _btnCopyJson]);
        cardHeader.Controls.Add(actionsPanel);
        cardHeader.Controls.Add(_lblDetailBadges);
        cardHeader.Controls.Add(_lblDetailTitle);

        // Sub-tabs for source details
        _detailTabs = new DarkTabControl
        {
            Dock = DockStyle.Fill
        };

        _tabSourceTimeline = new TabPage("Timeline Events");
        _tabSourceHeaders = new TabPage("HTTP Headers");
        _tabSourceParams = new TabPage("Selected Event Inspector");

        // 1. Timeline list
        _lvSourceEvents = new DoubleBufferedListView
        {
            Dock = DockStyle.Fill,
            VirtualMode = true
        };
        _lvSourceEvents.Columns.Add("+Δt (ms)", 75, HorizontalAlignment.Right);
        _lvSourceEvents.Columns.Add("Time", 85, HorizontalAlignment.Right);
        _lvSourceEvents.Columns.Add("Event Type", 190, HorizontalAlignment.Left);
        _lvSourceEvents.Columns.Add("Phase", 110, HorizontalAlignment.Left);
        _lvSourceEvents.Columns.Add("Summary / Params", 350, HorizontalAlignment.Left);

        _lvSourceEvents.RetrieveVirtualItem += LvSourceEvents_RetrieveVirtualItem;
        _lvSourceEvents.SelectedIndexChanged += LvSourceEvents_SelectedIndexChanged;
        _tabSourceTimeline.Controls.Add(_lvSourceEvents);

        // 2. HTTP Headers list
        _lvHeaders = new DoubleBufferedListView
        {
            Dock = DockStyle.Fill
        };
        _lvHeaders.Columns.Add("Direction", 80, HorizontalAlignment.Left);
        _lvHeaders.Columns.Add("Header Name", 180, HorizontalAlignment.Left);
        _lvHeaders.Columns.Add("Header Value", 360, HorizontalAlignment.Left);
        _tabSourceHeaders.Controls.Add(_lvHeaders);

        // 3. Event Parameters Inspector
        _txtEventParams = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = DarkTheme.CodeFont,
            BackColor = DarkTheme.Card,
            ForeColor = DarkTheme.TextBright,
            BorderStyle = BorderStyle.None
        };
        _tabSourceParams.Controls.Add(_txtEventParams);

        _detailTabs.TabPages.AddRange([_tabSourceTimeline, _tabSourceHeaders, _tabSourceParams]);

        _pnlSourceDetail.Controls.Add(_detailTabs);
        _pnlSourceDetail.Controls.Add(cardHeader);

        _sourcesSplit.Panel2.Controls.Add(_pnlSourceDetail);
        _tabSources.Controls.Add(_sourcesSplit);
    }

    private void BuildAllEventsTab()
    {
        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = DarkTheme.Card,
            Padding = new Padding(8, 4, 8, 4)
        };

        var lblFilter = new Label
        {
            Text = "Filter Global Events:",
            ForeColor = DarkTheme.TextSecondary,
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var txtFilter = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.Surface,
            ForeColor = DarkTheme.TextBright,
            BorderStyle = BorderStyle.FixedSingle
        };
        txtFilter.TextChanged += (s, e) =>
        {
            ApplyAllEventsFilter(txtFilter.Text);
        };

        pnlTop.Controls.Add(txtFilter);
        pnlTop.Controls.Add(lblFilter);

        _lvAllEvents = new DoubleBufferedListView
        {
            Dock = DockStyle.Fill,
            VirtualMode = true
        };
        _lvAllEvents.Columns.Add("Source ID", 80, HorizontalAlignment.Left);
        _lvAllEvents.Columns.Add("Time", 90, HorizontalAlignment.Right);
        _lvAllEvents.Columns.Add("Event Type", 200, HorizontalAlignment.Left);
        _lvAllEvents.Columns.Add("Phase", 110, HorizontalAlignment.Left);
        _lvAllEvents.Columns.Add("Summary", 500, HorizontalAlignment.Left);

        _lvAllEvents.RetrieveVirtualItem += LvAllEvents_RetrieveVirtualItem;
        _lvAllEvents.DoubleClick += (s, e) =>
        {
            if (_lvAllEvents.SelectedIndices.Count > 0)
            {
                int idx = _lvAllEvents.SelectedIndices[0];
                if (idx >= 0 && idx < _allFilteredEvents.Count)
                {
                    var ev = _allFilteredEvents[idx];
                    JumpToSource(ev.SourceId);
                }
            }
        };

        _tabAllEvents.Controls.Add(_lvAllEvents);
        _tabAllEvents.Controls.Add(pnlTop);
    }

    private void BuildConstantsTab()
    {
        _constantsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = DarkTheme.Background,
            Padding = new Padding(8)
        };

        _constantsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
        _constantsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        _constantsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));

        // Column 1: Client Info
        var grpClient = new GroupBox
        {
            Text = "Client & Environment Info",
            Dock = DockStyle.Fill,
            ForeColor = DarkTheme.TextBright
        };
        _txtClientInfo = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = DarkTheme.CodeFont,
            BackColor = DarkTheme.Card,
            ForeColor = DarkTheme.TextBright,
            BorderStyle = BorderStyle.None
        };
        grpClient.Controls.Add(_txtClientInfo);

        // Column 2: Active Field Trials
        var grpTrials = new GroupBox
        {
            Text = "Active Field Trials",
            Dock = DockStyle.Fill,
            ForeColor = DarkTheme.TextBright
        };
        _lstFieldTrials = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.Card,
            ForeColor = DarkTheme.TextBright,
            BorderStyle = BorderStyle.None,
            Font = DarkTheme.MainFont
        };
        grpTrials.Controls.Add(_lstFieldTrials);

        // Column 3: Constants Reference Table
        var grpConstants = new GroupBox
        {
            Text = "Net Errors & Source Mappings",
            Dock = DockStyle.Fill,
            ForeColor = DarkTheme.TextBright
        };
        _lvConstantsTable = new DoubleBufferedListView
        {
            Dock = DockStyle.Fill
        };
        _lvConstantsTable.Columns.Add("Code / ID", 75, HorizontalAlignment.Right);
        _lvConstantsTable.Columns.Add("Type / Category", 110, HorizontalAlignment.Left);
        _lvConstantsTable.Columns.Add("Constant Name", 240, HorizontalAlignment.Left);
        grpConstants.Controls.Add(_lvConstantsTable);

        _constantsLayout.Controls.Add(grpClient, 0, 0);
        _constantsLayout.Controls.Add(grpTrials, 1, 0);
        _constantsLayout.Controls.Add(grpConstants, 2, 0);

        _tabConstants.Controls.Add(_constantsLayout);
    }

    private async Task PromptOpenFileAsync()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Open Chromium / Edge NetLog Export JSON",
            Filter = "NetLog JSON (*.json)|*.json|All Files (*.*)|*.*",
            RestoreDirectory = true
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            await LoadFileAsync(ofd.FileName);
        }
    }

    private async Task LoadSampleFileAsync()
    {
        string samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"analyse_log\edge-net-export-log.json");
        if (!File.Exists(samplePath))
            samplePath = Path.GetFullPath("analyse_log/edge-net-export-log.json");

        if (File.Exists(samplePath))
        {
            await LoadFileAsync(samplePath);
        }
        else
        {
            MessageBox.Show(this, "Sample file 'edge-net-export-log.json' not found in analyse_log folder.", "Sample File Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task LoadFileAsync(string filePath)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _statusProgress.Visible = true;
        _statusProgress.Value = 0;
        _statusLabel.Text = $"Loading {Path.GetFileName(filePath)}...";

        var progress = new Progress<ParserProgress>(p =>
        {
            if (p.TotalBytes > 0)
                _statusProgress.Value = p.Percent;
            _statusLabel.Text = p.StatusText;
            _statusFilterCount.Text = $"{p.SourcesCount:N0} sources | {p.EventsCount:N0} events";
        });

        try
        {
            var sw = Stopwatch.StartNew();
            _session = await _parser.ParseFileAsync(filePath, progress, _cts.Token);
            sw.Stop();

            Text = $"NetLog Viewer - [{_session.FileName}] ({_session.FileSizeFormatted})";
            _statusFileInfo.Text = $"{_session.FileName} ({_session.FileSizeFormatted})";
            _statusLabel.Text = $"Loaded {_session.TotalEventsCount:N0} events into {_session.Sources.Count:N0} sources in {_session.ParseDuration.TotalSeconds:F2}s";

            PopulateTypeFilter();
            PopulateConstantsTab();
            PopulateAllEventsTab();
            ApplySourcesFilter();
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Loading cancelled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to parse NetLog file:\n{ex.Message}", "Parsing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Error loading file.";
        }
        finally
        {
            _statusProgress.Visible = false;
        }
    }

    private void PopulateTypeFilter()
    {
        _cmbTypeFilter.Items.Clear();
        _cmbTypeFilter.Items.Add("All Source Types");

        if (_session == null) return;

        var sourceTypes = _session.Sources
            .Select(s => s.TypeName)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .OrderBy(t => t);

        foreach (var t in sourceTypes)
            _cmbTypeFilter.Items.Add(t);

        _cmbTypeFilter.SelectedIndex = 0;
    }

    private void PopulateConstantsTab()
    {
        if (_session == null) return;

        // Client info
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Capture Mode: {_session.Constants.LogCaptureMode}");
        if (_session.Constants.BaseDateTime.HasValue)
            sb.AppendLine($"Start Time: {_session.Constants.BaseDateTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Time Tick Offset: {_session.Constants.TimeTickOffset}");
        sb.AppendLine(new string('-', 40));

        foreach (var (k, v) in _session.Constants.ClientInfo)
        {
            sb.AppendLine($"{k}:");
            sb.AppendLine($"  {v}");
        }

        _txtClientInfo.Text = sb.ToString();

        // Field Trials
        _lstFieldTrials.Items.Clear();
        foreach (var trial in _session.Constants.ActiveFieldTrials)
            _lstFieldTrials.Items.Add(trial);

        // Constants table
        _lvConstantsTable.Items.Clear();
        _lvConstantsTable.BeginUpdate();

        foreach (var (code, name) in _session.Constants.NetErrors)
        {
            var lvi = new ListViewItem(code.ToString());
            lvi.SubItems.Add("NetError");
            lvi.SubItems.Add(name);
            _lvConstantsTable.Items.Add(lvi);
        }

        foreach (var (id, name) in _session.Constants.LogSourceTypes)
        {
            var lvi = new ListViewItem(id.ToString());
            lvi.SubItems.Add("SourceType");
            lvi.SubItems.Add(name);
            _lvConstantsTable.Items.Add(lvi);
        }

        _lvConstantsTable.EndUpdate();
    }

    private void PopulateAllEventsTab()
    {
        if (_session == null) return;
        ApplyAllEventsFilter("");
    }

    private void ApplyAllEventsFilter(string query)
    {
        if (_session == null)
        {
            _allFilteredEvents = [];
            _lvAllEvents.VirtualListSize = 0;
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            // Limit to first 100,000 events for smooth UI responsiveness in global tab
            _allFilteredEvents = _session.Sources.SelectMany(s => s.Events).Take(100000).ToList();
        }
        else
        {
            string q = query.Trim();
            _allFilteredEvents = _session.Sources
                .SelectMany(s => s.Events)
                .Where(ev => ev.SourceId.ToString().Contains(q) ||
                             ev.EventTypeName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                             (ev.Summary != null && ev.Summary.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .Take(50000)
                .ToList();
        }

        _lvAllEvents.VirtualListSize = _allFilteredEvents.Count;
        _lvAllEvents.Invalidate();
    }

    private void ApplySourcesFilter()
    {
        if (_session == null)
        {
            _filteredSources = [];
            _lvSources.VirtualListSize = 0;
            return;
        }

        string query = _txtSearch.Text.Trim();
        string selectedType = _cmbTypeFilter.SelectedItem?.ToString() ?? "All Source Types";
        bool errorsOnly = _btnErrorsOnly.Checked;

        IEnumerable<NetLogSource> filtered = _session.Sources;

        if (errorsOnly)
        {
            filtered = filtered.Where(s => s.HasError);
        }

        if (!string.IsNullOrEmpty(selectedType) && selectedType != "All Source Types")
        {
            filtered = filtered.Where(s => string.Equals(s.TypeName, selectedType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(s =>
                s.Id.ToString().Contains(query) ||
                (s.Url != null && s.Url.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.Host != null && s.Host.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.Method != null && s.Method.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.TypeName != null && s.TypeName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.NetError != null && s.NetError.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.StatusCode.HasValue && s.StatusCode.Value.ToString().Contains(query))
            );
        }

        // Apply sorting
        filtered = _sortColumn switch
        {
            0 => _sortAscending ? filtered.OrderBy(s => s.Id) : filtered.OrderByDescending(s => s.Id),
            1 => _sortAscending ? filtered.OrderBy(s => s.TypeName) : filtered.OrderByDescending(s => s.TypeName),
            2 => _sortAscending ? filtered.OrderBy(s => s.Method ?? "") : filtered.OrderByDescending(s => s.Method ?? ""),
            3 => _sortAscending ? filtered.OrderBy(s => s.StatusCode ?? 0) : filtered.OrderByDescending(s => s.StatusCode ?? 0),
            4 => _sortAscending ? filtered.OrderBy(s => s.DurationMs) : filtered.OrderByDescending(s => s.DurationMs),
            5 => _sortAscending ? filtered.OrderBy(s => s.BytesReceived) : filtered.OrderByDescending(s => s.BytesReceived),
            6 => _sortAscending ? filtered.OrderBy(s => s.Description) : filtered.OrderByDescending(s => s.Description),
            _ => filtered
        };

        _filteredSources = filtered.ToList();
        _lvSources.VirtualListSize = _filteredSources.Count;
        _lvSources.Invalidate();

        _statusFilterCount.Text = $"{_filteredSources.Count:N0} of {_session.Sources.Count:N0} sources";

        // Auto select first item if available
        if (_filteredSources.Count > 0)
        {
            SelectSource(_filteredSources[0]);
        }
        else
        {
            ClearSourceDetails();
        }
    }

    private void ResetFilters()
    {
        _txtSearch.Text = "";
        _cmbTypeFilter.SelectedIndex = 0;
        _btnErrorsOnly.Checked = false;
        ApplySourcesFilter();
    }

    private void SelectSource(NetLogSource source)
    {
        _selectedSource = source;
        _selectedSourceEvents = source.Events;

        _lblDetailTitle.Text = source.Description;
        _lblDetailBadges.Text = $"Source #{source.Id} | Type: {source.TypeName} | Events: {source.Events.Count} | Status: {source.StatusDisplay} | Duration: {source.DurationMs} ms | Transferred: {source.BytesReceived:N0} bytes";

        // Update timeline list
        _lvSourceEvents.VirtualListSize = _selectedSourceEvents.Count;
        _lvSourceEvents.Invalidate();

        // Update headers list
        _lvHeaders.Items.Clear();
        _lvHeaders.BeginUpdate();
        foreach (var req in source.RequestHeaders)
        {
            var lvi = new ListViewItem("Request");
            lvi.SubItems.Add(req.Key);
            lvi.SubItems.Add(req.Value);
            _lvHeaders.Items.Add(lvi);
        }
        foreach (var resp in source.ResponseHeaders)
        {
            var lvi = new ListViewItem("Response");
            lvi.SubItems.Add(resp.Key);
            lvi.SubItems.Add(resp.Value);
            _lvHeaders.Items.Add(lvi);
        }
        _lvHeaders.EndUpdate();

        // Select first event if present
        if (_selectedSourceEvents.Count > 0)
        {
            DisplayEventParameters(_selectedSourceEvents[0]);
        }
        else
        {
            _txtEventParams.Text = "No events in this source.";
        }
    }

    private void ClearSourceDetails()
    {
        _selectedSource = null;
        _selectedSourceEvents = [];
        _lblDetailTitle.Text = "No source selected";
        _lblDetailBadges.Text = "";
        _lvSourceEvents.VirtualListSize = 0;
        _lvHeaders.Items.Clear();
        _txtEventParams.Text = "";
    }

    private void DisplayEventParameters(NetLogEvent ev)
    {
        if (string.IsNullOrWhiteSpace(ev.RawParamsJson))
        {
            _txtEventParams.Text = $"Event: {ev.EventTypeName} (Type #{ev.EventType})\nPhase: {ev.PhaseName}\nTime: {ev.TimeTicks}\n\n[No parameters recorded for this event]";
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(ev.RawParamsJson);
            string formatted = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            _txtEventParams.Text = $"Event: {ev.EventTypeName} (Type #{ev.EventType})\nPhase: {ev.PhaseName}\nTime: {ev.TimeTicks}\n\n{formatted}";
        }
        catch
        {
            _txtEventParams.Text = ev.RawParamsJson;
        }
    }

    private void JumpToSource(int sourceId)
    {
        _mainTabs.SelectedTab = _tabSources;
        _txtSearch.Text = sourceId.ToString();
    }

    private void LvSources_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= _filteredSources.Count) return;

        var s = _filteredSources[e.ItemIndex];
        var item = new ListViewItem(s.Id.ToString());
        item.SubItems.Add(s.TypeName);
        item.SubItems.Add(s.Method ?? "");
        item.SubItems.Add(s.StatusDisplay);
        item.SubItems.Add($"{s.DurationMs} ms");
        item.SubItems.Add(s.BytesReceived > 0 ? $"{s.BytesReceived:N0} B" : "");
        item.SubItems.Add(s.Description);

        e.Item = item;
    }

    private void LvSources_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_lvSources.SelectedIndices.Count > 0)
        {
            int idx = _lvSources.SelectedIndices[0];
            if (idx >= 0 && idx < _filteredSources.Count)
            {
                SelectSource(_filteredSources[idx]);
            }
        }
    }

    private void LvSources_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (_sortColumn == e.Column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = e.Column;
            _sortAscending = true;
        }

        ApplySourcesFilter();
    }

    private void LvSourceEvents_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= _selectedSourceEvents.Count) return;

        var ev = _selectedSourceEvents[e.ItemIndex];
        var item = new ListViewItem($"+{ev.ElapsedFromStartMs}");
        item.SubItems.Add(ev.TimeTicks.ToString());
        item.SubItems.Add(ev.EventTypeName);
        item.SubItems.Add(ev.PhaseName);
        item.SubItems.Add(ev.Summary ?? "");

        e.Item = item;
    }

    private void LvSourceEvents_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_lvSourceEvents.SelectedIndices.Count > 0)
        {
            int idx = _lvSourceEvents.SelectedIndices[0];
            if (idx >= 0 && idx < _selectedSourceEvents.Count)
            {
                DisplayEventParameters(_selectedSourceEvents[idx]);
            }
        }
    }

    private void LvAllEvents_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= _allFilteredEvents.Count) return;

        var ev = _allFilteredEvents[e.ItemIndex];
        var item = new ListViewItem(ev.SourceId.ToString());
        item.SubItems.Add(ev.TimeTicks.ToString());
        item.SubItems.Add(ev.EventTypeName);
        item.SubItems.Add(ev.PhaseName);
        item.SubItems.Add(ev.Summary ?? "");

        e.Item = item;
    }

    private void ExportFilteredSourcesCsv()
    {
        if (_filteredSources.Count == 0)
        {
            MessageBox.Show(this, "No sources to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Title = "Export Filtered Sources to CSV",
            Filter = "CSV File (*.csv)|*.csv",
            FileName = "netlog_sources_export.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                ExportService.ExportSourcesToCsv(sfd.FileName, _filteredSources);
                MessageBox.Show(this, $"Exported {_filteredSources.Count:N0} sources to {sfd.FileName}", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportSelectedSourceJson()
    {
        if (_selectedSource == null)
        {
            MessageBox.Show(this, "No source selected.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Title = "Export Selected Source to JSON",
            Filter = "JSON File (*.json)|*.json",
            FileName = $"source_{_selectedSource.Id}.json"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                File.WriteAllText(sfd.FileName, ExportService.ExportSourceToJson(_selectedSource));
                MessageBox.Show(this, $"Exported source #{_selectedSource.Id} to {sfd.FileName}", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
        else
            e.Effect = DragDropEffects.None;
    }

    private async void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            string file = files[0];
            if (File.Exists(file))
            {
                await LoadFileAsync(file);
            }
        }
    }

    private void UpdateMemoryUsage()
    {
        long bytes = GC.GetTotalMemory(false);
        double mb = bytes / (1024.0 * 1024.0);
        _statusMemory.Text = $"RAM: {mb:F1} MB";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            ResetFilters();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
            _searchDebounceTimer.Dispose();
            _memoryTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
