namespace MouseMotionInspector.Core;

public sealed record ActivityBucket(double StartSeconds, double EndSeconds, double Travel, int Events);

public static class PlotData
{
    public static IReadOnlyList<ActivityBucket> Activity(IReadOnlyList<Movement> moves, double seconds, int maximumBuckets = 120)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) seconds = 1;
        var width = Math.Max(1, Math.Ceiling(seconds / Math.Max(1, maximumBuckets)));
        var count = Math.Max(1, (int)Math.Ceiling(seconds / width)); var travel = new double[count]; var events = new int[count];
        foreach (var move in moves)
        {
            var index = Math.Clamp((int)(move.TimeMs / 1000 / width), 0, count - 1);
            travel[index] += Math.Sqrt((double)move.Dx * move.Dx + (double)move.Dy * move.Dy); events[index]++;
        }
        return Enumerable.Range(0, count).Select(i => new ActivityBucket(i * width, Math.Min(seconds, (i + 1) * width), travel[i], events[i])).ToArray();
    }

    // Keep chronological endpoints and X/Y extremes in each bucket. A simple
    // every-Nth stride can erase a brief out-and-back excursion entirely.
    public static IReadOnlyList<Movement> Reduce(IReadOnlyList<Movement> moves, int budget = 6000)
    {
        if (budget < 6) throw new ArgumentOutOfRangeException(nameof(budget));
        if (moves.Count <= budget) return moves;
        var size = (int)Math.Ceiling(moves.Count / (double)(budget / 6));
        var result = new List<Movement>();
        for (var start = 0; start < moves.Count; start += size)
        {
            var end = Math.Min(moves.Count, start + size); var minX = start; var maxX = start; var minY = start; var maxY = start;
            for (var i = start + 1; i < end; i++)
            {
                if (moves[i].X < moves[minX].X) minX = i; if (moves[i].X > moves[maxX].X) maxX = i;
                if (moves[i].Y < moves[minY].Y) minY = i; if (moves[i].Y > moves[maxY].Y) maxY = i;
            }
            foreach (var index in new[] { start, end - 1, minX, maxX, minY, maxY }.Distinct().Order()) result.Add(moves[index]);
        }
        return result;
    }

    public static double TickStep(double range, int target = 6)
    {
        if (!double.IsFinite(range) || range <= 0) return 1;
        var raw = range / Math.Max(1, target); var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude; return (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;
    }
}
