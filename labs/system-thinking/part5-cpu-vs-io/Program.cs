using Microsoft.EntityFrameworkCore;
using SystemThinkingPart5.Background;
using SystemThinkingPart5.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Lab knob: how big the thread pool is allowed to get.
//
// Starvation is easy to see on a small machine and hard to see on a big one,
// so the lab pins the pool instead of asking you to find smaller hardware.
// Set both values to 0 in appsettings.json to get .NET's normal behaviour back.
//
// .NET refuses a max below Environment.ProcessorCount, which is why
// launchSettings.json also sets DOTNET_PROCESSOR_COUNT=4: it makes the runtime
// behave as if this were a 4 core box.
// ---------------------------------------------------------------------------
var minThreads = builder.Configuration.GetValue("ThreadPool:MinWorkerThreads", 0);
var maxThreads = builder.Configuration.GetValue("ThreadPool:MaxWorkerThreads", 0);

ThreadPool.GetMinThreads(out _, out var minIoThreads);
ThreadPool.GetMaxThreads(out _, out var maxIoThreads);

if (minThreads > 0)
{
    ThreadPool.SetMinThreads(minThreads, minIoThreads);
}

if (maxThreads > 0 && !ThreadPool.SetMaxThreads(maxThreads, maxIoThreads))
{
    Console.WriteLine(
        $"WARNING: could not cap the pool at {maxThreads} threads. .NET will not go below " +
        $"Environment.ProcessorCount, which is {Environment.ProcessorCount} here. " +
        "Run with DOTNET_PROCESSOR_COUNT=4 (see the README).");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<ReportQueue>();
builder.Services.AddHostedService<ReportWorker>();

var app = builder.Build();

var iterations = builder.Configuration.GetValue<long>("Report:Iterations");

// Create the table and seed users on startup, so the only setup step is
// "docker compose up".
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var userCount = builder.Configuration.GetValue("Seed:UserCount", 5000);

    if (!db.Users.Any())
    {
        db.Database.ExecuteSqlInterpolated(
            $"""
             INSERT INTO "Users" ("Name", "Email")
             SELECT 'User ' || lpad(i::text, 5, '0'), 'user' || i || '@diy-lab.test'
             FROM generate_series(1, {userCount}) i
             """);
    }
}

// ===========================================================================
// STEP 1: the baseline.
// /users on its own, with nothing competing for threads. Whatever p95 you
// measure here is the number the rest of the lab is compared against.
// ===========================================================================
app.MapGet("/phase1/users", (AppDbContext db) =>
{
    var users = db.Users.AsNoTracking().ToList();
    return Results.Ok(Summarize(users.Count));
});

// ===========================================================================
// STEP 2: the broken version.
// ===========================================================================

// I/O-bound, done the blocking way.
// .ToList() (not ToListAsync) holds this thread for the whole database round
// trip. The thread is busy doing nothing: it is just waiting for an answer.
app.MapGet("/phase2/users", (AppDbContext db) =>
{
    var users = db.Users.AsNoTracking().ToList();
    return Results.Ok(Summarize(users.Count));
});

// CPU-bound, run right here on the request thread.
// This loop is not waiting for anything. It is a core at 100% for several
// seconds, and for all of those seconds it is holding a thread that
// /phase2/users needs.
app.MapGet("/phase2/report", () =>
{
    var total = ReportMath.Crunch(iterations);
    return Results.Ok(new { total, thread = Environment.CurrentManagedThreadId });
});

// ===========================================================================
// STEP 4: the fix. Two endpoints, two different fixes.
// ===========================================================================

// The I/O-bound fix: await.
// The thread goes back to the pool while the database works, so it can serve
// other requests instead of standing still. Same query, same database, same
// latency: only the thread's behaviour changed.
app.MapGet("/phase4/users", async (AppDbContext db) =>
{
    var users = await db.Users.AsNoTracking().ToListAsync();
    return Results.Ok(Summarize(users.Count));
});

// The CPU-bound fix: get it out of the request.
// You cannot await your way out of CPU work, so the report does not run here
// at all. The request adds a job to the queue and answers 202 immediately;
// the worker thread runs it. Same 8 seconds of CPU, charged to a thread that
// nobody is waiting on.
app.MapPost("/phase4/report", (ReportQueue queue) =>
{
    var job = queue.Add();
    return Results.Accepted($"/report/status/{job.Id}", new
    {
        jobId = job.Id,
        statusUrl = $"/report/status/{job.Id}"
    });
});

// The other half of a 202: the caller comes back to collect the answer.
app.MapGet("/report/status/{id}", (string id, ReportQueue queue) =>
{
    var job = queue.Find(id);

    return job is null
        ? Results.NotFound(new { id, error = "no such job" })
        : Results.Ok(new
        {
            jobId = job.Id,
            status = job.Status.ToString().ToLowerInvariant(),
            job.Total,
            job.WaitedMs,
            job.RanMs
        });
});

// ===========================================================================
// STEP 3's instrument: what the thread pool is doing right now.
//
// The deck watches these with dotnet-counters. This endpoint shows the same
// two numbers with nothing to install:
//   threads   how many threads the pool has grown to
//   queued    work that has arrived and has no thread to run on yet
//
// A "queued" number above zero IS thread pool starvation. Nothing is broken
// and nothing is slow. Work is simply waiting behind other work.
// ===========================================================================
app.MapGet("/threadpool", (ReportQueue queue) =>
{
    ThreadPool.GetMaxThreads(out var maxWorkers, out _);
    ThreadPool.GetAvailableThreads(out var freeWorkers, out _);

    return Results.Ok(new
    {
        processors = Environment.ProcessorCount,
        threads = ThreadPool.ThreadCount,
        busy = maxWorkers - freeWorkers,
        max = maxWorkers,
        queued = ThreadPool.PendingWorkItemCount,
        reportsWaiting = queue.WaitingCount
    });
});

ThreadPool.GetMaxThreads(out var poolMax, out _);
app.Logger.LogInformation("processors={Processors} maxWorkerThreads={Max} reportIterations={Iterations:N0}",
    Environment.ProcessorCount, poolMax, iterations);

app.Run();

// The endpoints return a count, not 5,000 rows. The query still reads every
// row, so the database work is real, but serializing 5,000 rows on every
// request would add CPU work to the endpoint that is meant to be the pure
// I/O-bound half of the experiment.
static object Summarize(int count) => new
{
    count,
    thread = Environment.CurrentManagedThreadId
};