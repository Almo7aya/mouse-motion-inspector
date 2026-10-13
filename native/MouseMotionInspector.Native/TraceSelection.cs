namespace MouseMotionInspector.Native;

internal readonly record struct TraceRow((int Device, string Mode) Key, long Movements);

// Keep a deliberate user selection, but do not leave the automatic selection
// on a quiet keyboard interface when another interface has a recorded trace.
internal sealed class TraceSelection
{
    internal (int Device, string Mode)? Key { get; private set; }
    private bool manuallySelected;
    private Guid? runId;

    internal void SetContext(Guid? value)
    {
        if (runId == value) return;
        runId = value;
        Key = null;
        manuallySelected = false;
    }

    internal void Select((int Device, string Mode) key)
    {
        Key = key;
        manuallySelected = true;
    }

    internal void Update(IReadOnlyList<TraceRow> rows)
    {
        var current = rows.FirstOrDefault(row => row.Key == Key);
        var exists = Key is not null && rows.Any(row => row.Key == Key);
        if (exists && (manuallySelected || current.Movements > 0)) return;

        manuallySelected = false;
        var moving = rows.OrderByDescending(row => row.Movements).FirstOrDefault();
        if (moving.Movements > 0) Key = moving.Key;
        else if (!exists) Key = rows.Count > 0 ? rows[0].Key : null;
    }
}
