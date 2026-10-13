using System.Runtime.InteropServices;
using System.Text.Json;
using MouseMotionInspector.Core;
using MouseMotionInspector.Native;

var passed = 0;
void Check(string name, Action action)
{
    try { action(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { Console.Error.WriteLine($"FAIL {name}: {e}"); Environment.ExitCode = 1; }
}
void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void True(bool condition) { if (!condition) throw new Exception("Condition was false"); }
Recording Run() => new(new("Cloth", 800, "Wireless", 1000, "", 60, 5), 5000, DateTimeOffset.UnixEpoch);
Packet Raw(double time, int device, int dx, int dy, ushort flags = 0) => new(PacketKind.Raw, time, device, X: dx, Y: dy, Flags: flags);

Check("native x64 ABI offsets and buffer sizes", () =>
{
    Equal(8, IntPtr.Size); Equal(24, Marshal.SizeOf<Win.RawHeader>()); Equal(24, Marshal.SizeOf<Win.RawMouse>());
    Equal(16, Marshal.SizeOf<Win.RawDevice>()); Equal(16, Marshal.SizeOf<Win.DeviceList>()); Equal(32, Marshal.SizeOf<Win.HookInfo>());
    Equal(12, (int)Marshal.OffsetOf<Win.RawMouse>(nameof(Win.RawMouse.X))); Equal(16, (int)Marshal.OffsetOf<Win.RawMouse>(nameof(Win.RawMouse.Y)));
    Equal(24, (int)Marshal.OffsetOf<Win.HookInfo>(nameof(Win.HookInfo.Extra))); Equal(12, Marshal.SizeOf<Win.HidAttributes>());
});
Check("same-named mouse interfaces remain separate by capture identity", () =>
{
    var r = Run(); r.Remember(new(1, "A", "path1", "Viper", "Razer", "1532:00E6", "", "")); r.Remember(new(2, "B", "path2", "Viper", "Razer", "1532:00E6", "", ""));
    r.Accept(Raw(5100, 1, 1, 0)); r.Accept(Raw(5101, 2, 0, -2)); Equal(2, r.Streams.Count); Equal(1L, r.Streams[0].X); Equal(-2L, r.Streams[1].Y);
});
Check("one-unit out-and-back jitter keeps travel with zero net displacement", () =>
{
    var r = Run(); r.Accept(Raw(5100, 1, 1, 0)); r.Accept(Raw(5101, 1, -1, 0)); var s = r.Streams.Single();
    Equal(2, s.Events); Equal(2d, s.Travel); Equal(0d, s.Net); Equal(1d, s.Peak); Equal("No displacement", s.Direction);
});
Check("settling and deadline reports do not contaminate a recording", () =>
{
    var r = Run(); r.Accept(Raw(4999, 1, 99, 99)); r.Accept(Raw(5000, 1, 1, 0)); r.Accept(Raw(65000, 1, 99, 99));
    Equal(1, r.Movements.Count); Equal(0d, r.Movements[0].TimeMs); r.Finish(65000, "Timer complete"); Equal(true, r.Complete); Equal(60d, r.RecordedSeconds);
});
Check("absolute reports use per-device baselines rather than treating positions as deltas", () =>
{
    var r = Run(); r.Accept(Raw(4800, 1, 30000, 20000, 1)); r.Accept(Raw(4900, 2, 1000, 1000, 1));
    r.Accept(Raw(5100, 1, 30001, 19999, 1)); r.Accept(Raw(5200, 2, 1000, 1002, 1));
    Equal(1L, r.Movements[0].Dx); Equal(-1L, r.Movements[0].Dy); Equal(2L, r.Movements[1].Dy); Equal("normalized absolute units", r.Movements[0].Unit);
});
Check("absolute coordinate domain and relative/absolute changes reset their baselines", () =>
{
    var r = Run(); r.Accept(Raw(5000, 1, 100, 100, 1)); r.Accept(Raw(5100, 1, 200, 200, 3)); Equal(0, r.Movements.Count);
    r.Accept(Raw(5200, 1, 201, 200, 3)); r.Accept(Raw(5300, 1, 1, 0)); r.Accept(Raw(5400, 1, 5000, 5000, 1));
    Equal(2, r.Movements.Count); Equal(2, r.Streams.Count); Equal("absolute-virtual-desktop", r.Movements[0].Mode); Equal("relative", r.Movements[1].Mode);
});
Check("absolute first report during recording is a baseline, not false motion", () =>
{
    var r = Run(); r.Accept(Raw(5100, 1, 65535, 65535, 1)); r.Accept(Raw(5200, 1, 65534, 65535, 1)); Equal(1, r.Movements.Count); Equal(-1L, r.Movements[0].Dx);
});
Check("buttons-only reports do not count as movement", () =>
{
    var r = Run(); r.Accept(new(PacketKind.Raw, 5100, 1, Buttons: 1)); Equal(0, r.Movements.Count); Equal(0, r.Streams.Count);
});
Check("injected events and sampled cursor movement stay separate from raw device evidence", () =>
{
    var r = Run(); r.ObserveCursor(4900, 10, 10); r.Accept(new(PacketKind.Injected, 5100, X: 11, Y: 10, Flags: 3, WindowsTime: 100)); r.ObserveCursor(5101, 11, 10);
    Equal(0, r.Movements.Count); Equal(0, r.Streams.Count); Equal(1, r.InjectedMoves.Count); Equal(1, r.CursorChanges.Count); Equal((ushort)3, r.InjectedMoves[0].Flags);
});
Check("manual stop cutoff excludes queued events after the stop time", () =>
{
    var r = Run(); r.RequestStop(5500); r.Accept(Raw(5499, 1, 1, 0)); r.Accept(Raw(5500, 1, 10, 0)); r.Accept(new(PacketKind.Injected, 5501, Flags: 1));
    r.Finish(5500, "Stopped by user"); r.Accept(Raw(5400, 1, 100, 0)); Equal(1, r.Movements.Count); Equal(0, r.InjectedMoves.Count); Equal(.5, r.RecordedSeconds); Equal(false, r.Complete);
});
Check("removal clears an absolute baseline and records a device-change marker", () =>
{
    var r = Run(); r.Accept(Raw(5000, 1, 100, 100, 1)); r.Accept(new(PacketKind.Removal, 5100, 1)); r.Accept(Raw(5200, 1, 200, 200, 1)); Equal(0, r.Movements.Count); Equal(false, r.DeviceChanges.Single().Connected);
});
Check("sample limit stops accumulation without altering already-retained measurements", () =>
{
    var r = Run(); for (var i = 0; i <= Recording.SampleLimit; i++) r.Accept(Raw(5000 + i / 100.0, 1, 1, 0));
    Equal(Recording.SampleLimit, r.Movements.Count); Equal((double)Recording.SampleLimit, r.Streams[0].Travel); Equal(true, r.AtLimit); r.Finish(65000, "250,000-observation limit reached"); Equal(false, r.Complete);
});
Check("JSON includes physical identification, units, flags and the full observations", () =>
{
    var r = Run(); r.Remember(new(1, "0", "", "Unattributed Windows pointer", "", "", "", "No device handle")); r.Accept(Raw(5100, 1, -1, 2));
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(r)); Equal(1, json.RootElement.GetProperty("Movements").GetArrayLength()); Equal("raw relative units", json.RootElement.GetProperty("Movements")[0].GetProperty("Unit").GetString()); True(!json.RootElement.TryGetProperty("Start", out _));
});
Check("CSV escapes text, preserves negative numbers, and includes zero-motion devices", () =>
{
    Equal("\"'=formula\"", Exports.Cell("=formula")); Equal("\"-1\"", Exports.Cell(-1)); Equal("\"a,\"\"b\"\"\"", Exports.Cell("a,\"b\""));
    var r = Run(); r.Remember(new(1, "", "p", "Quiet device", "", "", "", "")); r.Finish(65000, "Timer complete"); True(Exports.SummaryCsv([r]).Contains("Quiet device")); True(Exports.SummaryCsv([r]).Contains("\"60\",\"True\""));
});
Check("trace follows recorded movement instead of the first quiet device", () =>
{
    var selection = new TraceSelection(); selection.SetContext(Guid.NewGuid());
    selection.Update([new((1, "relative"), 0), new((3, "relative"), 0), new((4, "relative"), 0)]);
    Equal((1, "relative"), selection.Key!.Value);
    selection.Update([new((1, "relative"), 0), new((3, "relative"), 0), new((4, "relative"), 3327)]);
    Equal((4, "relative"), selection.Key!.Value);
    // Once it has a trace, do not jump to other devices as their counts change.
    selection.Update([new((1, "relative"), 5000), new((4, "relative"), 3327)]);
    Equal((4, "relative"), selection.Key!.Value);
});
Check("manual quiet-device selection survives refresh and finishing the same run", () =>
{
    var selection = new TraceSelection(); var id = Guid.NewGuid(); selection.SetContext(id);
    TraceRow[] rows = [new((1, "relative"), 0), new((4, "relative"), 3327)];
    selection.Update(rows); selection.Select((1, "relative")); selection.Update(rows);
    selection.SetContext(id); selection.Update(rows);
    Equal((1, "relative"), selection.Key!.Value);
    selection.SetContext(Guid.NewGuid()); selection.Update(rows);
    Equal((4, "relative"), selection.Key!.Value);
});
Check("clicking the initial automatic row keeps that deliberate choice", () =>
{
    var selection = new TraceSelection();
    selection.Update([new((1, "relative"), 0), new((4, "relative"), 0)]);
    selection.Select((1, "relative"));
    selection.Update([new((1, "relative"), 0), new((4, "relative"), 100)]);
    Equal((1, "relative"), selection.Key!.Value);
});
Check("trace selection recovers when its row disappears and handles empty lists", () =>
{
    var selection = new TraceSelection(); selection.Select((4, "relative"));
    selection.Update([new((2, "relative"), 0), new((5, "relative"), 10)]);
    Equal((5, "relative"), selection.Key!.Value);
    selection.Update([]); True(selection.Key is null);
    selection.Update([new((2, "relative"), 0)]); Equal((2, "relative"), selection.Key!.Value);
});
Check("trace selection keeps coordinate modes separate and resets for live view", () =>
{
    var selection = new TraceSelection(); selection.SetContext(Guid.NewGuid());
    selection.Update([new((4, "relative"), 0)]);
    selection.Update([new((4, "relative"), 0), new((4, "absolute-primary-display"), 10)]);
    Equal((4, "absolute-primary-display"), selection.Key!.Value);
    selection.Select((4, "relative"));
    selection.Update([new((4, "relative"), 0), new((4, "absolute-primary-display"), 10)]);
    Equal((4, "relative"), selection.Key!.Value);
    selection.SetContext(null); selection.Update([new((2, "relative"), 0), new((5, "relative"), 20)]);
    Equal((5, "relative"), selection.Key!.Value);
});

Check("activity keeps quiet time and sums out-and-back travel", () =>
{
    var r = Run(); r.Accept(Raw(5100, 1, 1, 0)); r.Accept(Raw(5200, 1, -1, 0));
    var buckets = PlotData.Activity(r.Movements, 60);
    Equal(60, buckets.Count); Equal(2d, buckets[0].Travel); Equal(2, buckets[0].Events);
    Equal(0d, buckets[^1].Travel); Equal(60d, buckets[^1].EndSeconds);
    Equal(r.Streams[0].Travel, buckets.Sum(b => b.Travel));
});
Check("activity includes the end of partial tests and scales long test buckets", () =>
{
    var r = Run(); r.Accept(Raw(5999, 1, 3, 4));
    var shortBuckets = PlotData.Activity(r.Movements, .999); Equal(5d, shortBuckets.Sum(b => b.Travel));
    var longBuckets = PlotData.Activity(r.Movements, 600); Equal(120, longBuckets.Count);
    Equal(5d, longBuckets[0].EndSeconds); Equal(600d, longBuckets[^1].EndSeconds);
});
Check("plot reduction preserves single-sample excursions and chronological endpoints", () =>
{
    var r = Run();
    for (var i = 0; i < 12000; i++) r.Accept(Raw(5000 + i, 1, i == 611 ? 10000 : i == 612 ? -10000 : i % 2 == 0 ? 1 : -1, i == 455 ? -9000 : i == 456 ? 9000 : 0));
    var reduced = PlotData.Reduce(r.Movements, 120);
    True(reduced.Count <= 120); Equal(r.Movements[0], reduced[0]); Equal(r.Movements[^1], reduced[^1]);
    Equal(r.Movements.Max(m => m.X), reduced.Max(m => m.X)); Equal(r.Movements.Min(m => m.Y), reduced.Min(m => m.Y));
    True(reduced.Zip(reduced.Skip(1)).All(pair => pair.First.TimeMs < pair.Second.TimeMs));
});
Check("axis ticks remain finite across tiny, empty and large extents", () =>
{
    foreach (var extent in new[] { 0d, 1e-8, 8, 100, 5e14 }) { var step = PlotData.TickStep(extent); True(double.IsFinite(step) && step > 0); }
    Equal(20d, PlotData.TickStep(100));
});
Movement Point(int i, int device = 1, string mode = "relative") =>
    new(i * 2.399, device, mode, "raw relative units", 3, 4, i % 503 - 251, i % 307 - 153, 3, 4, 0, 0);
Check("incremental plots isolate interfaces and modes and reset when the source changes", () =>
{
    var source = new List<Movement> { Point(0), Point(1, 2), Point(2, 1, "absolute-primary-display") };
    var series = new PlotSeries(); True(series.Sync(source, 1, "relative")); Equal(1, series.Moves.Count);
    True(!series.Sync(source, 1, "relative"));
    source.Add(Point(3, 2)); True(!series.Sync(source, 1, "relative"));
    source.Add(Point(4)); True(series.Sync(source, 1, "relative")); Equal(2, series.Moves.Count); Equal(source[^1], series.Moves[^1]);
    True(series.Sync(source, 2, "relative")); Equal(2, series.Moves.Count); True(series.Moves.All(m => m.Device == 2));
    True(series.Sync(source, 1, "absolute-primary-display")); Equal(source[2], series.Moves.Single());
    Movement[] replacement = [Point(100)]; True(series.Sync(replacement, 1, "relative")); Equal(replacement[0], series.Moves.Single());
    True(series.Sync(source, 1, "relative")); source.Clear(); True(series.Sync(source, 1, "relative"));
    Equal(0, series.Moves.Count); Equal((0L, 0L, 0L, 0L), series.Bounds); Equal(0d, series.Activity(60).Sum(b => b.Travel));
});
Check("incremental reduction preserves peaks and chronology across every merge boundary", () =>
{
    var source = new List<Movement>(); var series = new PlotSeries();
    foreach (var count in new[] { 1, 5999, 6000, 6001, 8000, 8001, 16000, 16001, 64001, 128001, 250000 })
    {
        while (source.Count < count)
        {
            var i = source.Count; var p = Point(i);
            source.Add(i == count - 2 ? p with { X = i * (i % 2 == 0 ? 1L : -1L), Y = -i * (i % 2 == 0 ? 1L : -1L) } : p);
        }
        True(series.Sync(source, 1, "relative")); var reduced = series.Reduced;
        True(reduced.Count <= 6000); Equal(source[0], reduced[0]); Equal(source[^1], reduced[^1]);
        Equal(source.Min(m => m.X), reduced.Min(m => m.X)); Equal(source.Max(m => m.X), reduced.Max(m => m.X));
        Equal(source.Min(m => m.Y), reduced.Min(m => m.Y)); Equal(source.Max(m => m.Y), reduced.Max(m => m.Y));
        True(reduced.Zip(reduced.Skip(1)).All(p => p.First.TimeMs < p.Second.TimeMs));
        Equal((Math.Min(0, source.Min(m => m.X)), Math.Max(0, source.Max(m => m.X)), Math.Min(0, source.Min(m => m.Y)), Math.Max(0, source.Max(m => m.Y))), series.Bounds);
        Equal(count, source.Count); Equal(count, series.Moves.Count); True(!series.Sync(source, 1, "relative"));
        True(ReferenceEquals(reduced, series.Reduced));
    }
});
Check("incremental activity matches full-data totals at partial and long-test boundaries", () =>
{
    var source = new List<Movement>(); var series = new PlotSeries();
    foreach (var time in new[] { 0d, 999.999, 1000, 1999.999, 2000, 59999, 120000, 599999 })
    {
        source.Add(Point(source.Count) with { TimeMs = time }); series.Sync(source, 1, "relative");
        foreach (var seconds in new[] { .5, 1, 1.999, 60, 120.001, 600 })
        {
            var expected = PlotData.Activity(source, seconds); var actual = series.Activity(seconds);
            Equal(expected.Count, actual.Count);
            for (var i = 0; i < expected.Count; i++) Equal(expected[i], actual[i]);
            Equal(source.Count * 5d, actual.Sum(b => b.Travel)); Equal(source.Count, actual.Sum(b => b.Events));
            True(ReferenceEquals(actual, series.Activity(seconds)));
        }
    }
});
Recording SavedRun()
{
    var r = Run(); r.Remember(new(1, "0", "test-path", "Synthetic mouse", "Example", "1234:5678", "", "Synthetic fixture"));
    r.Accept(Raw(5100, 1, 3, -4)); r.Accept(Raw(5200, 1, -3, 4)); r.Finish(65000, "Timer complete"); return r;
}
IReadOnlyList<Recording> ImportJson(string text) { using var json = JsonDocument.Parse(text); return RecordingFiles.Parse(json.RootElement); }
void RejectImport(string text)
{
    try { ImportJson(text); } catch (InvalidDataException) { return; }
    throw new Exception("Invalid recording was accepted.");
}
Check("recording import preserves identity and recalculates trace statistics", () =>
{
    var original = SavedRun(); var restored = ImportJson(JsonSerializer.Serialize(original)).Single();
    Equal(original.Id, restored.Id); Equal(true, restored.Finished); Equal(true, restored.Complete);
    Equal(2, restored.Movements.Count); Equal(10d, restored.Streams.Single().Travel); Equal(0L, restored.Streams.Single().X);
    Equal(original.OperatingSystem, restored.OperatingSystem); Equal(Exports.SummaryCsv([original]), Exports.SummaryCsv([restored]));
    var combined = ImportJson(JsonSerializer.Serialize(new { Runs = new[] { original, SavedRun() } })); Equal(2, combined.Count);
});
Check("import rejects future formats, missing arrays and inconsistent totals", () =>
{
    var json = JsonSerializer.Serialize(SavedRun());
    RejectImport(json.Replace("\"FormatVersion\":1", "\"FormatVersion\":99"));
    RejectImport(json.Replace("\"X\":3", "\"X\":99"));
    RejectImport(json.Replace("\"Warnings\":[]", "\"Warnings\":null"));
    RejectImport("[]"); RejectImport("{\"Runs\":{}}");
});
Check("import rejects wrong units, unknown devices and out-of-window timestamps", () =>
{
    var json = JsonSerializer.Serialize(SavedRun());
    RejectImport(json.Replace("raw relative units", "pixels"));
    RejectImport(json.Replace("\"Device\":1", "\"Device\":99"));
    RejectImport(json.Replace("\"TimeMs\":100", "\"TimeMs\":90000"));
});
Check("timer completion with warnings remains an incomplete recording", () =>
{
    var r = Run(); r.Warnings.Add("Input read failed"); r.Finish(65000, "Timer complete"); Equal(false, r.Complete);
});
Check("device-change floods count toward the observation limit", () =>
{
    var r = Run(); for (var i = 0; i <= Recording.SampleLimit; i++) r.Accept(new(PacketKind.Arrival, 5100, 1));
    Equal(Recording.SampleLimit, r.DeviceChanges.Count); Equal(true, r.AtLimit);
});
Check("atomic save replaces a complete file and leaves no temporary sibling", () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "mmi-check-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
    try
    {
        var file = Path.Combine(folder, "recording.json");
        RecordingFiles.WriteAtomic(file, "original"); RecordingFiles.WriteAtomic(file, JsonSerializer.Serialize(SavedRun()));
        Equal(1, Directory.GetFiles(folder).Length); Equal(2, RecordingFiles.Load(file).Single().Movements.Count);
        File.Delete(file);
    }
    finally { if (Directory.GetFiles(folder).Length == 0) Directory.Delete(folder); }
});

// Optional saved recordings exercise the same selection policy against actual
// data without opening the capture window or generating mouse input.
foreach (var path in args)
    Check($"recorded trace selection: {Path.GetFileName(path)}", () =>
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        var streams = root.GetProperty("Streams").EnumerateArray().ToArray();
        var rows = root.GetProperty("Devices").EnumerateArray().OrderBy(d => d.GetProperty("Id").GetInt32()).SelectMany(device =>
        {
            var id = device.GetProperty("Id").GetInt32();
            return streams.Where(s => s.GetProperty("Device").GetInt32() == id)
                .Select(s => new TraceRow((id, s.GetProperty("Mode").GetString()!), s.GetProperty("Events").GetInt64()))
                .DefaultIfEmpty(new((id, "relative"), 0));
        }).ToArray();
        var expected = rows.OrderByDescending(row => row.Movements).First(); True(expected.Movements > 0);
        var selection = new TraceSelection(); selection.SetContext(root.GetProperty("Id").GetGuid());
        selection.Update(rows.Select(row => row with { Movements = 0 }).ToArray());
        selection.Update(rows); Equal(expected.Key, selection.Key!.Value);
        var moves = root.GetProperty("Movements").Deserialize<Movement[]>()!
            .Where(m => selection.Key == (m.Device, m.Mode)).ToArray();
        Equal(expected.Movements, (long)moves.Length); True(moves.Any(m => m.X != 0 || m.Y != 0));
        var stream = streams.Single(s => (s.GetProperty("Device").GetInt32(), s.GetProperty("Mode").GetString()!) == selection.Key);
        Equal(stream.GetProperty("X").GetInt64(), moves[^1].X); Equal(stream.GetProperty("Y").GetInt64(), moves[^1].Y);
        var reopened = RecordingFiles.Load(path).Single(); Equal(root.GetProperty("Id").GetGuid(), reopened.Id);
        Equal(root.GetProperty("Movements").GetArrayLength(), reopened.Movements.Count);
        Console.WriteLine($"  Selected D{selection.Key.Value.Device} ({selection.Key.Value.Mode}): {moves.Length:N0} points, end ({moves[^1].X}, {moves[^1].Y}), X [{moves.Min(m => m.X)}, {moves.Max(m => m.X)}], Y [{moves.Min(m => m.Y)}, {moves.Max(m => m.Y)}]");
    });
Console.WriteLine($"{passed} checks passed. These validate data handling, not physical mouse behavior or the on-screen UI.");
