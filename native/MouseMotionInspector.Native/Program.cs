using System.Text.Json;

namespace MouseMotionInspector.Native;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--render-guide")
        {
            try { ApplicationConfiguration.Initialize(); AppTheme.Initialize(); GuidePreview.Render(args[1]); return 0; }
            catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
        }
        if (args.Length == 2 && args[0] == "--benchmark-charts")
        {
            try { ChartBenchmark.Run(args[1]); return 0; }
            catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
        }
        if ((args.Length == 2 && args[0] == "--render-demo") || (args.Length == 3 && args[0] == "--render-recording"))
        {
            try { ChartPreview.Render(args.Length == 3 ? args[1] : null, args[^1]); return 0; }
            catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
        }
        if (args.Length == 2 && args[0] == "--probe")
        {
            ApplicationConfiguration.Initialize();
            using var engine = new CaptureEngine();
            var counts = new Dictionary<string, int>();
            var errors = new List<string>();
            var until = CaptureEngine.Now + 2000;
            while (CaptureEngine.Now < until)
            {
                while (engine.TryRead(out var packet))
                {
                    var key = packet.Kind.ToString(); counts[key] = counts.GetValueOrDefault(key) + 1;
                    if (packet.Kind == MouseMotionInspector.Core.PacketKind.Error) errors.Add(packet.Detail ?? "Unknown error");
                }
                Thread.Sleep(10);
            }
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { engine.Ready, engine.HookInstalled, engine.HotkeyInstalled, engine.FatalError, engine.Dropped, Counts = counts, Errors = errors }, new JsonSerializerOptions { WriteIndented = true }));
            return engine.Ready && engine.FatalError is null ? 0 : 1;
        }
        if (args.Length == 2 && args[0] == "--list-devices")
        {
            try
            {
                var devices = Win.MouseDevices().Select((handle, i) => Win.Describe(i + 1, handle)).ToArray();
                File.WriteAllText(args[1], JsonSerializer.Serialize(new { CapturedAt = DateTimeOffset.Now, Devices = devices }, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception e) { File.WriteAllText(args[1], JsonSerializer.Serialize(new { Error = e.Message })); return 1; }
        }
        if (args.Length != 0) { Console.Error.WriteLine("Unknown or incomplete command. Use --list-devices <file>, --probe <file>, --render-demo <folder>, --render-guide <folder>, --render-recording <file> <folder>, or --benchmark-charts <file>."); return 2; }
        ApplicationConfiguration.Initialize();
        AppTheme.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
