<div align="center">

# DIY-Lab: From Code to Systems

**Same endpoint, same code. Only the concurrency changes.**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)  
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)  
[![k6](https://img.shields.io/badge/k6-v2-7D64FF?logo=k6&logoColor=white)](https://k6.io/)

</div>

A tiny `/products` API that starts fast, gets a 500ms problem bolted onto it on
purpose, and is then hammered by [k6](https://k6.io/) at 1 and 50 virtual users.
The exact same code tells two completely different stories depending on how many
users arrive at once. Fixing it is one `IMemoryCache` line, and the whole chain
stays async.

> **Repo layout:** this lab lives at `labs/system-thinking/part1-code-to-systems` on
> the `main` branch. Run it from its own folder: `cd labs/system-thinking/part1-code-to-systems`,
> then the commands below. From the repo root you can also run
> `dotnet run --project labs/system-thinking/part1-code-to-systems`.

## Table of contents

- [The four steps](#the-four-steps)
- [Getting started](#getting-started)
- [Step 1: set up a simple /products API](#step-1-set-up-a-simple-products-api)
- [Step 2: introduce the problem on purpose](#step-2-introduce-the-problem-on-purpose)
- [Step 3: run it once, then run it loaded](#step-3-run-it-once-then-run-it-loaded)
- [Step 4: fix it with system thinking](#step-4-fix-it-with-system-thinking)
- [Why the numbers move](#why-the-numbers-move)
- [Project layout](#project-layout)
- [Analyze it yourself](#analyze-it-yourself)
- [License](#license)

## The four steps

1. A clean `/products` endpoint that reads a table and returns JSON.
2. A 500ms simulated database load is bolted on. This is what a busy database
   feels like under real load.
3. The load test runs twice: **1 VU** and **50 VU**. Same code, only concurrency. (**VU** = Virtual User)
4. The read-heavy endpoint gets an `IMemoryCache` and the same test runs again.

The lab is driven by two knobs in `appsettings.json`, so every step runs without
editing code:

| Setting | Step 1 | Steps 2 & 3 | Step 4 |
| --- | --- | --- | --- |
| `SimulatedDelayMs` | `0` | `500` | `500` |
| `Cache:Enabled` | `false` | `false` | `true` |

The values can also be passed as environment variables, e.g.
`SimulatedDelayMs=0 dotnet run`.

## Getting started

Prerequisites: the .NET 10 SDK, Docker, and [k6](https://k6.io/docs/getting-started/installation/).

```bash
# Terminal 1: start PostgreSQL and apply the migration (seeds 5 products)
docker compose up -d
dotnet ef database update

# Terminal 2: run the API (listens on http://localhost:5130)
dotnet run

# Sanity check
curl http://localhost:5130/products
```

The compose file maps PostgreSQL to host port **5433** so it never clashes with
other labs that use 5432.

## Step 1: set up a simple /products API

```csharp
app.MapGet("/products", async (AppDbContext db, IMemoryCache cache, IConfiguration config) =>
{
    return Results.Ok(await LoadProducts(db, config));
});
```

The read-heavy endpoint behind it is one `ToListAsync` against a `Products`
table with five seeded rows:

```csharp
static async Task<List<Product>> LoadProducts(AppDbContext db, IConfiguration config)
{
    return await db.Products.ToListAsync();
}
```

Baseline, measured locally:

```
1 VU  · 10s        p(95): ~0.8ms    errors: 0.00%    ~2,400 req/s
50 VU · 10s        p(95): ~4.4ms    errors: 0.00%    ~18,900 req/s
```

Concurrency barely moves a healthy endpoint.

## Step 2: introduce the problem on purpose

Set `SimulatedDelayMs` to `500` and the handler now spends half a second on a
simulated database load before answering. Two details matter:

```csharp
static async Task<List<Product>> LoadProducts(AppDbContext db, IConfiguration config)
{
    var simulatedMs = config.GetValue<int>("SimulatedDelayMs");
    if (simulatedMs > 0)
    {
        await db.Database.OpenConnectionAsync();
        await Task.Delay(simulatedMs);
    }

    return await db.Products.ToListAsync();
}
```

1. The delay runs **after** `OpenConnectionAsync`, so the simulated load holds a
   real connection from the pool. A literal `Task.Delay` before the query would
   not hold one, and no amount of traffic would ever make the endpoint slow. The
   pool is the scarce resource, exactly as it is with a genuinely busy database.
2. The connection string caps the pool at `Maximum Pool Size=20` and sets
   `Timeout=1`, so when 50 users queue for 20 slots the waiters time out instead
   of waiting forever.

## Step 3: run it once, then run it loaded

Same endpoint, same code, same binary. Only `--vus` changes:

```bash
k6 run --vus 1  --duration 10s loadtest/products.js
k6 run --vus 50 --duration 10s loadtest/products.js
```

Real numbers from one machine:

| Run | avg | p(95) | errors | req/s |
| --- | --- | --- | --- | --- |
| 1 VU  · 10s | 512ms | 558ms | 0.00% | 2 |
| 50 VU · 10s | 1.17s | 1.49s | 2.93% | 40 |

Fifty users, and p(95) triples while one in thirty requests fails outright. The
server logs the failures, and they are all the same line:

```
Npgsql: The connection pool has been exhausted, either raise 'Max Pool Size'
(currently 20) or 'Timeout' (currently 1 seconds) in your connection string.
```

That error is the system talking: 20 slots, 50 users holding them for 500ms each,
a 1s patience limit. Watch p(95), the error rate, and the pool. That is where the
real story is.

## Step 4: fix it with system thinking

The endpoint is read-heavy, so the fix is to stop paying the database on every
request: cache it, with a sliding expiration, and keep the whole chain async.

```csharp
app.MapGet("/products", async (AppDbContext db, IMemoryCache cache, IConfiguration config) =>
{
    var useCache = config.GetValue<bool>("Cache:Enabled");
    if (useCache)
    {
        var cached = await cache.GetOrCreateAsync("products", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromSeconds(30);
            return await LoadProducts(db, config);
        });
        return Results.Ok(cached);
    }

    return Results.Ok(await LoadProducts(db, config));
});
```

Set `Cache:Enabled` to `true`, re-run the **exact same** 50 VU test, and the
numbers collapse:

| Metric | 50 VU, before | 50 VU, after |
| --- | --- | --- |
| avg | 1.17s | 0.8ms |
| p(95) | 1.49s | 1.5ms |
| errors | 2.93% | 0.00% |
| throughput | 40 req/s | ~61,000 req/s |

The first request still pays the 500ms to fill the cache. Every request for the
next 30 seconds of activity answers from memory, and the connection pool never
even wakes up.

## Why the numbers move

The endpoint's own code did not change between the 1 VU and 50 VU runs. What
changed is how many users share the same scarce resource, the connection pool:

- At 1 VU, one request holds one of the 20 connections for ~500ms and goes home.
  No contention, ~512ms per request, no errors.
- At 50 VU, all 20 slots are taken. The extra 30 requests queue, and the queue
  pushes the slowest requests past 1s, which trips the connection timeout. Latency
  triples and some requests fail.
- With caching, repeat requests never reach the database at all, so the pool
  stops being the bottleneck.

This is the system-thinking shift: **a single endpoint has no meaningful latency
number; it has one per concurrency level.** The code is the same, the system is
not.

## Project layout

```
Domain/           Product entity
Infrastructure/   AppDbContext (EF Core + Npgsql)
Migrations/       InitialCreate with 5 seeded products
loadtest/         k6 script (VUs come from the CLI flag)
Program.cs        the /products endpoint with the config-driven slow path + cache
docker-compose.yml  PostgreSQL 16 on host port 5433
appsettings.json  SimulatedDelayMs, Cache:Enabled, and the connection string
```

## Analyze it yourself

The fastest way to learn this lab is to break it on purpose. Every experiment
changes exactly one thing, re-runs the same `k6` command, and asks you to explain
the number before you change it. Start the API first, then work through them.

**1. Find the knee.**

Run the slow endpoint (delay 500, no cache) at 10, 20, 25, 30, and 40 VUs:

```bash
k6 run --vus 10 --duration 10s loadtest/products.js
```

At what count does p(95) stop being flat and start climbing? Now read the
connection string. Why should exactly that number be the turning point?

**2. The pool is the scarce resource.**

Change `Maximum Pool Size` to `5`, restart, run 50 VU. Then set it to `50` and run
50 VU again. How do p(95) and the error rate move in each case? When nobody queues,
what does the latency return to, and why?

**3. Timeout is the error switch.**

Set `Timeout=10`, restart, run 50 VU. The errors should disappear. What is the
latency doing instead? Which is worse for a real user: a 1.5s wait that fails, or
a 1.5s wait that succeeds? Your answer says what a timeout is actually for.

**4. Prove the pool is what saturates.**

Delete the `await db.Database.OpenConnectionAsync();` line so the 500ms delay runs
before any connection is held. Run 50 VU. Why does the endpoint behave like the
baseline again? This is the whole "busy database" illusion in one line.

**5. The cache is cold exactly once.**

Restart with `Cache:Enabled=true` and run `--duration 1s` at 50 VU, then
`--duration 10s` at 50 VU. Where did the 500ms go in the second run? Now set
`SlidingExpiration` to 5 seconds and run `--duration 30s`: does the cache ever
expire while traffic is continuous? What would the output look like the moment it
did?

**6. Do the arithmetic on the serial path.**

At 1 VU the slow endpoint does ~2 req/s and p(50) is ~500ms. Write the one-line
calculation that produces both numbers from a single 500ms request. At 50 VU,
throughput jumps to ~40 req/s. Given 20 pool slots and 500ms per hold, roughly how
many "waves" of 20 requests drain per second, and does that match ~40?

**7. Errors must match the server.**

Run 50 VU slow, then count `pool has been exhausted` in the server console.
Compare it with k6's `http_req_failed`. Why must the two numbers agree? What would
it mean if they did not?

**8. The cache key is a system decision.**

The key is the literal string `"products"`. If the endpoint grew a `?category=`
parameter, would one shared key still be correct? If two different endpoints shared
one key, or the key changed per user, what breaks? Name the property a cache key
must have before a cache is safe to add.

## License

[GPL-3.0](../../LICENSE)