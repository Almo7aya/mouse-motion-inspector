using MouseMotionInspector.Core;

namespace MouseMotionInspector.Native;

internal static class ChartPreview
{
    // Render the exact chart painter into images, without opening a form,
    // registering Raw Input, or interacting with the desktop.
    internal static void Render(string? recording, string directory)
    {
        Recording run;
        if (recording is null)
        {
            run = new(new("Synthetic demonstration", 1600, "Demo", 1000, "Generated example, not a physical mouse test.", 60, 0), 0, DateTimeOffset.UnixEpoch);
            run.Remember(new(1, "", "", "Demo device", "", "", "", "Synthetic data"));
            for (var i = 0; i < 1200; i++)
            {
                var time = 5000 + i * 20;
                var dx = i < 400 ? (i % 9 == 0 ? 2 : 0) : i < 800 ? (i % 7 == 0 ? -2 : 0) : (i % 2 == 0 ? 1 : -1);
                var dy = i < 400 ? (i % 11 == 0 ? 1 : 0) : i < 800 ? (i % 8 == 0 ? -1 : 0) : 0;
                run.Accept(new(PacketKind.Raw, time, 1, X: dx, Y: dy));
            }
            run.Finish(60000, "Timer complete");
        }
        else run = RecordingFiles.Load(recording).First(r => r.Movements.Count > 0);
        var stream = run.Streams.OrderByDescending(s => s.Events).First();
        var info = run.Devices.First(d => d.Id == stream.Device);
        var series = new PlotSeries(); series.Sync(run.Movements, stream.Device, stream.Mode);
        var snapshot = new ChartSnapshot($"D{info.Id} · {info.DisplayName} · {stream.Mode}" + (recording is null ? " · SYNTHETIC EXAMPLE" : ""), "No movement", series, run.RecordedSeconds, stream.Unit);
        Directory.CreateDirectory(directory);
        using var font = new Font("Segoe UI", 10);
        foreach (var kind in Enum.GetValues<ChartKind>())
            foreach (var height in new[] { 340, 700 })
            {
                using var bitmap = new Bitmap(1240, height); using var g = Graphics.FromImage(bitmap);
                ChartRenderer.Draw(g, new(0, 0, 1240, height), font, snapshot, kind, 1, PointF.Empty, null);
                bitmap.Save(Path.Combine(directory, $"{kind.ToString().ToLowerInvariant()}-{height}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
    }
}
