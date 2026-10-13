using System.Text;
using System.Text.Json;

namespace MouseMotionInspector.Core;

internal sealed record SavedRecording(int FormatVersion, Guid Id, DateTimeOffset StartedAt, TestSettings Settings,
    double RecordedSeconds, bool Complete, string StopReason, DeviceInfo[] Devices, Movement[] Movements,
    Injection[] InjectedMoves, CursorChange[] CursorChanges, DeviceChange[] DeviceChanges, string[] Warnings,
    bool InjectionMonitorInstalled, string OperatingSystem);

public static class RecordingFiles
{
    public const long MaximumFileBytes = 128 * 1024 * 1024;
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 32 };

    public static IReadOnlyList<Recording> Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumFileBytes) throw new InvalidDataException("Recording files must be smaller than 128 MiB.");
        using var json = JsonDocument.Parse(stream, new() { MaxDepth = 32 });
        return Parse(json.RootElement);
    }

    public static IReadOnlyList<Recording> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a recording object.");
        if (root.TryGetProperty("Runs", out var list) && list.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Runs must be an array.");
        var elements = root.TryGetProperty("Runs", out var runs) ? runs.EnumerateArray().ToArray() : [root];
        if (elements.Length is < 1 or > 100) throw new InvalidDataException("Open between 1 and 100 recordings at a time.");
        var result = new List<Recording>(); long observations = 0;
        foreach (var element in elements)
        {
            var saved = element.Deserialize<SavedRecording>(JsonOptions) ?? throw new InvalidDataException("Missing recording.");
            if (saved.FormatVersion != 1) throw new InvalidDataException("This recording format is not supported. Expected format version 1.");
            if (saved.Id == Guid.Empty || saved.Settings is null || saved.Devices is null || saved.Movements is null ||
                saved.InjectedMoves is null || saved.CursorChanges is null || saved.DeviceChanges is null || saved.Warnings is null ||
                saved.StopReason is null || saved.OperatingSystem is null)
                throw new InvalidDataException("The recording is missing required fields.");
            var settings = saved.Settings;
            if (settings.DurationSeconds is < 1 or > 600 || settings.SettleSeconds is < 0 or > 60 ||
                settings.Dpi is < 1 or > 100000 || settings.PollingHz is < 1 or > 32000 ||
                string.IsNullOrWhiteSpace(settings.Surface) || settings.Connection is null || settings.Notes is null ||
                !double.IsFinite(saved.RecordedSeconds) || saved.RecordedSeconds < 0 || saved.RecordedSeconds > settings.DurationSeconds)
                throw new InvalidDataException("The recording contains invalid settings or duration.");
            var count = (long)saved.Movements.Length + saved.InjectedMoves.Length + saved.CursorChanges.Length + saved.DeviceChanges.Length;
            observations += count;
            if (count > Recording.SampleLimit || observations > 1_000_000 || saved.Devices.Length > 1024 || saved.Warnings.Length > 1024)
                throw new InvalidDataException("Recording limits exceeded.");
            if (saved.Devices.Any(d => d is null || d.Id < 1 || d.Name is null || d.Path is null) ||
                saved.Devices.Select(d => d.Id).Distinct().Count() != saved.Devices.Length)
                throw new InvalidDataException("Invalid or duplicate device identities.");
            var ids = saved.Devices.Select(d => d.Id).ToHashSet(); var previous = -1d;
            bool ValidTime(double time) => double.IsFinite(time) && time >= 0 && time <= saved.RecordedSeconds * 1000 + .001;
            foreach (var move in saved.Movements)
            {
                if (move is null || !ValidTime(move.TimeMs) || move.TimeMs < previous || !ids.Contains(move.Device) ||
                    move.Mode is not ("relative" or "absolute-primary-display" or "absolute-virtual-desktop") ||
                    move.Dx is < int.MinValue or > int.MaxValue || move.Dy is < int.MinValue or > int.MaxValue || (move.Dx == 0 && move.Dy == 0))
                    throw new InvalidDataException("Invalid movement sample, timestamp, or device reference.");
                previous = move.TimeMs;
            }
            if (saved.InjectedMoves.Any(m => m is null || !ValidTime(m.TimeMs)) || saved.CursorChanges.Any(m => m is null || !ValidTime(m.TimeMs)) ||
                saved.DeviceChanges.Any(m => m is null || !ValidTime(m.TimeMs) || !ids.Contains(m.Device)) || saved.Warnings.Any(w => w is null))
                throw new InvalidDataException("Invalid auxiliary observation.");
            result.Add(Recording.Restore(saved));
        }
        return result;
    }

    // Write beside the destination and rename only after closing a complete file.
    public static void WriteAtomic(string path, string contents)
    {
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, contents, new UTF8Encoding(false)); File.Move(temporary, full, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
