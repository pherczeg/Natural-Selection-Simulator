/// <summary>
/// Main-thread-only counters used by the benchmark to compare the legacy and ECS
/// simulation paths. A "sensor query" is one creature receiving one closest-target
/// search pass (food, prey or mate), regardless of which path produced it.
/// </summary>
public static class SimulationPerfCounters
{
    public static long TotalSensorQueries { get; private set; }

    public static void AddSensorQueries(int count)
    {
        if (count > 0)
        {
            TotalSensorQueries += count;
        }
    }

    public static void Reset()
    {
        TotalSensorQueries = 0;
    }
}
