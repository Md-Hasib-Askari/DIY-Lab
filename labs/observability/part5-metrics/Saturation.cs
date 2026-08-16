using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Saturation: how full this process's resources are right now (CPU, memory,
/// DB connections, thread pool). Each gauge is read live on every scrape of
/// /metrics and reported from 0 to 100%. Latency can stay fine while these
/// creep toward their limit, and that gap is the early warning.
/// </summary>
public static class Saturation
{
    // Memory is reported as a percentage of this budget.
    private const long MemoryBudgetBytes = 512L * 1024 * 1024;

    private static readonly Process CurrentProcess = Process.GetCurrentProcess();

    // CPU is read on each scrape, so compare the processor time used since
    // the previous scrape against the wall-clock time that passed.
    private static double _lastCpu;
    private static double _lastWall;

    // A simulated database connection pool. /batch borrows and returns one
    // connection per request, so this gauge moves with real load. Five
    // connections means four active /batch slots push it to 80%, right at
    // the alert limit.
    private const int DbPoolSize = 5;
    private static int _dbActive;
    private static readonly object DbLock = new();

    public static void Register(Meter meter)
    {
        meter.CreateObservableGauge("saturation.cpu",
            () =>
            {
                double wallNow = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                double cpuNow = CurrentProcess.TotalProcessorTime.TotalSeconds;

                double pct = 0;
                if (_lastWall != 0 && wallNow > _lastWall)
                {
                    pct = (cpuNow - _lastCpu) / (wallNow - _lastWall) / Environment.ProcessorCount * 100.0;
                }

                _lastWall = wallNow;
                _lastCpu = cpuNow;
                return Math.Clamp(pct, 0, 100);
            },
            unit: "%",
            description: "Process CPU as a percentage of one core");

        meter.CreateObservableGauge("saturation.memory",
            () => CurrentProcess.WorkingSet64 / (double)MemoryBudgetBytes * 100.0,
            unit: "%",
            description: "Working set as a percentage of the 512 MB budget");

        meter.CreateObservableGauge("saturation.db_connections",
            () =>
            {
                lock (DbLock) { return _dbActive / (double)DbPoolSize * 100.0; }
            },
            unit: "%",
            description: "Busy connections out of a pool of 5");

        meter.CreateObservableGauge("saturation.thread_pool",
            () =>
            {
                ThreadPool.GetMaxThreads(out int maxWorkerThreads, out _);
                return ThreadPool.ThreadCount / (double)maxWorkerThreads * 100.0;
            },
            unit: "%",
            description: "Thread pool threads as a percentage of the maximum");
    }

    // /batch calls these around its work so the connection gauge moves.
    public static void DbAcquire()
    {
        lock (DbLock) { _dbActive = Math.Min(_dbActive + 1, DbPoolSize); }
    }

    public static void DbRelease()
    {
        lock (DbLock) { _dbActive = Math.Max(_dbActive - 1, 0); }
    }
}