namespace MouseMotionInspector.Native;

internal static class GuideReference
{
    internal static void Show(IWin32Window owner)
    {
        using var dialog = Create(); dialog.ShowDialog(owner);
    }
    internal static Form Create()
    {
        var dialog = new Form
        {
            Text = "Help with a result", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new(650, 420), MinimumSize = new(600, 420), AutoScaleMode = AutoScaleMode.Dpi,
            BackColor = AppTheme.Background, ForeColor = AppTheme.Text, Font = new("Segoe UI", 10),
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
        };
        var topics = new (string Name, string Meaning, string Next)[]
        {
            ("The graph is empty", "Live identification updates counters only. A graph needs a timed test and a selected device with recorded movement.", "Wait for settling to finish. Select the row with Test reports. Make a separate test with deliberate movement to confirm capture."),
            ("Travel rises, but net stays near zero", "Travel adds every step. Net is the final displacement. A +1 step followed by −1 gives travel 2 and net 0. Peak is the farthest recorded point from the start.", "Look for reversals in the path and displacement graphs. Repeat hands-off tests before drawing a conclusion about the sensor."),
            ("The cursor moves, but no device does", "Cursor positions, device reports and injected events are separate observations. They can overlap. A cursor change without a device trace leaves its source unresolved.", "Check Injected input and capture warnings. Cursor sampling can miss brief motion; virtual devices and some touchpads may be unattributed. Nearby timestamps do not prove a match."),
            ("There are injected events", "Windows flagged software-injected input. This does not identify the process. Zero flagged events does not rule out software or an unavailable input monitor.", "Check pointer automation you have enabled. Keep injected events separate from raw device totals; they may describe the same movement."),
            ("The test has warnings or dropped reports", "Queue overflow, capture errors or the 250,000-observation limit can make a run incomplete. The stop reason and warnings explain what happened.", "Repeat a shorter test or reduce system load. Do not use an incomplete run as a clean comparison. Partial recordings are still saved."),
            ("Units, graph controls and timing", "Raw relative units, normalized absolute units and screen pixels are different. DPI does not provide a verified conversion to millimetres. App observation times are not USB or sensor timestamps.", "Path: wheel to zoom, drag to pan, Home or double-click to fit. Activity sums travel per bucket. Large traces reduce drawing points while keeping extremes; JSON retains every captured sample. Report count is not polling rate."),
            ("Saving, reopening and sharing", "Tests save in your local application-data folder under MouseMotionInspector\\recordings. Open recordings reopens JSON. CSV contains summaries; Save graph exports a fitted PNG.", "Review device identifiers, screen coordinates, mouse-button flags and notes before sharing. The app makes no automatic network requests and records no keystrokes. Ctrl+Shift+F8 starts/stops globally; Esc stops while focused.")
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(22), ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        Label Text(string text, Color color, bool bold = false) => new() { Text = text, AutoSize = true, Dock = DockStyle.Top, MaximumSize = new(dialog.ClientSize.Width - 44, 0), ForeColor = color, Font = new("Segoe UI", 10, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new(0, 12, 0, 0), UseMnemonic = false };
        var choose = new ThemeComboBox { Dock = DockStyle.Top, AccessibleName = "Help topic", DropDownWidth = 590 };
        choose.Items.AddRange(topics.Select(t => t.Name).ToArray());
        var meaning = Text("", AppTheme.Text); var action = Text("", AppTheme.Text);
        layout.Controls.Add(choose); layout.Controls.Add(Text("What it means", AppTheme.Accent, true)); layout.Controls.Add(meaning);
        layout.Controls.Add(Text("What to do", AppTheme.Accent, true)); layout.Controls.Add(action);
        var close = new ThemeButton { Text = "Close", AutoSize = true, MinimumSize = new(90, 34), Anchor = AnchorStyles.Right, DialogResult = DialogResult.OK };
        layout.Controls.Add(close); dialog.Controls.Add(layout); dialog.AcceptButton = dialog.CancelButton = close;
        layout.SizeChanged += (_, _) =>
        {
            foreach (var label in layout.Controls.OfType<Label>()) label.MaximumSize = new(Math.Max(100, layout.ClientSize.Width - layout.Padding.Horizontal), 0);
        };
        choose.SelectedIndexChanged += (_, _) => { meaning.Text = topics[choose.SelectedIndex].Meaning; action.Text = topics[choose.SelectedIndex].Next; };
        choose.SelectedIndex = 0; return dialog;
    }
}
