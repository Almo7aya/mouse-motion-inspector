using System.Globalization;
using System.Text;
using System.Text.Json;
using MouseMotionInspector.Core;

namespace MouseMotionInspector.Native;

internal sealed class MainForm : Form
{
    private static readonly Color Background = AppTheme.Background, PanelColor = AppTheme.Panel,
        TextColor = AppTheme.Text, Muted = AppTheme.Muted, Accent = AppTheme.Accent;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly CaptureEngine engine = new();
    private UiFrameClock? frameClock;
    private readonly Dictionary<int, DeviceState> devices = [];
    private readonly List<HistoryItem> history = [];
    private readonly List<string> monitorWarnings = [];
    private readonly TextBox surface = new() { Text = "Cloth pad", Width = 170 }, notes = new() { Width = 230 }, alias = new() { Width = 210 };
    private readonly ComboBox dpi = PresetInput("DPI", [400, 800, 1000, 1200, 1600, 2000, 2400, 3200, 6400]),
        polling = PresetInput("Polling rate in Hz", [125, 250, 500, 1000, 2000, 4000, 8000]);
    private readonly NumericUpDown duration = NumberInput(5, 600, 60, 70), settle = NumberInput(3, 60, 5, 60);
    private readonly ComboBox connection = new ThemeComboBox { Width = 105 }, sessions = new ThemeComboBox { Width = 325, DropDownWidth = 650, AccessibleName = "Recording selection" };
    private readonly Button start = Button("Start test", true), stop = Button("Stop & save"), export = Button("Export JSON"), csv = Button("Export summary"), rename = Button("Save label"), open = Button("Open recordings"), saveGraph = Button("Save graph");
    private readonly Label captureStatus = Label("Opening Windows input capture…", 10), status = Label("Move only one mouse or pointing device at a time to identify its row.", 12), result = Label("", 10);
    private readonly Grid deviceGrid = new(), eventGrid = new(), injectionGrid = new();
    private readonly MotionChart trace = new();
    private readonly ComboBox chartKind = new ThemeComboBox { Width = 215 };
    private readonly Label chartMetrics = Label("No recorded movement", 10);
    private (Guid? Run, int? Device, string? Mode) chartContext;
    private readonly PlotSeries chartSeries = new();
    private readonly Dictionary<(int Device, string Mode), DataGridViewRow> deviceRows = [];
    private Guid? gridRunId;
    private (Guid? Run, int Count) reportView = (null, -1), injectionView = (null, -1);
    private (Guid? Run, DeviceInfo? Info, int Warnings) detailsView = (null, null, -1);
    private readonly TextBox deviceDetails = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel setup = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
    private Recording? active;
    private long droppedAtStart;
    private double lastCursorSample, lastReports;
    private bool rendering, saving, draining, stopping, starting, dialogOpen;
    private readonly TraceSelection traceSelection = new();
    private (int Device, string Mode)? selectedKey => traceSelection.Key;
    private Recording? ViewRun => active ?? (sessions.SelectedItem as HistoryItem)?.Run;

    public MainForm()
    {
        Text = "Mouse Motion Inspector"; ClientSize = new(1360, 910); MinimumSize = new(1100, 800);
        AutoScaleMode = AutoScaleMode.Dpi; Font = new("Segoe UI", 10); BackColor = Background; ForeColor = TextColor; KeyPreview = true;
        connection.Items.AddRange(["Wireless", "Wired", "Unknown"]); connection.SelectedIndex = 0;
        sessions.Items.Add("Live identification"); sessions.SelectedIndex = 0;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(22), ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new(SizeType.Absolute, 62)); root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Absolute, 88)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.Absolute, 25));
        var heading = new Panel { Dock = DockStyle.Fill };
        var title = Label("Mouse Motion Inspector", 21); title.AutoSize = true; title.ForeColor = TextColor; title.Location = new(0, 0);
        captureStatus.Location = new(0, 39); captureStatus.AutoSize = true; heading.Controls.AddRange([title, captureStatus]); root.Controls.Add(heading, 0, 0);
        setup.Controls.AddRange([Field("Surface", surface), Field("DPI (select / type)", dpi), Field("Polling Hz (select / type)", polling), Field("Connection", connection), Field("Record (s)", duration), Field("Settle (s)", settle), Field("Notes", notes)]);
        root.Controls.Add(setup, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new(0, 8, 0, 6) };
        actions.Controls.AddRange([start, stop, open, export, csv, sessions]); root.Controls.Add(actions, 0, 2);
        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = PanelColor, Padding = new(12) };
        status.Location = new(12, 10); status.Size = new(1220, 27); status.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; status.ForeColor = TextColor;
        result.Location = new(12, 40); result.Size = new(1220, 41); result.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        statusPanel.Controls.AddRange([status, result]); root.Controls.Add(statusPanel, 0, 3);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8, BackColor = Background };
        split.Panel1.Controls.Add(deviceGrid);
        var tabs = new ThemeTabs { Dock = DockStyle.Fill };
        var graphPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        graphPanel.RowStyles.Add(new(SizeType.AutoSize)); graphPanel.RowStyles.Add(new(SizeType.Absolute, 30)); graphPanel.RowStyles.Add(new(SizeType.Percent, 100));
        var graphTools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        chartKind.Items.AddRange(["Movement path", "Displacement over time", "Activity over time"]); chartKind.SelectedIndex = 0;
        var fit = Button("Fit view");
        var gestureHint = Label("Path: wheel to zoom · drag to pan · double-click to fit", 9); gestureHint.AutoSize = true; gestureHint.Margin = new(8, 10, 0, 0);
        graphTools.Controls.AddRange([chartKind, fit, saveGraph, gestureHint]);
        chartMetrics.Dock = DockStyle.Fill; chartMetrics.ForeColor = Accent;
        graphPanel.Controls.Add(graphTools, 0, 0); graphPanel.Controls.Add(chartMetrics, 0, 1); graphPanel.Controls.Add(trace, 0, 2);
        tabs.AddPage("Movement graphs", graphPanel);
        tabs.AddPage("Device reports", eventGrid); tabs.AddPage("Injected input", injectionGrid);
        var detailPanel = new Panel { Dock = DockStyle.Fill };
        var labelControls = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 43, Padding = new(4) }; labelControls.Controls.AddRange([alias, rename]);
        detailPanel.Controls.Add(deviceDetails); detailPanel.Controls.Add(labelControls); tabs.AddPage("Device identity", detailPanel);
        tabs.AddPage("Interpretation guide", InterpretationGuide.Create(() => tabs.ShowPage(0)));
        tabs.AddPage("About", AboutPage.Create()); split.Panel2.Controls.Add(tabs); root.Controls.Add(split, 0, 4);
        var footer = Label("Ctrl+Shift+F8: start / stop globally  •  Esc: stop while focused  •  Raw input continues when another app is focused  •  No keyboard recording", 9);
        footer.Dock = DockStyle.Fill; root.Controls.Add(footer, 0, 5); Controls.Add(root);
        Columns(deviceGrid, ("Id", 48), ("Device / verified label", 245), ("VID:PID", 100), ("State", 100), ("Live reports", 100), ("Test reports", 100), ("Total travel", 100), ("Net X", 72), ("Net Y", 72), ("Net direction", 110), ("Units / coordinate mode", 240));
        Columns(eventGrid, ("Time (s)", 105), ("Device", 70), ("ΔX", 90), ("ΔY", 90), ("X from start", 125), ("Y from start", 125), ("Direction", 120), ("Mode", 240));
        Columns(injectionGrid, ("Time (s)", 110), ("Screen X", 110), ("Screen Y", 110), ("Flags", 110), ("Meaning", 670));
        Theme(root); trace.BackColor = Background;
        start.Click += (_, _) => StartTest(); stop.Click += (_, _) => StopTest("Stopped by user");
        sessions.SelectedIndexChanged += (_, _) => RefreshView();
        deviceGrid.SelectionChanged += (_, _) => SelectDeviceRow();
        // Clicking the already-selected automatic row is also an explicit choice.
        deviceGrid.CellClick += (_, e) => { if (e.RowIndex >= 0) SelectDeviceRow(); };
        rename.Click += (_, _) => RenameSelected();
        export.Click += async (_, _) => await ExportAll(); csv.Click += (_, _) => ExportSummary();
        open.Click += async (_, _) => await OpenRecordings();
        chartKind.SelectedIndexChanged += (_, _) => { trace.Kind = (ChartKind)chartKind.SelectedIndex; trace.Fit(); };
        fit.Click += (_, _) => trace.Fit(); saveGraph.Click += (_, _) => SaveGraph();
        trace.VisibleChanged += (_, _) => { if (trace.Visible) RefreshChart(); };
        eventGrid.VisibleChanged += (_, _) => { if (eventGrid.Visible) RefreshReportTables(true); };
        injectionGrid.VisibleChanged += (_, _) => { if (injectionGrid.Visible) RefreshReportTables(true); };
        deviceDetails.VisibleChanged += (_, _) => { if (deviceDetails.Visible) RefreshDetails(); };
        Shown += (_, _) =>
        {
            split.Panel1MinSize = 130; split.Panel2MinSize = 260; split.SplitterDistance = Math.Max(150, split.Height / 3);
            RefreshView(); frameClock = new UiFrameClock(this, Tick);
        };
        Resize += (_, _) => frameClock?.SetMinimized(WindowState == FormWindowState.Minimized);
        FormClosing += (_, e) =>
        {
            if (saving) { e.Cancel = true; status.Text = "Wait for the recording to finish saving before closing."; return; }
            if (active is not null) { e.Cancel = true; StopTest("Window closed during test"); return; }
            if (history.Any(h => h.SavePath is null) && MessageBox.Show(this, "Some recordings have not been saved. Close without exporting them?", "Unsaved recordings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) e.Cancel = true;
        };
        FormClosed += (_, _) => { frameClock?.Dispose(); engine.Dispose(); };
    }

    private static NumericUpDown NumberInput(int min, int max, int value, int width) => new() { Minimum = min, Maximum = max, Value = value, Width = width, BorderStyle = BorderStyle.FixedSingle };
    private static ComboBox PresetInput(string name, int[] presets)
    {
        var input = new ThemeComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown, Width = 130, MaxDropDownItems = 12,
            AccessibleName = name, AccessibleDescription = "Select a preset or type a custom whole number. Unknown or 0 means unspecified."
        };
        input.Items.Add("Unknown");
        foreach (var value in presets) input.Items.Add(value.ToString(CultureInfo.InvariantCulture));
        input.SelectedIndex = 0;
        return input;
    }
    private bool TryReadPreset(ComboBox input, string name, int maximum, out int? value)
    {
        value = null;
        var text = input.Text.Trim();
        if (text.Length == 0 || text.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) return true;
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 0 && number <= maximum)
        {
            value = number == 0 ? null : number;
            return true;
        }
        MessageBox.Show(this, $"Select a {name} preset or type a whole number from 1 to {maximum:N0}.\nUse Unknown or 0 if you do not know the setting.",
            $"Invalid {name}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        input.Focus(); input.SelectAll();
        return false;
    }
    private static void RememberPreset(ComboBox input, int? value)
    {
        var text = value?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
        if (value is int number && !input.Items.Contains(text))
        {
            var index = 1;
            while (index < input.Items.Count && int.Parse((string)input.Items[index]!, CultureInfo.InvariantCulture) < number) index++;
            input.Items.Insert(index, text);
        }
        input.SelectedItem = text;
    }
    private static Label Label(string text, float size) => new() { Text = text, Font = new("Segoe UI", size), ForeColor = Muted };
    private static Button Button(string text, bool accent = false) => new ThemeButton { Text = text, Primary = accent, AutoSize = true, MinimumSize = new(0, 34), Padding = new(10, 2, 10, 2), Margin = new(0, 2, 8, 2) };
    private static Control Field(string text, Control control)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new(0, 2, 14, 6) };
        var label = Label(text, 9); label.AutoSize = true; panel.Controls.AddRange([label, control]); return panel;
    }
    private static void Theme(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is TextBox or ComboBox or NumericUpDown) { c.BackColor = AppTheme.Input; c.ForeColor = TextColor; }
            if (c is TextBox t) t.BorderStyle = BorderStyle.FixedSingle;
            Theme(c);
        }
    }
    private static void Columns(Grid grid, params (string Name, int Width)[] columns)
    {
        foreach (var (name, width) in columns) grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable });
    }
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private DeviceInfo Device(int id) => devices.TryGetValue(id, out var state) ? state.Info : new(id, "", "", $"Unresolved interface {id}", "", "", "", "Identity could not be resolved.");
    private void Drain(double budgetMs = double.PositiveInfinity)
    {
        if (draining) return; draining = true;
        try
        {
            var deadline = CaptureEngine.Now + budgetMs;
            for (var i = 0; i < 25_000 && engine.TryRead(out var packet); i++)
            {
                if (packet.Kind == PacketKind.Arrival)
                {
                    DeviceInfo info;
                    try { info = Win.Describe(packet.Device, new nint(packet.Handle)); }
                    catch (Exception e) { info = new(packet.Device, $"0x{packet.Handle:X}", "", $"Unresolved interface {packet.Device}", "", "", "", e.Message); }
                    devices[packet.Device] = new(info); active?.Remember(info);
                }
                else if (packet.Kind == PacketKind.Removal && devices.TryGetValue(packet.Device, out var removed)) removed.Connected = false;
                else if (packet.Kind == PacketKind.Raw)
                {
                    if (!devices.TryGetValue(packet.Device, out var state))
                    {
                        var info = Device(packet.Device); state = new(info); devices.Add(packet.Device, state);
                        active?.Remember(info);
                    }
                    state.Observe(packet);
                }
                else if (packet.Kind == PacketKind.Error)
                {
                    var warning = packet.Detail ?? "Unknown capture error"; monitorWarnings.Add(warning); active?.Warnings.Add(warning);
                }
                else if (packet.Kind == PacketKind.Hotkey)
                {
                    if (active is null) StartTest(); else StopTest("Stopped with Ctrl+Shift+F8", packet.Time);
                }
                active?.Accept(packet);
                if ((i & 127) == 127 && CaptureEngine.Now >= deadline) break;
            }
        }
        finally { draining = false; }
    }
    private void Tick()
    {
        Drain(3); var now = CaptureEngine.Now;
        if (active is not null)
        {
            if (now - lastCursorSample >= 50)
            {
                lastCursorSample = now;
                if (Win.GetCursorPos(out var point)) active.ObserveCursor(now, point.X, point.Y);
            }
            if (engine.FatalError is not null) StopTest("Input listener failed; recording incomplete", now);
            else if (engine.Dropped != droppedAtStart) StopTest("Capture queue overflow; recording incomplete", now);
            else if (active.AtLimit) StopTest("250,000-observation limit reached", now);
            else if (now >= active.End) StopTest("Timer complete", active.End);
        }
        if (WindowState != FormWindowState.Minimized)
        {
            RefreshView();
            // Paint within this frame instead of letting queued input delay WM_PAINT.
            if (trace.Visible) trace.Update();
        }
    }
    private void StartTest()
    {
        if (starting || dialogOpen) return;
        starting = true;
        try { StartTestCore(); }
        finally { starting = false; }
    }
    private void StartTestCore()
    {
        if (active is not null || saving || !engine.Ready || engine.FatalError is not null) return;
        if (string.IsNullOrWhiteSpace(surface.Text)) { MessageBox.Show(this, "Enter a surface name first."); return; }
        if (!TryReadPreset(dpi, "DPI", 100000, out var dpiValue) ||
            !TryReadPreset(polling, "polling rate", 32000, out var pollingValue)) return;
        RememberPreset(dpi, dpiValue); RememberPreset(polling, pollingValue);
        Drain();
        // Draining can dispatch a queued global start/stop shortcut.
        if (active is not null || saving) return;
        if (history.Count >= 100 || history.Sum(h => (long)h.Run.Movements.Count + h.Run.CursorChanges.Count + h.Run.InjectedMoves.Count + h.Run.DeviceChanges.Count) > 1_000_000 - Recording.SampleLimit)
        { MessageBox.Show(this, "This session has reached its recording limit. Export your recordings and restart the app before another test.", "Session limit"); return; }
        var settings = new TestSettings(surface.Text.Trim(), dpiValue, connection.Text,
            pollingValue, notes.Text.Trim(), (int)duration.Value, (int)settle.Value);
        var now = CaptureEngine.Now;
        active = new(settings, now + settings.SettleSeconds * 1000, DateTimeOffset.Now.AddSeconds(settings.SettleSeconds)) { InjectionMonitorInstalled = engine.HookInstalled };
        foreach (var state in devices.Values.Where(d => d.Connected)) active.Remember(state.Info);
        active.Warnings.AddRange(monitorWarnings);
        if (!engine.HookInstalled) active.Warnings.Add("Injected-event monitor is not installed; zero injected observations has no diagnostic meaning.");
        if (Win.GetCursorPos(out var point)) active.ObserveCursor(now, point.X, point.Y);
        droppedAtStart = engine.Dropped; sessions.SelectedIndex = 0; RefreshView();
    }
    private async void StopTest(string reason, double? time = null)
    {
        if (active is null || stopping) return;
        stopping = true;
        var at = time ?? CaptureEngine.Now;
        active.RequestStop(at);
        Drain();
        // Drain already-observed input up to the cutoff. New live input after the
        // cutoff must not keep a timed recording open indefinitely.
        if (!draining)
            for (var pass = 0; pass < 8 && engine.HasPendingBefore(at); pass++) Drain();
        if (active is null) { stopping = false; return; }
        var run = active; active = null;
        if (at < run.Start) { stopping = false; status.Text = "Settling cancelled. No test was recorded."; RefreshView(); return; }
        var dropped = engine.Dropped - droppedAtStart;
        if (dropped > 0) run.Warnings.Add($"{dropped} observations were dropped because the capture queue filled. This recording is incomplete.");
        if (engine.HasPendingBefore(at)) { run.Warnings.Add("Queued input before the stop time could not all be consumed. This recording is incomplete."); reason = "Stopped with a capture backlog"; }
        run.Finish(at, reason); var item = new HistoryItem(run); history.Add(item); sessions.Items.Add(item); sessions.SelectedItem = item;
        saving = true; stopping = false; RefreshView();
        try
        {
            var folder = AppPaths.Recordings;
            var path = Path.Combine(folder, $"mouse-{run.StartedAt:yyyyMMdd-HHmmss}-{run.Id.ToString("N")[..8]}.json");
            await Task.Run(() => RecordingFiles.WriteAtomic(path, JsonSerializer.Serialize(run, JsonOptions)));
            item.SavePath = path;
        }
        catch (Exception e) { run.Warnings.Add($"Automatic save failed: {e.Message}"); MessageBox.Show(this, "Automatic save failed. The recording is still in memory. Use Export all JSON before closing.\n\n" + e.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { saving = false; RefreshView(); }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && active is not null) { StopTest("Stopped with Escape"); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void RefreshView()
    {
        if (IsDisposed) return;
        var now = CaptureEngine.Now; var run = ViewRun;
        traceSelection.SetContext(run?.Id);
        start.Enabled = active is null && !saving && engine.Ready && engine.FatalError is null; stop.Enabled = active is not null;
        setup.Enabled = active is null && !saving; sessions.Enabled = active is null && !saving;
        open.Enabled = active is null && !saving;
        export.Enabled = csv.Enabled = history.Count > 0 && active is null && !saving;
        captureStatus.Text = engine.FatalError is not null ? "Capture failed: " + engine.FatalError : !engine.Ready ? "Opening Windows capture…" :
            $"Windows Raw Input active • Injected-event monitor {(engine.HookInstalled ? "installed" : "unavailable")} • Global shortcut {(engine.HotkeyInstalled ? "ready" : "unavailable")} • Dropped: {engine.Dropped}";
        if (active is not null)
        {
            status.Text = now < active.Start ? $"Settling: {Math.Max(0, (active.Start - now) / 1000):0.0} s — take your hand off the mouse." :
                $"Recording: {Math.Max(0, (active.End - now) / 1000):0.0} s left — {active.Settings.Surface} • {active.Settings.Dpi?.ToString() ?? "unknown"} DPI";
        }
        else if (run is not null) status.Text = $"{run.Settings.Surface} • {run.Settings.Dpi?.ToString() ?? "unknown"} DPI • {run.RecordedSeconds:0.0} s • {run.StopReason}{(saving ? " • Saving…" : "")}";
        else status.Text = "Live identification: move only one physical device at a time, then label the row that changes.";
        if (run is not null)
        {
            var moving = run.Streams.Select(s => s.Device).Distinct().ToArray();
            var summary = $"{run.Movements.Count:N0} device movement events from {moving.Length} interface(s) • {run.InjectedMoves.Count:N0} Windows-flagged injected moves • {run.CursorChanges.Count:N0} cursor-position changes sampled";
            result.Text = summary + (run.Warnings.Count > 0 ? "\nWarning: " + run.Warnings[^1] : run.Movements.Count == 0 && run.CursorChanges.Count > 0 ? "\nCursor movement has no recorded raw device motion in this run; source is unresolved. Check the Injected input tab." : "\nSelect a device row to inspect its graphs, identity and direction. Screen and raw movement are separate observations.");
        }
        else result.Text = monitorWarnings.Count > 0 ? monitorWarnings[^1] : "Identify a device, then compare hands-off tests. Recordings save locally; open the Interpretation guide for the workflow and limits.";

        rendering = true;
        try
        {
            if (gridRunId != run?.Id) { gridRunId = run?.Id; deviceGrid.Rows.Clear(); deviceRows.Clear(); }
            var traceRows = new List<TraceRow>();
            var identities = run?.Devices.OrderBy(d => d.Id) ?? devices.Values.Select(d => d.Info).OrderBy(d => d.Id);
            foreach (var info in identities)
            {
                devices.TryGetValue(info.Id, out var state);
                var streams = run?.Streams.Where(s => s.Device == info.Id).ToList() ?? [];
                if (streams.Count == 0) streams.Add(new MotionStats { Device = info.Id });
                foreach (var stream in streams)
                {
                    var key = (info.Id, stream.Mode);
                    if (!deviceRows.TryGetValue(key, out var row))
                    {
                        row = deviceGrid.Rows[deviceGrid.Rows.Add()]; row.Tag = key; deviceRows.Add(key, row);
                    }
                    object[] values = [$"D{info.Id}", info.DisplayName, info.VidPid, run?.Finished == true ? "In recording" : state?.Connected == true ? "Connected" : "Removed",
                        run?.Finished == true ? "—" : state?.Moves ?? 0, run is null ? "—" : stream.Events, run is null ? "—" : F(stream.Travel), run is null ? "—" : stream.X, run is null ? "—" : stream.Y,
                        run is null ? "—" : stream.Direction, run is null ? "Awaiting test" : stream.Unit + " / " + stream.Mode];
                    SetCells(row, values);
                    traceRows.Add(new(key, run is null ? state?.Moves ?? 0 : stream.Events));
                    var color = run?.Finished != true && state is not null && now - state.LastMove < 700 ? Accent : TextColor;
                    if (row.DefaultCellStyle.ForeColor != color) row.DefaultCellStyle.ForeColor = color;
                }
            }
            var keys = traceRows.Select(row => row.Key).ToHashSet();
            foreach (var key in deviceRows.Keys.Where(key => !keys.Contains(key)).ToArray())
            { deviceGrid.Rows.Remove(deviceRows[key]); deviceRows.Remove(key); }
            traceSelection.Update(traceRows);
            var selectedRow = selectedKey is { } selected ? deviceRows.GetValueOrDefault(selected) : null;
            if (deviceGrid.CurrentCell?.OwningRow != selectedRow) deviceGrid.CurrentCell = selectedRow?.Cells[0];
            foreach (var row in deviceRows.Values)
                if (row.Selected != (row == selectedRow)) row.Selected = row == selectedRow;
        }
        finally { rendering = false; }
        RefreshReportTables();
        RefreshDetails(); RefreshChart();
    }
    private static void SetCells(DataGridViewRow row, object[] values)
    {
        for (var i = 0; i < values.Length; i++)
            if (!Equals(row.Cells[i].Value, values[i])) row.Cells[i].Value = values[i];
    }
    private void RefreshReportTables(bool force = false)
    {
        var now = CaptureEngine.Now;
        if (!force && now - lastReports < 100) return;
        lastReports = now; var run = ViewRun;
        var reports = (run?.Id, run?.Movements.Count ?? 0);
        if (eventGrid.Visible && (force || reportView != reports))
        {
            reportView = reports; var count = Math.Min(50, reports.Item2);
            if (eventGrid.RowCount != count) eventGrid.RowCount = count;
            for (var i = 0; i < count; i++)
            {
                var move = run!.Movements[run.Movements.Count - 1 - i];
                SetCells(eventGrid.Rows[i], [F(move.TimeMs / 1000), $"D{move.Device}", move.Dx, move.Dy, move.X, move.Y, Directions.Describe(move.Dx, move.Dy), move.Mode]);
            }
        }
        var injections = (run?.Id, run?.InjectedMoves.Count ?? 0);
        if (injectionGrid.Visible && (force || injectionView != injections))
        {
            injectionView = injections; var count = Math.Min(50, injections.Item2);
            if (injectionGrid.RowCount != count) injectionGrid.RowCount = count;
            for (var i = 0; i < count; i++)
            {
                var move = run!.InjectedMoves[run.InjectedMoves.Count - 1 - i];
                SetCells(injectionGrid.Rows[i], [F(move.TimeMs / 1000), move.ScreenX, move.ScreenY, $"0x{move.Flags:X}",
                    (move.Flags & 2) != 0 ? "Injected from a lower-integrity process; process identity unavailable" : "Windows marked this move as injected; process identity unavailable"]);
            }
        }
    }
    private void SelectDeviceRow()
    {
        if (rendering || deviceGrid.SelectedRows.Count == 0 || deviceGrid.SelectedRows[0].Tag is not ValueTuple<int, string> key) return;
        traceSelection.Select(key);
        RefreshDetails(); RefreshChart();
    }
    private void RefreshDetails()
    {
        if (!deviceDetails.Visible) return;
        var run = ViewRun;
        var info = selectedKey is null ? null : run?.Devices.FirstOrDefault(d => d.Id == selectedKey.Value.Device) ?? Device(selectedKey.Value.Device);
        rename.Enabled = alias.Enabled = info is not null && run is null && !saving;
        var view = (run?.Id, info, run?.Warnings.Count ?? 0);
        if (detailsView == view) return;
        detailsView = view;
        if (info is null) { deviceDetails.Text = "No device selected."; return; }
        var text = $"D{info.Id}  {info.DisplayName}\r\nReported name: {info.Name}\r\nManufacturer: {info.Manufacturer}\r\nVID:PID: {info.VidPid}\r\nCapture handle: {info.Handle}\r\nPnP instance: {info.InstanceId}\r\nDevice interface: {info.Path}\r\n\r\n{info.IdentificationNote}\r\n\r\nLabels are your physical-device identification, not an automatic hardware diagnosis.";
        if (run is not null) text += $"\r\n\r\nSurface: {run.Settings.Surface}\r\nDPI label: {run.Settings.Dpi}\r\nPolling label: {run.Settings.PollingHz}\r\nConnection: {run.Settings.Connection}\r\nNotes: {run.Settings.Notes}\r\n\r\n" + string.Join("\r\n", run.Warnings);
        if (deviceDetails.Text != text) deviceDetails.Text = text;
    }
    private void RenameSelected()
    {
        if (ViewRun is not null || selectedKey is null || !devices.TryGetValue(selectedKey.Value.Device, out var state)) return;
        state.Info = state.Info with { Alias = alias.Text.Trim() }; RefreshView();
    }
    private void RefreshChart()
    {
        if (!trace.Visible) return;
        var run = ViewRun;
        var context = (run?.Id, selectedKey?.Device, selectedKey?.Mode);
        var contextChanged = chartContext != context;
        if (chartContext != context) { chartContext = context; trace.Fit(); }
        var changed = chartSeries.Sync(run?.Movements ?? (IReadOnlyList<Movement>)Array.Empty<Movement>(), selectedKey?.Device ?? -1, selectedKey?.Mode ?? "relative");
        var moves = chartSeries.Moves;
        var info = selectedKey is { } key ? run?.Devices.FirstOrDefault(d => d.Id == key.Device) ?? Device(key.Device) : null;
        var title = info is null ? "No device selected" : $"D{info.Id} · {info.DisplayName} · {selectedKey?.Mode}";
        var empty = "No movement recorded for this interface.";
        if (run is null) empty = "Live identification · Start a timed test to plot movement.";
        else if (active is not null && CaptureEngine.Now < active.Start) empty = "Settling · Plotting begins after the countdown.";
        else if (run.Streams.OrderByDescending(s => s.Events).FirstOrDefault() is { Events: > 0 } moving)
            empty += $" Select D{moving.Device} ({moving.Mode}) for its {moving.Events:N0} reports.";
        else if (active is not null) empty = "Recording · Waiting for device movement.";
        var seconds = run is null ? 1 : run.Finished ? run.RecordedSeconds : Math.Clamp((CaptureEngine.Now - run.Start) / 1000, 0, run.Settings.DurationSeconds);
        var stat = run?.Streams.FirstOrDefault(s => selectedKey == (s.Device, s.Mode));
        chartMetrics.Text = stat is null ? "No recorded movement for this selection" :
            $"{stat.Events:N0} reports    ·    Travel {F(stat.Travel)}    ·    Net X {stat.X} / Y {stat.Y}    ·    Peak {F(stat.Peak)}    ·    {stat.Direction}";
        var prior = trace.Snapshot;
        trace.Snapshot = new(title, empty, chartSeries, seconds, stat?.Unit ?? "raw relative units");
        trace.AccessibleDescription = title + ". " + (moves.Count == 0 ? empty : chartMetrics.Text);
        saveGraph.Enabled = moves.Count > 0 && active is null && !saving;
        if (changed || contextChanged || prior.Title != title || prior.EmptyMessage != empty || (trace.Kind != ChartKind.Path && prior.Seconds != seconds)) trace.Invalidate();
    }
    private DialogResult ShowFileDialog(CommonDialog dialog)
    {
        dialogOpen = true;
        try { return dialog.ShowDialog(this); }
        finally { dialogOpen = false; }
    }
    private async Task OpenRecordings()
    {
        using var dialog = new OpenFileDialog { Filter = "Recording JSON (*.json)|*.json", Multiselect = true, Title = "Open saved recordings" };
        if (Directory.Exists(AppPaths.Recordings)) dialog.InitialDirectory = AppPaths.Recordings;
        if (ShowFileDialog(dialog) != DialogResult.OK) return;
        saving = true; RefreshView();
        try
        {
            var loaded = await Task.Run(() =>
            {
                var list = new List<(Recording Run, string Path)>(); long observations = 0;
                foreach (var path in dialog.FileNames)
                    foreach (var run in RecordingFiles.Load(path))
                    {
                        observations += run.Movements.Count + run.CursorChanges.Count + run.InjectedMoves.Count + run.DeviceChanges.Count;
                        if (list.Count >= 100 || observations > 1_000_000) throw new InvalidDataException("Open at most 100 runs and 1,000,000 observations per session.");
                        list.Add((run, path));
                    }
                return list;
            });
            var additions = loaded.Where(item => history.All(h => h.Run.Id != item.Run.Id)).DistinctBy(item => item.Run.Id).ToArray();
            if (history.Count + additions.Length > 100 || history.Sum(h => (long)h.Run.Movements.Count + h.Run.CursorChanges.Count + h.Run.InjectedMoves.Count + h.Run.DeviceChanges.Count)
                + additions.Sum(h => (long)h.Run.Movements.Count + h.Run.CursorChanges.Count + h.Run.InjectedMoves.Count + h.Run.DeviceChanges.Count) > 1_000_000)
                throw new InvalidDataException("Session limit reached. Export your current recordings and restart before opening more.");
            foreach (var (run, path) in additions) { var item = new HistoryItem(run) { SavePath = path }; history.Add(item); sessions.Items.Add(item); }
            var last = loaded.LastOrDefault();
            if (last.Run is not null) sessions.SelectedItem = history.First(h => h.Run.Id == last.Run.Id);
        }
        catch (Exception e) { MessageBox.Show(this, "Could not open the recordings. Existing tests are unchanged.\n\n" + e.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { saving = false; RefreshView(); }
    }
    private void SaveGraph()
    {
        using var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = $"mouse-motion-{trace.Kind.ToString().ToLowerInvariant()}-{DateTime.Now:yyyyMMdd-HHmmss}.png" };
        if (ShowFileDialog(dialog) != DialogResult.OK) return;
        try { trace.Export(dialog.FileName); }
        catch (Exception e) { MessageBox.Show(this, e.Message, "Graph export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private async Task ExportAll()
    {
        using var dialog = new SaveFileDialog { Filter = "JSON recordings (*.json)|*.json", FileName = $"mouse-device-tests-{DateTime.Now:yyyyMMdd-HHmmss}.json", InitialDirectory = Directory.Exists(AppPaths.Recordings) ? AppPaths.Recordings : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        if (ShowFileDialog(dialog) != DialogResult.OK) return;
        saving = true; RefreshView();
        try
        {
            var runs = history.Select(h => h.Run).ToArray();
            await Task.Run(() => RecordingFiles.WriteAtomic(dialog.FileName, JsonSerializer.Serialize(new { App = "Mouse Motion Inspector", Version = 1, ExportedAt = DateTimeOffset.Now, Runs = runs }, JsonOptions)));
            foreach (var item in history) item.SavePath ??= dialog.FileName;
            MessageBox.Show(this, "Saved all recordings:\n" + dialog.FileName, "Export complete");
        }
        catch (Exception e) { MessageBox.Show(this, e.Message, "Export failed"); }
        finally { saving = false; RefreshView(); }
    }
    private void ExportSummary()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV summary (*.csv)|*.csv", FileName = $"mouse-device-summary-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (ShowFileDialog(dialog) != DialogResult.OK) return;
        try { RecordingFiles.WriteAtomic(dialog.FileName, "\uFEFF" + Exports.SummaryCsv(history.Select(h => h.Run))); }
        catch (Exception e) { MessageBox.Show(this, e.Message, "Export failed"); }
    }
    private sealed class DeviceState(DeviceInfo info)
    {
        public DeviceInfo Info = info;
        public bool Connected = true;
        public long Moves;
        public double LastMove;
        private (int X, int Y, ushort Mode)? previous;
        public void Observe(Packet p)
        {
            var moved = p.X != 0 || p.Y != 0;
            if ((p.Flags & 1) != 0) { moved = previous is not null && previous.Value.Mode == (p.Flags & 3) && (p.X != previous.Value.X || p.Y != previous.Value.Y); previous = (p.X, p.Y, (ushort)(p.Flags & 3)); }
            else previous = null;
            if (moved) { Moves++; LastMove = p.Time; }
        }
    }
    private sealed class HistoryItem(Recording run)
    {
        public Recording Run { get; } = run;
        public string? SavePath;
        public override string ToString() => $"{Run.StartedAt:yyyy-MM-dd HH:mm:ss} · {Run.Settings.Surface} · {Run.Settings.Dpi?.ToString() ?? "?"} DPI · {Run.RecordedSeconds:0.#} s · {(Run.Complete ? "Complete" : "Partial")}";
    }
    private sealed class Grid : DataGridView
    {
        public Grid()
        {
            DoubleBuffered = true; Dock = DockStyle.Fill; ReadOnly = true; AllowUserToAddRows = AllowUserToDeleteRows = false;
            AllowUserToResizeRows = false; RowHeadersVisible = false; MultiSelect = false; SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            BackgroundColor = PanelColor; BorderStyle = BorderStyle.None; GridColor = AppTheme.Border; EnableHeadersVisualStyles = false;
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            DefaultCellStyle = new() { BackColor = PanelColor, ForeColor = TextColor, SelectionBackColor = AppTheme.Selection, SelectionForeColor = TextColor, Padding = new(5, 0, 5, 0) };
            AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 31, 38);
            ColumnHeadersDefaultCellStyle = new() { BackColor = Background, ForeColor = Muted, SelectionBackColor = Background, SelectionForeColor = TextColor, Font = new("Segoe UI", 9), Padding = new(5, 0, 5, 0) };
            RowTemplate.Height = 28; ColumnHeadersHeight = 32;
        }
    }
}
