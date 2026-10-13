using System.Drawing.Drawing2D;

namespace MouseMotionInspector.Native;

internal sealed class GuidePicture : Control
{
    private int step, example = 1;
    internal GuidePicture()
    {
        Dock = DockStyle.Top; Height = 190; Margin = Padding.Empty; DoubleBuffered = true;
        AccessibleRole = AccessibleRole.Graphic; AccessibleName = "Illustrative example, not live recording data";
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    internal void SetPage(int value) { step = value; Describe(); Invalidate(); }
    internal void SetExample(int value) { example = value; Describe(); Invalidate(); }
    private void Describe() => AccessibleDescription = step switch
    {
        0 => "Example: Device B's movement counter increases. A and C stay at zero. Verify B by moving only that physical device.",
        1 => "Suggested test: 5 seconds settling, then 60 seconds hands off. Repeat 2 or 3 times.",
        2 => example switch { 0 => "Quiet example: travel zero, net X zero, peak zero.", 1 => "Back-and-forth example: travel six, net X zero, peak one raw unit.", _ => "Drift example: travel six, net X plus six, peak six raw units." },
        _ => "Compare repeated tests on surface A and surface B. Keep DPI, connection, polling rate and duration fixed."
    };
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.Clear(AppTheme.Input); g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Height / 210f; var w = Width / scale; var h = Height / scale;
        using var small = new Font("Segoe UI", 9); using var large = new Font("Segoe UI", 20, FontStyle.Bold);
        using var pen = new Pen(AppTheme.Border); using var accent = new Pen(AppTheme.Accent, 2); using var cyan = new Pen(AppTheme.Cyan, 2);
        void Text(string text, float x, float y, float width, float height, Color color, Font? font = null, bool center = false) =>
            TextRenderer.DrawText(g, text, font ?? small, new Rectangle((int)(x * scale), (int)(y * scale), (int)(width * scale), (int)(height * scale)), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | (center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
        void Box(float x, float y, float width, float height, Color color)
        { using var brush = new SolidBrush(color); g.FillRectangle(brush, x * scale, y * scale, width * scale, height * scale); }
        void Line(Pen p, float x, float y, float x2, float y2) => g.DrawLine(p, x * scale, y * scale, x2 * scale, y2 * scale);
        Text(step == 2 ? "ILLUSTRATION · NOT YOUR RECORDING" : step == 1 ? "A REPEATABLE TEST" : step == 3 ? "A FAIR COMPARISON" : "EXAMPLE · WATCH THE COUNTER", 16, 5, w - 32, 27, AppTheme.Muted);
        if (step == 0)
        {
            for (var i = 0; i < 3; i++)
            {
                var y = 38 + i * 42; Box(16, y, w - 32, 35, i == 1 ? AppTheme.Selection : AppTheme.Panel);
                Text($"Device {(char)('A' + i)}", 28, y, w * .45f, 35, i == 1 ? AppTheme.Accent : AppTheme.Muted);
                Text(i == 1 ? "24 → 31 reports" : "0 reports", w * .5f, y, w * .5f - 28, 35, i == 1 ? AppTheme.Accent : AppTheme.Muted);
            }
            Text("Counter rises → select that row → verify its device", 16, 173, w - 32, 28, AppTheme.Cyan);
        }
        else if (step == 1)
        {
            var cell = (w - 48) / 3;
            string[] values = ["5 s", "60 s", "2–3×"], labels = ["Settle", "Hands off", "Repeat"];
            for (var i = 0; i < 3; i++)
            {
                var x = 16 + i * (cell + 8); Box(x, 47, cell, 103, AppTheme.Panel);
                Text(values[i], x, 54, cell, 48, i == 1 ? AppTheme.Accent : AppTheme.Cyan, large, true);
                Text(labels[i], x, 104, cell, 30, AppTheme.Text, center: true);
            }
            Text("Start test → release the mouse → leave the desk still", 16, 164, w - 32, 35, AppTheme.Muted);
        }
        else if (step == 2)
        {
            int[] values = example switch { 0 => [0, 0, 0, 0, 0, 0, 0], 1 => [0, 1, 0, -1, 0, 1, 0], _ => [0, 1, 2, 3, 4, 5, 6] };
            var cell = (w - 48) / 3;
            for (var chart = 0; chart < 3; chart++)
            {
                var x = 16 + chart * (cell + 8); Text(new[] { "Path", "X over time", "Activity" }[chart], x, 31, cell, 26, AppTheme.Text);
                Box(x, 60, cell, 84, AppTheme.Panel);
                for (var row = 1; row < 4; row++) Line(pen, x, 60 + row * 21, x + cell, 60 + row * 21);
                float X(int i) => x + 9 + i / 6f * (cell - 18);
                float Y(int value) => 128 - value * 9;
                if (chart == 0)
                {
                    var origin = x + cell / 2; var unit = (cell - 20) / 14;
                    for (var i = 1; i < values.Length; i++) Line(accent, origin + values[i - 1] * unit, 102, origin + values[i] * unit, 102);
                    g.DrawEllipse(cyan, (origin - 5) * scale, 97 * scale, 10 * scale, 10 * scale);
                    using var brush = new SolidBrush(AppTheme.Accent); g.FillEllipse(brush, (origin + values[^1] * unit - 2.5f) * scale, 99.5f * scale, 5 * scale, 5 * scale);
                }
                else if (chart == 1)
                {
                    for (var i = 1; i < values.Length; i++) { Line(accent, X(i - 1), Y(values[i - 1]), X(i), Y(values[i - 1])); Line(accent, X(i), Y(values[i - 1]), X(i), Y(values[i])); }
                }
                else
                {
                    for (var i = 1; i < values.Length; i++) Box(x + 8 + (i - 1) * (cell - 16) / 6, 140 - Math.Abs(values[i] - values[i - 1]) * 55, (cell - 16) / 6 - 3, Math.Abs(values[i] - values[i - 1]) * 55, AppTheme.Accent);
                }
            }
            var travel = values.Zip(values.Skip(1)).Sum(pair => Math.Abs(pair.Second - pair.First));
            Text($"Travel {travel}     Net X {values[^1]}     Peak {values.Max(Math.Abs)}", 16, 146, w - 32, 28, AppTheme.Accent);
            Text(example switch { 0 => "No movement in this example. No activity bars.", 1 => "It moved six units in total, then returned to the start.", _ => "Each step went right. Travel and final displacement match." }, 16, 174, w - 32, h - 176, AppTheme.Muted);
        }
        else
        {
            var cell = (w - 56) / 2;
            for (var i = 0; i < 2; i++)
            {
                var x = 16 + i * (cell + 24); Box(x, 43, cell, 99, AppTheme.Panel);
                Text(i == 0 ? "Surface A" : "Surface B", x, 48, cell, 35, i == 0 ? AppTheme.Cyan : AppTheme.Accent, center: true);
                Text("3 × 60 seconds", x, 88, cell, 37, AppTheme.Text, center: true);
            }
            Text("Same DPI · same connection · same polling rate", 16, 150, w - 32, 28, AppTheme.Accent);
            Text("Repeatable differences are more useful than one unusual run.", 16, 178, w - 32, 28, AppTheme.Muted);
        }
    }
}
