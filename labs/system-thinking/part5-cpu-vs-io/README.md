<div align="center">

# System Thinking, Part 5

**CPU-bound vs I/O-bound: async won't save a CPU-bound endpoint**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![k6](https://img.shields.io/badge/k6-load%20test-7D64FF?logo=k6&logoColor=white)](https://k6.io/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

Two endpoints that never call each other. `/users` reads a table. `/report` counts
to five billion. They share one process and one thread pool, and that is the entire
connection between them.

Run them at the same time and `/users` goes from a **51ms** p95 to **11.17 seconds**
without a single line of its code changing, and without a single error. It never got
slower. It just could not get a thread to run on.

## Table of contents

- [The one distinction](#the-one-distinction)
- [Endpoints](#endpoints)
- [Getting started](#getting-started)
- [Step 1: the baseline](#step-1-the-baseline)
- [Step 2: the broken version](#step-2-the-broken-version)
- [Step 3: measure both at once](#step-3-measure-both-at-once)
- [Step 4: fix it and prove it](#step-4-fix-it-and-prove-it)
- [Results](#results)
- [Why the pool is pinned to 4 threads](#why-the-pool-is-pinned-to-4-threads)
- [Project structure](#project-structure)
- [Cleaning up](#cleaning-up)
- [Troubleshooting](#troubleshooting)
- [Analyze it yourself](#analyze-it-yourself)
- [License](#license)

## The one distinction

A thread is either **working** or **waiting**.

| | CPU-bound | I/O-bound |
| --- | --- | --- |
| Example | `/report` | `/users` |
| Bottleneck | CPU cores | The database |
| Thread state | Busy, actively computing | Waiting, doing nothing |
| Right fix | Get it out of the request | `async` / `await` |
| More threads | Does not help, can hurt | Frees up real capacity |

`async` only helps when the thread was waiting. `/report` is never waiting, so no
keyword will ever fix it. That is the whole lab.

## Endpoints

| Endpoint | Step | What it does |
| --- | --- | --- |
| `GET /phase1/users` | 1 | The baseline. Blocking `.ToList()`, measured alone. |
| `GET /phase2/users` | 2 | Same blocking code, measured while `/phase2/report` runs. |
| `GET /phase2/report` | 2 | Five billion iterations, inline, on the request thread. |
| `GET /phase4/users` | 4 | The I/O fix: `await ToListAsync()`. |
| `POST /phase4/report` | 4 | The CPU fix: queue the job, answer `202` immediately. |
| `GET /report/status/{id}` | 4 | Collect the answer the `202` promised. |
| `GET /threadpool` | 3 | What the thread pool is doing right now. |

Phase 1, 2 and 4 all exist at the same time in one process, so the broken and the
fixed version can be compared without a restart or a code edit.

## Getting started

Prerequisites: the .NET 10 SDK, Docker, and [k6](https://k6.io/docs/getting-started/installation/).

```bash
# Terminal 1: start PostgreSQL
docker compose up -d

# Terminal 2: run the API (listens on http://localhost:5155)
dotnet run

# Sanity check: 5,000 users get seeded on first start
curl http://localhost:5155/phase1/users
```

The table is created and seeded automatically on startup, so there is no
`dotnet ef database update` step.

## Step 1: the baseline

`/users` on its own, with nothing competing for threads:

```bash
k6 run -e PHASE=1 -e REPORT_VUS=0 loadtest/mixed.js
```

```
{ scenario:users }  avg=35.08ms  p(50)=31.84ms  p(95)=51.14ms   1,279 req/s   0.00% errors
```

**Write down that p95.** It is the entire experiment. Everything below is compared
against 51ms.

## Step 2: the broken version

Two handlers, straight from the deck, unfixed. Resist every instinct to improve them.

```csharp
// I/O-bound, done the blocking way. .ToList() holds this thread for the whole
// round trip, and the thread does no work at all while it is held.
app.MapGet("/phase2/users", (AppDbContext db) =>
{
    var users = db.Users.AsNoTracking().ToList();
    return Results.Ok(Summarize(users.Count));
});

// CPU-bound, run right here on the request thread. This loop is not waiting for
// anything. It is a core at 100% for eleven seconds, holding a thread the whole time.
app.MapGet("/phase2/report", () =>
{
    var total = ReportMath.Crunch(iterations);
    return Results.Ok(new { total, thread = Environment.CurrentManagedThreadId });
});
```

Neither endpoint is wrong on its own. `/phase2/report` alone answers in about 11
seconds, which is what five billion iterations costs. `/phase2/users` alone is the
51ms baseline you just measured.

## Step 3: measure both at once

One request at a time hides the problem completely, because nothing is competing
for a thread. The load test runs both scenarios together: 45 virtual users on
`/users`, 5 on `/report`.

```bash
k6 run -e PHASE=2 loadtest/mixed.js
```

While it runs, ask the process what its thread pool is doing:

```bash
curl http://localhost:5155/threadpool
```

```json
{ "processors": 4, "threads": 4, "busy": 4, "max": 4, "queued": 25, "reportsWaiting": 0 }
```

That `queued: 25` **is** thread pool starvation. Four threads, all of them holding
a report, and 25 pieces of work with nowhere to run. Nothing is broken. Nothing is
even slow. Work is waiting behind other work.

The k6 summary says the same thing in latency:

```
{ scenario:users }   avg=7.97s   p(50)=11.05s   p(95)=11.17s   0.00% errors
{ scenario:report }  avg=13.96s  p(50)=11.23s   p(95)=22.25s
```

Read the `/users` p95 next to the baseline: **51ms to 11.17s**, and not one request
failed. `/users` never got slower. It spent 11 seconds in a queue, because every
thread it needed was busy counting to five billion.

## Step 4: fix it and prove it

Two endpoints, two different fixes.

```csharp
// The I/O-bound fix: await. The thread goes back to the pool while the database
// works, instead of standing still holding it.
app.MapGet("/phase4/users", async (AppDbContext db) =>
{
    var users = await db.Users.AsNoTracking().ToListAsync();
    return Results.Ok(Summarize(users.Count));
});

// The CPU-bound fix: get it out of the request. You cannot await your way out of
// CPU work, so the report does not run here at all.
app.MapPost("/phase4/report", (ReportQueue queue) =>
{
    var job = queue.Add();
    return Results.Accepted($"/report/status/{job.Id}", new { jobId = job.Id });
});
```

The job is picked up by `ReportWorker`, which runs on its **own** thread
(`TaskCreationOptions.LongRunning`), not a thread pool thread. That detail is the
fix. A background job that quietly runs on a pool thread is still competing for the
same scarce resource it was competing for in step 2.

Run the identical test against phase 4:

```bash
k6 run -e PHASE=4 loadtest/mixed.js
```

```
{ scenario:users }   avg=52.73ms  p(50)=51.51ms  p(95)=74.28ms   772 req/s   0.00% errors
{ scenario:report }  avg=29.97ms  p(95)=37.06ms                  202 Accepted
```

`/users` is back to normal. `/report` now answers in 37ms because it is not doing
the work, it is queueing it:

```bash
curl -X POST http://localhost:5155/phase4/report
# { "jobId": "86fca276", "statusUrl": "/report/status/86fca276" }

curl http://localhost:5155/report/status/86fca276
# { "status": "running", "total": null, "waitedMs": 46, "ranMs": 0 }
# { "status": "done", "total": 14999999995, "waitedMs": 46, "ranMs": 11002 }
```

## Results

All three runs, same machine, same binary, 45 VUs on `/users` and 5 on `/report`:

| Run | /users p50 | /users p95 | /users req/s | errors |
| --- | --- | --- | --- | --- |
| Step 1: baseline, no report | 31.8ms | **51.1ms** | 1,279 | 0.00% |
| Step 2: broken, report inline | 11.05s | **11.17s** | 5.6 | 0.00% |
| Step 4: fixed, report queued | 51.5ms | **74.3ms** | 772 | 0.00% |

Three things worth noticing:

1. **The error rate never moved.** Starvation does not throw. It queues. If you are
   watching error rates for this, you will never see it.
2. **The fixed run is slightly worse than the baseline** (74ms vs 51ms, 772 vs 1,279
   req/s). It should be. One of the four cores is now permanently busy running
   reports. The work did not disappear, it just stopped being charged to the request
   pool.
3. **`/report` did not get faster, and that is the correct outcome.** Watch
   `reportsWaiting` in `/threadpool` climb during the phase 4 run: reports arrive
   faster than one worker can finish them. It was always a compute problem. It just
   stopped being everyone else's problem.

## Why the pool is pinned to 4 threads

Starvation is easy to see on a small machine and hard to see on a big one. Rather
than asking you to find a 4 core box, the lab makes any machine behave like one:

```json
"ThreadPool": {
  "MinWorkerThreads": 4,
  "MaxWorkerThreads": 4
}
```

`ThreadPool.SetMaxThreads` refuses any value below `Environment.ProcessorCount`, so
`Properties/launchSettings.json` also sets `DOTNET_PROCESSOR_COUNT=4`. That makes the
runtime count and size everything as if the machine had 4 cores. If you run without
that variable you will see a warning at startup and the cap will not apply.

Set both values to `0` to get .NET's normal behaviour back. The starvation is still
there on a 16 core machine, it just takes more concurrent reports to show it.

## Project structure

```
Program.cs                 all seven endpoints, one per lab step
Background/ReportQueue.cs  the job, the queue, the worker thread, and the CPU loop
Models/User.cs             the seeded entity
Data/AppDbContext.cs       EF Core + Npgsql
Migrations/                InitialCreate, applied automatically at startup
loadtest/mixed.js          k6: /users and /report hammered at the same time
docker-compose.yml         PostgreSQL 16 on host port 5432
appsettings.json           iterations, seed size, thread pool size
```

Knobs, all in `appsettings.json`:

| Setting | Default | What it does |
| --- | --- | --- |
| `Report:Iterations` | `5000000000` | Size of the CPU loop. ~11s on a modern core. |
| `Seed:UserCount` | `5000` | Rows seeded on first start. |
| `ThreadPool:MinWorkerThreads` | `4` | `0` leaves the .NET default alone. |
| `ThreadPool:MaxWorkerThreads` | `4` | `0` leaves the .NET default alone. |

And on the load test:

| Variable | Default | What it does |
| --- | --- | --- |
| `PHASE` | `2` | Which phase's endpoints to hit. |
| `USERS_VUS` | `45` | Virtual users on `/users`. |
| `REPORT_VUS` | `5` | Virtual users on `/report`. `0` removes the scenario. |
| `DURATION` | `30s` | Length of the run. |
| `REPORT_THINK` | `8` | Phase 4 only: pause between report requests, so phase 4 asks for roughly as many reports as phase 2 did. Set it to `11` to match the measured report time on your machine. |

## Cleaning up

```bash
docker compose down -v    # -v also deletes the seeded database
```

## Troubleshooting

**`address already in use` on 5432.** Another PostgreSQL is already running. Stop it,
or change the host port in `docker-compose.yml` and the `Port=` in both
`appsettings*.json` to match.

**Startup warning about the thread pool cap.** You are running without
`DOTNET_PROCESSOR_COUNT=4`. Use `dotnet run` (which reads `launchSettings.json`) or
set it yourself: `DOTNET_PROCESSOR_COUNT=4 dotnet run`.

**Phase 2 seems fine.** Check `/threadpool` first. If `max` is 16 rather than 4 the
cap did not apply, and 5 reports cannot starve 16 threads. Raise `REPORT_VUS` or fix
the processor count.

**`/report/status/{id}` says `queued` forever.** The queue is backed up: one worker,
eleven seconds per report. That is the point of note 3 above, not a bug. Lower
`Report:Iterations` if you want faster turnaround.

## Analyze it yourself

Every experiment changes exactly one thing, re-runs the same k6 command, and asks
you to explain the number before you change it.

**1. Async, but still starved.**

Phase 4's `/users` is async. Point the load test at it while the **inline** report
runs: `k6 run -e PHASE=4 loadtest/mixed.js` in one terminal, and
`curl http://localhost:5155/phase2/report` five times in another. Does `await` save
`/users`? Why not? Write the answer down before you run it.

**2. Find the number of reports it takes.**

Run phase 2 with `-e REPORT_VUS=1`, then `2`, then `3`, then `4`. At which count
does the `/users` p95 leave 51ms behind? Now look at `MaxWorkerThreads`. Why should
exactly that number be the turning point, and why does it not need to be exact?

**3. More threads is not the fix.**

Set `MaxWorkerThreads` to `32` and `MinWorkerThreads` to `32`, restart, and re-run
phase 2. p95 improves. Now look at `/threadpool` during the run and at your CPU
meter. What is the machine actually doing with 32 threads and 4 cores? Which number
got better, and which one could not?

**4. Prove the worker thread matters.**

In `ReportQueue.cs`, change `ExecuteAsync` to run `Loop` directly instead of through
`Task.Factory.StartNew(..., TaskCreationOptions.LongRunning)`. Re-run phase 4. The
p95 gets worse. Explain where that regression comes from, in terms of which pool the
worker is now taking a thread from.

**5. The error rate lied.**

Both the broken and the fixed run report `0.00% errors`. If your dashboard only
tracked error rate and throughput, at what point in step 2 would you have been
paged? What is the smallest set of numbers you would have to watch to catch this
before a user does?

**6. Serialization is CPU work too.**

The `/users` endpoints return a count, not 5,000 rows. Change `Summarize` to return
the whole list and re-run the baseline. Which p95 moved, and which category of work
did you just add to an endpoint that was supposed to be pure I/O?

**7. Do the arithmetic on step 2.**

45 virtual users, 4 threads, and every thread holding an 11 second report. Write the
one line calculation that turns those three numbers into an 11 second p95. Then
explain why `/report`'s own p95 was 22 seconds, roughly double its solo time.

**8. Where does the queue actually live?**

`reportsWaiting` grows during phase 4 and never fully drains. In a real system, what
breaks first if reports arrive faster than one worker can finish them, and which of
these is the fix: more workers, fewer reports, a faster loop, or refusing the
request? Name what you would need to measure to choose.

## License

[GPL-3.0](../../LICENSE)