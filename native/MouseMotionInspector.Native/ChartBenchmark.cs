using System.Diagnostics;
using System.Text.Json;
using MouseMotionInspector.Core;

namespace MouseMotionInspector.Native;

internal static class ChartBenchmark
{
    // Off-screen preparation + painting, not a measurement of displayed FPS.
    internal static void Run(string output)
    {
        var results = new List<object>();
        using var bitmap = new Bitmap(1240, 340); using var g = Graphics.FromImage(bitmap);
        using var font = new Font("Segoe UI", 10);
        foreach (var kind in Enum.GetValues<ChartKind>())
        {
            var source = new List<Movement>(250_000);
            void Append()
            {
                var i = source.Count; var x = (long)(Math.Sin(i * .005) * 500); var y = (long)(Math.Cos(i * .003) * 300);
                var previous = source.Count > 0 ? source[^1] : null;
                source.Add(new(i / 8d, 1, "relative", "raw relative units", x - (previous?.X ?? 0), y - (previous?.Y ?? 0), x, y, 0, 0, 0, 0));
            }
            while (source.Count < 220_000) Append();
            var series = new PlotSeries();
            ChartSnapshot Prepare()
            {
                series.Sync(source, 1, "relative");
                return new("Synthetic 8,000 reports/s workload", "", series, source.Count / 8000d, "raw relative units");
            }
            for (var warmup = 0; warmup < 5; warmup++) ChartRenderer.Draw(g, new(0, 0, 1240, 340), font, Prepare(), kind, 1, PointF.Empty, null);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var frames = new List<double>(); var preparation = new List<double>();
            for (var frame = 0; frame < 120; frame++)
            {
                for (var i = 0; i < 128; i++) Append();
                var begin = Stopwatch.GetTimestamp(); var snapshot = Prepare(); var prepared = Stopwatch.GetTimestamp();
                ChartRenderer.Draw(g, new(0, 0, 1240, 340), font, snapshot, kind, 1, PointF.Empty, null);
                frames.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
                preparation.Add(Stopwatch.GetElapsedTime(begin, prepared).TotalMilliseconds);
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated; var ordered = frames.Order().ToArray();
            results.Add(new { Chart = kind.ToString(), Samples = source.Count, Frames = frames.Count,
                MeanFrameMs = frames.Average(), P95FrameMs = ordered[(int)(ordered.Length * .95)], MaxFrameMs = ordered[^1],
                MeanPreparationMs = preparation.Average(), AllocatedBytesPerFrame = bytes / frames.Count,
                UiScheduler = MeasureScheduler(() =>
                {
                    for (var i = 0; i < 128; i++) Append();
                    ChartRenderer.Draw(g, new(0, 0, 1240, 340), font, Prepare(), kind, 1, PointF.Empty, null);
                }) });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { Note = "Synthetic off-screen prepare and paint. UiScheduler separately measures the frame clock on a hidden Windows message loop with the same workload. Neither measurement includes tables or desktop presentation; neither is on-screen FPS.", Results = results }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static object MeasureScheduler(Action frame)
    {
        using var owner = new Control(); _ = owner.Handle;
        using var context = new ApplicationContext();
        var thread = Environment.CurrentManagedThreadId; var intervals = new List<double>(); long previous = 0;
        Exception? failure = null;
        using var clock = new UiFrameClock(owner, () =>
        {
            try
            {
                if (Environment.CurrentManagedThreadId != thread) throw new InvalidOperationException("Frame ran outside the UI thread.");
                var now = Stopwatch.GetTimestamp();
                if (previous != 0) intervals.Add(Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds);
                previous = now; frame();
                if (intervals.Count >= 90) context.ExitThread();
            }
            catch (Exception e) { failure = e; context.ExitThread(); }
        });
        using var watchdog = new System.Windows.Forms.Timer { Interval = 10000 };
        watchdog.Tick += (_, _) => { failure = new TimeoutException("Frame clock did not complete the benchmark."); context.ExitThread(); };
        watchdog.Start(); Application.Run(context);
        if (failure is not null) throw failure;
        var sorted = intervals.Order().ToArray();
        return new { Callbacks = intervals.Count, MeanIntervalMs = intervals.Average(), P95IntervalMs = sorted[(int)(sorted.Length * .95)], MaxIntervalMs = sorted[^1] };
    }
}
