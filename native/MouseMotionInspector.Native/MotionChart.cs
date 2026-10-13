using System.Drawing.Drawing2D;
using System.Globalization;
using MouseMotionInspector.Core;

namespace MouseMotionInspector.Native;

internal enum ChartKind { Path, Displacement, Activity }
internal sealed record ChartSnapshot(string Title, string EmptyMessage, PlotSeries Series, double Seconds, string Unit)
{
    internal IReadOnlyList<Movement> Moves => Series.Moves;
    internal IReadOnlyList<Movement> Reduced => Series.Reduced;
    internal (long MinX, long MaxX, long MinY, long MaxY) Bounds => Series.Bounds;
    internal IReadOnlyList<ActivityBucket> Activity => Series.Activity(Seconds);
}

internal sealed class MotionChart : Control
{
    internal ChartSnapshot Snapshot = new("Movement analysis", "Start a timed test to plot device movement.", new(), 1, "raw relative units");
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal ChartKind Kind { get; set; }
    private double zoom = 1;
    private PointF pan;
    private Point? hover, drag;
    internal MotionChart()
    {
        DoubleBuffered = true; Dock = DockStyle.Fill; BackColor = AppTheme.Background;
        AccessibleName = "Device movement graph"; TabStop = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    internal void Fit() { zoom = 1; pan = PointF.Empty; hover = null; Invalidate(); }
    internal void Export(string path)
    {
        using var bitmap = new Bitmap(1600, 900); using var g = Graphics.FromImage(bitmap);
        using var font = new Font("Segoe UI", 12);
        ChartRenderer.Draw(g, new(0, 0, bitmap.Width, bitmap.Height), font, Snapshot, Kind, 1, PointF.Empty, null);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); ChartRenderer.Draw(e.Graphics, ClientRectangle, Font, Snapshot, Kind, zoom, pan, hover); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e); if (Kind != ChartKind.Path) return;
        zoom = Math.Clamp(zoom * Math.Pow(1.2, e.Delta / 120d), 1, 64); if (zoom == 1) pan = PointF.Empty; Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); if (e.Button == MouseButtons.Left && Kind == ChartKind.Path) { drag = e.Location; Capture = true; } }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); hover = e.Location;
        if (drag is { } previous) { pan.X += e.X - previous.X; pan.Y += e.Y - previous.Y; drag = e.Location; }
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = null; Capture = false; }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) drag = null; }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }
    protected override void OnDoubleClick(EventArgs e) { base.OnDoubleClick(e); Fit(); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Home or Keys.Add or Keys.Subtract || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Home) { Fit(); e.Handled = true; }
        if (Kind == ChartKind.Path && e.KeyCode is Keys.Add or Keys.Subtract)
        { zoom = Math.Clamp(zoom * (e.KeyCode == Keys.Add ? 1.2 : 1 / 1.2), 1, 64); Invalidate(); e.Handled = true; }
    }
}

internal static class ChartRenderer
{
    private static string Number(double value) => Math.Abs(value) < .00000001 ? "0" : value.ToString("0.###", CultureInfo.InvariantCulture);
    internal static void Draw(Graphics g, Rectangle area, Font font, ChartSnapshot data, ChartKind kind, double zoom, PointF pan, Point? hover)
    {
        g.Clear(AppTheme.Background);
        // Time plots are horizontal/vertical steps and bars. Rasterizing them
        // directly avoids expensive antialiasing of thousands of overlapping steps.
        g.SmoothingMode = kind == ChartKind.Path ? SmoothingMode.AntiAlias : SmoothingMode.None;
        var unit = font.Height / 17f;
        var left = 78 * unit; var top = 80 * unit; var right = 26 * unit; var bottom = 60 * unit;
        var plot = new RectangleF(left, top, Math.Max(1, area.Width - left - right), Math.Max(1, area.Height - top - bottom));
        void Text(string text, Color color, RectangleF bounds, TextFormatFlags align = TextFormatFlags.Left)
            => TextRenderer.DrawText(g, text, font, Rectangle.Round(bounds), color, align | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        Text(data.Title, AppTheme.Text, new(18, 5, area.Width - 36, 28 * unit));
        if (area.Height < 175 * unit || area.Width < 300 * unit)
        { Text("Enlarge the graph area to see axes and movement.", AppTheme.Muted, new(18, 40, area.Width - 36, 30)); return; }
        using var grid = new Pen(AppTheme.Border); using var axis = new Pen(AppTheme.Muted);
        using var green = new Pen(AppTheme.Accent, 1.6f); using var cyan = new Pen(AppTheme.Cyan, 1.6f);
        using var greenBrush = new SolidBrush(AppTheme.Accent); using var cyanBrush = new SolidBrush(AppTheme.Cyan);
        using var backgroundBrush = new SolidBrush(AppTheme.Input);
        g.FillRectangle(backgroundBrush, plot);
        if (data.Moves.Count == 0)
        {
            g.DrawRectangle(grid, plot.X, plot.Y, plot.Width, plot.Height);
            Text(data.EmptyMessage, AppTheme.Muted, new(plot.X + 16, plot.Top + plot.Height / 2 - 20, plot.Width - 32, 40), TextFormatFlags.HorizontalCenter);
            Text("Each device and coordinate mode has its own trace.", AppTheme.Muted, new(18, area.Height - 30 * unit, area.Width - 36, 24 * unit));
            return;
        }
        var moves = data.Moves; var reduced = kind == ChartKind.Activity ? moves : data.Reduced; var seconds = Math.Max(.001, data.Seconds);
        var (minX, maxX, minY, maxY) = data.Bounds;
        double x0, x1, y0, y1; var down = kind == ChartKind.Path;
        IReadOnlyList<ActivityBucket> buckets = [];
        if (kind == ChartKind.Path)
        {
            var scale = Math.Min(plot.Width / Math.Max(8, (maxX - (double)minX) * 1.15), plot.Height / Math.Max(8, (maxY - (double)minY) * 1.15)) * zoom;
            var cx = minX / 2d + maxX / 2d - pan.X / scale; var cy = minY / 2d + maxY / 2d - pan.Y / scale;
            x0 = cx - plot.Width / scale / 2; x1 = cx + plot.Width / scale / 2;
            y0 = cy - plot.Height / scale / 2; y1 = cy + plot.Height / scale / 2;
        }
        else
        {
            x0 = 0; x1 = seconds;
            if (kind == ChartKind.Activity)
            {
                buckets = data.Activity; y0 = 0; y1 = Math.Max(1, buckets.Max(b => b.Travel) * 1.12);
            }
            else
            {
                var low = Math.Min(minX, minY); var high = Math.Max(maxX, maxY); var padding = Math.Max(1, (high - (double)low) * .1);
                y0 = low - padding; y1 = high + padding;
            }
        }
        PointF Map(double x, double y) => new((float)(plot.Left + (x - x0) / (x1 - x0) * plot.Width),
            (float)(down ? plot.Top + (y - y0) / (y1 - y0) * plot.Height : plot.Bottom - (y - y0) / (y1 - y0) * plot.Height));
        var xStep = PlotData.TickStep(x1 - x0, Math.Max(2, (int)(plot.Width / (100 * unit))));
        var yStep = PlotData.TickStep(y1 - y0, Math.Max(2, (int)(plot.Height / (45 * unit))));
        for (var x = Math.Ceiling(x0 / xStep) * xStep; x <= x1 + xStep * 1e-8; x += xStep)
        {
            var p = Map(x, y0); g.DrawLine(Math.Abs(x) < xStep * .001 ? axis : grid, p.X, plot.Top, p.X, plot.Bottom);
            Text(Number(x), AppTheme.Muted, new(p.X - 45 * unit, plot.Bottom + 3, 90 * unit, 23 * unit), TextFormatFlags.HorizontalCenter);
        }
        for (var y = Math.Ceiling(y0 / yStep) * yStep; y <= y1 + yStep * 1e-8; y += yStep)
        {
            var p = Map(x0, y); g.DrawLine(Math.Abs(y) < yStep * .001 ? axis : grid, plot.Left, p.Y, plot.Right, p.Y);
            Text(Number(y), AppTheme.Muted, new(2, p.Y - 11 * unit, plot.Left - 10, 22 * unit), TextFormatFlags.Right);
        }
        string subtitle;
        var state = g.Save(); g.SetClip(plot);
        if (kind == ChartKind.Path)
        {
            var points = new PointF[reduced.Count + 1]; points[0] = Map(0, 0);
            for (var i = 0; i < reduced.Count; i++) points[i + 1] = Map(reduced[i].X, reduced[i].Y);
            g.DrawLines(green, points); var origin = Map(0, 0); var last = Map(moves[^1].X, moves[^1].Y);
            g.DrawEllipse(cyan, origin.X - 5, origin.Y - 5, 10, 10); g.FillEllipse(greenBrush, last.X - 4, last.Y - 4, 8, 8);
            subtitle = $"○ Start   ● End   ·   Equal X/Y scale   ·   +X right / +Y down   ·   {zoom:0.#}×";
        }
        else if (kind == ChartKind.Displacement)
        {
            void Series(Func<Movement, long> coordinate, Pen pen)
            {
                var points = new PointF[reduced.Count * 2 + 2]; points[0] = Map(0, 0); long previous = 0; var index = 1;
                foreach (var m in reduced) { points[index++] = Map(m.TimeMs / 1000, previous); previous = coordinate(m); points[index++] = Map(m.TimeMs / 1000, previous); }
                points[index] = Map(seconds, previous); g.DrawLines(pen, points);
            }
            Series(m => m.X, green); Series(m => m.Y, cyan);
            subtitle = "X displacement: green   ·   Y displacement: cyan   ·   Idle time remains on the time axis";
        }
        else
        {
            foreach (var b in buckets)
            {
                var p = Map(b.StartSeconds, b.Travel); var end = Map(b.EndSeconds, 0);
                g.FillRectangle(greenBrush, p.X + 1, p.Y, Math.Max(1, end.X - p.X - 2), Math.Max(0, end.Y - p.Y));
            }
            subtitle = $"Travel per {Number(buckets[0].EndSeconds - buckets[0].StartSeconds)} s bucket (last may be partial)   ·   Includes out-and-back movement";
        }
        if (hover is { } mouse && plot.Contains(mouse))
        {
            using var crosshair = new Pen(Color.FromArgb(120, AppTheme.Text)) { DashStyle = DashStyle.Dot };
            g.DrawLine(crosshair, mouse.X, plot.Top, mouse.X, plot.Bottom); g.DrawLine(crosshair, plot.Left, mouse.Y, plot.Right, mouse.Y);
        }
        g.Restore(state);
        Text(subtitle, AppTheme.Muted, new(18, 33 * unit, area.Width - 36, 25 * unit));
        Text(kind == ChartKind.Path ? "Y ↓" : kind == ChartKind.Activity ? "Travel" : "X / Y", AppTheme.Muted, new(4, plot.Top - 25 * unit, 74 * unit, 24 * unit));
        Text(kind == ChartKind.Path ? "X →" : "Time (s)", AppTheme.Muted, new(plot.Right - 80 * unit, plot.Bottom + 25 * unit, 80 * unit, 24 * unit), TextFormatFlags.Right);
        var footer = data.Unit + (kind == ChartKind.Activity ? $" · Travel summed from all {moves.Count:N0} reports" :
            reduced.Count < moves.Count ? " · Display reduced with extremes preserved; exports contain every sample" : " · All samples shown");
        if (hover is { } position && plot.Contains(position))
        {
            var x = x0 + (position.X - plot.Left) / plot.Width * (x1 - x0);
            var y = down ? y0 + (position.Y - plot.Top) / plot.Height * (y1 - y0) : y1 - (position.Y - plot.Top) / plot.Height * (y1 - y0);
            footer = $"Chart coordinates: {(kind == ChartKind.Path ? "X" : "time")} {Number(x)}{(kind == ChartKind.Path ? "" : " s")}, {(kind == ChartKind.Path ? "Y" : kind == ChartKind.Activity ? "travel" : "displacement")} {Number(y)} · {data.Unit}";
        }
        Text(footer, AppTheme.Muted, new(18, area.Height - 29 * unit, Math.Max(1, area.Width - 140 * unit), 25 * unit));
    }
}
