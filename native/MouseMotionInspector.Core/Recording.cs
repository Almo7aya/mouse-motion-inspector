using System.Text.Json.Serialization;

namespace MouseMotionInspector.Core;

public enum PacketKind { Raw, Injected, Arrival, Removal, Hotkey, Error }
public readonly record struct Packet(PacketKind Kind, double Time, int Device = 0, long Handle = 0,
    int X = 0, int Y = 0, ushort Flags = 0, ushort Buttons = 0, uint WindowsTime = 0, string? Detail = null);
public sealed record DeviceInfo(int Id, string Handle, string Path, string Name, string Manufacturer,
    string VidPid, string InstanceId, string IdentificationNote, string Alias = "")
{
    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Name : Alias;
}
public sealed record TestSettings(string Surface, int? Dpi, string Connection, int? PollingHz,
    string Notes, int DurationSeconds, int SettleSeconds);
public sealed record Movement(double TimeMs, int Device, string Mode, string Unit, long Dx, long Dy,
    long X, long Y, int ReportX, int ReportY, ushort Flags, ushort Buttons);
public sealed record Injection(double TimeMs, int ScreenX, int ScreenY, ushort Flags, uint WindowsMessageTime);
public sealed record CursorChange(double TimeMs, int ScreenX, int ScreenY, long Dx, long Dy);
public sealed record DeviceChange(double TimeMs, int Device, bool Connected);

public sealed class MotionStats
{
    public int Device { get; init; }
    public string Mode { get; init; } = "relative";
    public string Unit => Mode == "relative" ? "raw relative units" : "normalized absolute units";
    public int Events { get; private set; }
    public long X { get; private set; }
    public long Y { get; private set; }
    public double Travel { get; private set; }
    public double Net => Math.Sqrt((double)X * X + (double)Y * Y);
    public double Peak { get; private set; }
    public double LargestStep { get; private set; }
    public double? FirstMs { get; private set; }
    public double? LastMs { get; private set; }
    public string Direction => Directions.Describe(X, Y);
    public void Add(double time, long dx, long dy)
    {
        if (dx == 0 && dy == 0) return;
        Events++; X += dx; Y += dy;
        var length = Math.Sqrt((double)dx * dx + (double)dy * dy);
        Travel += length; LargestStep = Math.Max(LargestStep, length); Peak = Math.Max(Peak, Net);
        FirstMs ??= time; LastMs = time;
    }
}

public static class Directions
{
    private static readonly string[] Names = ["Right", "Down-right", "Down", "Down-left", "Left", "Up-left", "Up", "Up-right"];
    public static string Describe(long x, long y) => x == 0 && y == 0 ? "No displacement" :
        Names[((int)Math.Floor(Math.Atan2(y, x) / (Math.PI / 4) + .5) + 8) % 8];
}

public sealed class Recording
{
    public const int SampleLimit = 250_000;
    public string App => "Mouse Motion Inspector";
    public int FormatVersion => 1;
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; }
    public TestSettings Settings { get; }
    public double RecordedSeconds { get; private set; }
    public bool Complete { get; private set; }
    public string StopReason { get; private set; } = "Recording";
    public List<DeviceInfo> Devices { get; } = [];
    public List<Movement> Movements { get; } = [];
    public List<Injection> InjectedMoves { get; } = [];
    public List<CursorChange> CursorChanges { get; } = [];
    public List<DeviceChange> DeviceChanges { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<MotionStats> Streams { get; } = [];
    public bool InjectionMonitorInstalled { get; set; }
    public string OperatingSystem { get; private set; } = Environment.OSVersion.ToString();
    public string Timing => "Monotonic observation timestamps on the capture thread; not device/USB timestamps. Cursor positions are sampled on the UI thread at intervals of approximately 50 ms or longer under load.";
    public string Limits => "Raw Input identifies a Windows mouse interface, not necessarily a unique physical peripheral. Zero device handles, virtual drivers and precision touchpads can be unattributed. Injected flags do not identify a process, and absence of that flag does not prove physical input. Raw, absolute and screen coordinates must not be added or directly compared. Hook events and cursor samples can overlap raw events; they are separate observations. No automatic event-to-device correlation is inferred.";
    [JsonIgnore] public double Start { get; }
    [JsonIgnore] public double End { get; }
    [JsonIgnore] public double CaptureEnd { get; private set; }
    [JsonIgnore] public bool AtLimit => Movements.Count + InjectedMoves.Count + CursorChanges.Count + DeviceChanges.Count >= SampleLimit;
    [JsonIgnore] public bool Finished { get; private set; }
    private readonly Dictionary<(int, string), MotionStats> stats = [];
    private readonly Dictionary<int, (int X, int Y, ushort Flags)> absolute = [];
    private (int X, int Y)? cursor;

    public Recording(TestSettings settings, double start, DateTimeOffset startedAt)
    {
        Settings = settings; Start = start; End = start + settings.DurationSeconds * 1000.0; CaptureEnd = End; StartedAt = startedAt;
    }
    public bool InWindow(double time) => !Finished && time >= Start && time < CaptureEnd;
    public void RequestStop(double time) => CaptureEnd = Math.Min(CaptureEnd, time);
    public void Remember(DeviceInfo device)
    {
        var i = Devices.FindIndex(d => d.Id == device.Id);
        if (i < 0) Devices.Add(device); else Devices[i] = device;
    }
    public void Accept(Packet packet)
    {
        if (Finished || packet.Time >= CaptureEnd) return;
        if (packet.Kind is PacketKind.Arrival or PacketKind.Removal)
        {
            if (packet.Kind == PacketKind.Removal) absolute.Remove(packet.Device);
            if (InWindow(packet.Time) && !AtLimit) DeviceChanges.Add(new(packet.Time - Start, packet.Device, packet.Kind == PacketKind.Arrival));
            return;
        }
        if (packet.Kind == PacketKind.Injected)
        {
            if (InWindow(packet.Time) && !AtLimit) InjectedMoves.Add(new(packet.Time - Start, packet.X, packet.Y, packet.Flags, packet.WindowsTime));
            return;
        }
        if (packet.Kind != PacketKind.Raw) return;
        long dx = packet.X, dy = packet.Y;
        var mode = "relative";
        if ((packet.Flags & 1) != 0)
        {
            var domain = (ushort)(packet.Flags & 3);
            var previous = absolute.GetValueOrDefault(packet.Device);
            var valid = absolute.ContainsKey(packet.Device) && previous.Flags == domain;
            absolute[packet.Device] = (packet.X, packet.Y, domain);
            mode = (packet.Flags & 2) != 0 ? "absolute-virtual-desktop" : "absolute-primary-display";
            if (!valid) return; // Absolute coordinates establish a baseline, never a huge relative jump.
            dx = (long)packet.X - previous.X; dy = (long)packet.Y - previous.Y;
        }
        else absolute.Remove(packet.Device);
        if (!InWindow(packet.Time) || AtLimit || (dx == 0 && dy == 0)) return;
        var key = (packet.Device, mode);
        if (!stats.TryGetValue(key, out var stat))
        {
            stat = new() { Device = packet.Device, Mode = mode }; stats.Add(key, stat); Streams.Add(stat);
        }
        stat.Add(packet.Time - Start, dx, dy);
        Movements.Add(new(packet.Time - Start, packet.Device, mode, stat.Unit, dx, dy, stat.X, stat.Y,
            packet.X, packet.Y, packet.Flags, packet.Buttons));
    }
    public void ObserveCursor(double time, int x, int y)
    {
        if (Finished || time >= CaptureEnd) return;
        var previous = cursor; cursor = (x, y);
        if (!InWindow(time) || previous is null || AtLimit) return;
        long dx = (long)x - previous.Value.X, dy = (long)y - previous.Value.Y;
        if (dx != 0 || dy != 0) CursorChanges.Add(new(time - Start, x, y, dx, dy));
    }
    public void Finish(double time, string reason)
    {
        if (Finished) return;
        RecordedSeconds = Math.Clamp((time - Start) / 1000, 0, Settings.DurationSeconds);
        Complete = reason == "Timer complete" && time >= End && !AtLimit && Warnings.Count == 0;
        StopReason = reason; Finished = true;
    }

    internal static Recording Restore(SavedRecording saved)
    {
        var run = new Recording(saved.Settings, 0, saved.StartedAt)
        {
            Id = saved.Id, RecordedSeconds = saved.RecordedSeconds, Complete = saved.Complete && saved.RecordedSeconds >= saved.Settings.DurationSeconds && saved.StopReason == "Timer complete",
            StopReason = saved.StopReason, Finished = true, CaptureEnd = saved.RecordedSeconds * 1000,
            InjectionMonitorInstalled = saved.InjectionMonitorInstalled, OperatingSystem = saved.OperatingSystem
        };
        run.Devices.AddRange(saved.Devices);
        foreach (var move in saved.Movements)
        {
            var key = (move.Device, move.Mode);
            if (!run.stats.TryGetValue(key, out var stat))
            {
                stat = new() { Device = move.Device, Mode = move.Mode };
                run.stats.Add(key, stat); run.Streams.Add(stat);
            }
            stat.Add(move.TimeMs, move.Dx, move.Dy);
            if (stat.X != move.X || stat.Y != move.Y || stat.Unit != move.Unit)
                throw new InvalidDataException("Movement totals or units do not match the recorded steps.");
            run.Movements.Add(move);
        }
        run.InjectedMoves.AddRange(saved.InjectedMoves); run.CursorChanges.AddRange(saved.CursorChanges);
        run.DeviceChanges.AddRange(saved.DeviceChanges); run.Warnings.AddRange(saved.Warnings);
        if (run.Warnings.Count > 0 || run.AtLimit) run.Complete = false;
        return run;
    }
}
