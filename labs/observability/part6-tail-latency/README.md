<div align="center">

# DIY-Lab: Hide a Slow Path Inside a Fast Average

**A runnable lab for learning to read tail latency, not just averages.**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![k6](https://img.shields.io/badge/k6-v2-7D64FF?logo=k6&logoColor=white)](https://k6.io/)

</div>

A patient lookup API with a deliberately hidden problem: 5% of requests stall
for four seconds, simulating an overloaded dependency like a starved connection
pool. The average looks fine. The tail does not.

> **Repo layout:** this lab lives at `labs/observability/part6-tail-latency` on the
> `main` branch. Run it from its own folder: `cd labs/observability/part6-tail-latency`,
> then the commands below. From the repo root you can also run
> `dotnet run --project labs/observability/part6-tail-latency`.

## The scenario

```csharp
if (Random.Shared.Next(0, 100) < 5)
{
    await Task.Delay(4000);
}

var patient = await db.Patients.FindAsync(id);
```

Five percent of lookups quietly stall for four seconds. Averages smooth this
out into a number that looks healthy, which is exactly how real outages hide.

## Getting started

1. **Start a PostgreSQL server** and create a database:

   ```sql
   CREATE DATABASE diy_lab;
   ```

2. **Set your connection string** in `appsettings.json` and
   `appsettings.Development.json`:

   ```json
   "ConnectionStrings": {
     "Default": "Host=localhost;Port=5432;Database=diy_lab;Username=postgres;Password=yourpassword"
   }
   ```

3. **Apply the migration** (it also seeds 3 patients):

   ```bash
   dotnet ef database update
   ```

4. **Run the app** (http profile, listens on `http://localhost:5118`):

   ```bash
   dotnet run
   ```

5. **Sanity-check a lookup:**

   ```bash
   curl http://localhost:5118/patients/1
   ```

## Step 1: Run the load test

Install [k6](https://k6.io/docs/getting-started/installation/), then:

```bash
k6 run loadtest/patients.js
```

The script runs 20 virtual users for 60 seconds of steady traffic against
`/patients/{id}`, with summary statistics configured via:

```js
summaryTrendStats: ['avg', 'min', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
```

## Step 2: Read the tail, not just the average

k6 prints avg, min, max, p(50), p(90), p(95), and p(99) for `http_req_duration`:

```
http_req_duration....: avg=179.6ms min=342.31µs max=4.01s p(50)=481.36µs p(90)=3.67ms p(95)=16.21ms p(99)=4s
```

Read it across the percentiles:

- **p(50) ~481µs** - the typical request is sub-millisecond. Looks perfect.
- **avg ~180ms** - polluted by the slow path (5% x 4000ms ~ 200ms).
- **p(95) ~16ms** - still clean. The 5% slow path sits right at the p(95) edge.
- **p(99) / max ~4s** - the only place the stall shows clearly.

The key lesson: an average-based SLO would never catch this. Only the tail
percentiles reveal the ~60 requests per minute that each take a full four
seconds. That unlucky five percent is a real doctor, every time.

## Step 3: Fix the tail, then measure again

Find what is actually stalling. For a starved dependency the real fixes are
connection pool limits, sensible timeouts, and caching the lookup.

Change the code, then re-run the **exact same** load test and compare:

| Metric | Before | After |
| --- | --- | --- |
| p(95) | ~1620ms | ~180ms |
| p(99) | ~4020ms | ~310ms |

Same average traffic. A completely different experience for the unlucky five
percent.

## Project layout

```
Domain/           Patient entity
Infrastructure/   AppDbContext (EF Core + Npgsql)
Migrations/       InitialCreate with seed data
loadtest/         k6 script
Program.cs        the /patients/{id} endpoint with the 5% slow path
```

## License

[GPL-3.0](../../LICENSE)
