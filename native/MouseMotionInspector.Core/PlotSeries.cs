namespace MouseMotionInspector.Core;

// UI-thread-owned view of one stream. Sync visits only appended source samples;
// capture/export keep the original recording and are never decimated.
public sealed class PlotSeries
{
    private const int Budget = 6000;
    private readonly List<Movement> moves = [];
    private readonly List<Bucket> buckets = [];
    private readonly List<Movement> reduced = new(Budget);
    private readonly double[] travel = new double[600];
    private readonly int[] events = new int[600];
    private IReadOnlyList<Movement>? source;
    private (int Device, string Mode) key;
    private int consumed, bucketSize = 1, reducedCount = -1, activityCount = -1;
    private double activitySeconds = -1;
    private IReadOnlyList<ActivityBucket> activity = [];
    public IReadOnlyList<Movement> Moves => moves;
    public (long MinX, long MaxX, long MinY, long MaxY) Bounds { get; private set; }

    public bool Sync(IReadOnlyList<Movement> samples, int device, string mode)
    {
        var reset = !ReferenceEquals(source, samples) || key != (device, mode) || samples.Count < consumed;
        if (reset)
        {
            source = samples; key = (device, mode); consumed = 0; bucketSize = 1;
            moves.Clear(); buckets.Clear(); reduced.Clear(); Array.Clear(travel); Array.Clear(events);
            Bounds = (0, 0, 0, 0); reducedCount = activityCount = -1;
        }
        var count = moves.Count;
        for (; consumed < samples.Count; consumed++)
        {
            var sample = samples[consumed];
            if (sample.Device == device && sample.Mode == mode) Append(sample);
        }
        return reset || moves.Count != count;
    }

    private void Append(Movement sample)
    {
        var index = moves.Count; moves.Add(sample);
        var (minX, maxX, minY, maxY) = Bounds;
        Bounds = (Math.Min(minX, sample.X), Math.Max(maxX, sample.X), Math.Min(minY, sample.Y), Math.Max(maxY, sample.Y));
        var second = Math.Clamp((int)(sample.TimeMs / 1000), 0, travel.Length - 1);
        travel[second] += Math.Sqrt((double)sample.Dx * sample.Dx + (double)sample.Dy * sample.Dy); events[second]++;
        var bucket = new Bucket(index, index, index, index, index, index);
        if (index % bucketSize == 0) buckets.Add(bucket);
        else buckets[^1] = Merge(buckets[^1], bucket);
        // Merge adjacent summaries at powers of two; each merge preserves their
        // first/last samples and X/Y extrema without scanning past raw samples.
        while (moves.Count > Budget && buckets.Count > Budget / 6)
        {
            var count = buckets.Count; var write = 0;
            for (var read = 0; read < count; read += 2)
                buckets[write++] = read + 1 < count ? Merge(buckets[read], buckets[read + 1]) : buckets[read];
            buckets.RemoveRange(write, count - write); bucketSize *= 2;
        }
    }

    private Bucket Merge(Bucket a, Bucket b) => new(a.First, b.Last,
        moves[a.MinX].X <= moves[b.MinX].X ? a.MinX : b.MinX, moves[a.MaxX].X >= moves[b.MaxX].X ? a.MaxX : b.MaxX,
        moves[a.MinY].Y <= moves[b.MinY].Y ? a.MinY : b.MinY, moves[a.MaxY].Y >= moves[b.MaxY].Y ? a.MaxY : b.MaxY);

    public IReadOnlyList<Movement> Reduced
    {
        get
        {
            if (moves.Count <= Budget) return moves;
            if (reducedCount == moves.Count) return reduced;
            reduced.Clear(); Span<int> indices = stackalloc int[6];
            foreach (var bucket in buckets)
            {
                indices[0] = bucket.First; indices[1] = bucket.Last; indices[2] = bucket.MinX;
                indices[3] = bucket.MaxX; indices[4] = bucket.MinY; indices[5] = bucket.MaxY;
                indices.Sort(); var previous = -1;
                foreach (var index in indices) { if (index != previous) reduced.Add(moves[index]); previous = index; }
            }
            reducedCount = moves.Count; return reduced;
        }
    }

    public IReadOnlyList<ActivityBucket> Activity(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) seconds = 1;
        if (activityCount == moves.Count && activitySeconds == seconds) return activity;
        var width = Math.Max(1, Math.Ceiling(seconds / 120)); var count = Math.Max(1, (int)Math.Ceiling(seconds / width));
        var distances = new double[count]; var counts = new int[count];
        for (var second = 0; second < travel.Length; second++)
        {
            var index = Math.Min(count - 1, (int)(second / width)); distances[index] += travel[second]; counts[index] += events[second];
        }
        activity = Enumerable.Range(0, count).Select(i => new ActivityBucket(i * width, Math.Min(seconds, (i + 1) * width), distances[i], counts[i])).ToArray();
        activityCount = moves.Count; activitySeconds = seconds; return activity;
    }

    private readonly record struct Bucket(int First, int Last, int MinX, int MaxX, int MinY, int MaxY);
}
