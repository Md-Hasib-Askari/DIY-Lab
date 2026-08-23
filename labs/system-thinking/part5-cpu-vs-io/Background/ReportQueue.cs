using System.Collections.Concurrent;
using System.Diagnostics;

namespace SystemThinkingPart5.Background;

// Where a job is in its life. Queued -> Running -> Done.
public enum JobStatus
{
    Queued,
    Running,
    Done
}

// One job = one report someone asked for.
public class ReportJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public long? Total { get; set; }
    public long WaitedMs { get; set; }               // time spent in the queue
    public long RanMs { get; set; }                  // time spent computing

    // Set when the job joins the queue, used to work out WaitedMs later.
    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;
}

// The waiting line for reports.
// The request handler only adds to it, which takes microseconds. That is why
// /phase4/report can answer instantly no matter how slow a report is.
public class ReportQueue
{
    private readonly ConcurrentQueue<ReportJob> _waiting = new();
    private readonly ConcurrentDictionary<string, ReportJob> _all = new();

    public ReportJob Add()
    {
        var job = new ReportJob();
        _all[job.Id] = job;
        _waiting.Enqueue(job);
        return job;
    }

    public bool TryTakeNext(out ReportJob? job) => _waiting.TryDequeue(out job);

    public ReportJob? Find(string id) => _all.GetValueOrDefault(id);

    public int WaitingCount => _waiting.Count;
}

// The worker that actually runs the reports.
//
// TaskCreationOptions.LongRunning gives this loop its OWN thread instead of a
// thread pool thread. That is the whole fix: the report still costs the same
// seconds of CPU, but it no longer takes a thread away from /users.
//
// One worker = one core's worth of reports at most, no matter how many people
// ask for one.
public class ReportWorker(ReportQueue queue, IConfiguration config, ILogger<ReportWorker> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => Loop(stoppingToken), TaskCreationOptions.LongRunning);

    private void Loop(CancellationToken stoppingToken)
    {
        var iterations = config.GetValue<long>("Report:Iterations");
        logger.LogInformation("report worker ready on its own thread");

        while (!stoppingToken.IsCancellationRequested)
        {
            // A real app would use a Channel and wait properly. Checking every
            // 50ms keeps this loop readable, and 50ms is nothing next to a
            // report that takes seconds.
            if (!queue.TryTakeNext(out var job) || job is null)
            {
                Thread.Sleep(50);
                continue;
            }

            job.WaitedMs = (long)(DateTimeOffset.UtcNow - job.QueuedAt).TotalMilliseconds;
            job.Status = JobStatus.Running;

            var sw = Stopwatch.StartNew();
            job.Total = ReportMath.Crunch(iterations);
            sw.Stop();

            job.RanMs = sw.ElapsedMilliseconds;
            job.Status = JobStatus.Done;
            logger.LogInformation("[{Job}] report done in {Ms}ms (waited {Waited}ms in the queue)",
                job.Id, job.RanMs, job.WaitedMs);
        }
    }
}

// The CPU-bound work itself, in one place, so phase 2 and phase 4 run the
// exact same loop. No database, no network, no waiting: every millisecond in
// here is a core doing arithmetic.
public static class ReportMath
{
    public static long Crunch(long iterations)
    {
        long total = 0;

        for (long i = 0; i < iterations; i++)
        {
            total += i % 7;
        }

        return total;
    }
}