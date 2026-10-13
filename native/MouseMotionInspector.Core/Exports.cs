using System.Globalization;

namespace MouseMotionInspector.Core;

public static class Exports
{
    public static string Cell(object? value)
    {
        var text = value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";
        if (value is string && text.TrimStart() is { Length: > 0 } trimmed && "=+-@".Contains(trimmed[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
    public static string SummaryCsv(IEnumerable<Recording> runs)
    {
        List<string> lines = [string.Join(',', new[] { "Run", "Started", "Surface", "DPI label", "Polling Hz label", "Connection", "Seconds", "Complete", "Stop reason", "Device", "Name", "VID PID", "Device interface", "Mode", "Units", "Movement events", "Travel", "Net X", "Net Y", "Net distance", "Peak excursion", "Injected moves (whole run)", "Cursor samples changed (whole run)", "Warnings", "Notes" }.Select(Cell))];
        foreach (var run in runs)
        {
            var rows = run.Streams.ToList();
            foreach (var device in run.Devices.Where(d => rows.All(s => s.Device != d.Id))) rows.Add(new() { Device = device.Id });
            if (rows.Count == 0) rows.Add(new() { Device = -1 });
            foreach (var stream in rows)
            {
                var device = run.Devices.FirstOrDefault(d => d.Id == stream.Device);
                object?[] cells = [run.Id, run.StartedAt.ToString("O"), run.Settings.Surface, run.Settings.Dpi, run.Settings.PollingHz, run.Settings.Connection,
                    run.RecordedSeconds, run.Complete, run.StopReason, device is null ? "No resolved devices" : $"D{device.Id}", device?.DisplayName, device?.VidPid, device?.Path,
                    stream.Mode, stream.Unit, stream.Events, stream.Travel, stream.X, stream.Y, stream.Net, stream.Peak, run.InjectedMoves.Count, run.CursorChanges.Count, string.Join("; ", run.Warnings), run.Settings.Notes];
                lines.Add(string.Join(',', cells.Select(Cell)));
            }
        }
        return string.Join("\r\n", lines);
    }
}
