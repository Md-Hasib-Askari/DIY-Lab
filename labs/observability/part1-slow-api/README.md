<div align="center">

# Observability, Part 1

**Diagnosing a slow .NET API using only built-in tooling**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-Npgsql-86BCDA)](https://learn.microsoft.com/ef/core/)
[![Docker](https://img.shields.io/badge/Docker-24db7ed?logo=docker&logoColor=white)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

A healthcare Patient API that takes ~2 seconds per request, and gives you no way to see
why: no request logs, no metrics, no traces. This lab builds that problem on purpose,
then fixes the blindness using only what .NET already ships: `ILogger`, `Stopwatch`, and
a per-request correlation ID. No APM vendor, no Prometheus, no Jaeger.

Each phase is exposed as its own route (`/phase1/...` to `/phase4/...`) so all four can be
run and compared in a single process.

> **Repo layout:** this lab lives at `labs/observability/part1-slow-api` on the `main`
> branch. Run it from its own folder: `cd labs/observability/part1-slow-api`, then the
> commands below. From the repo root you can also run
> `dotnet run --project labs/observability/part1-slow-api`.

## Table of contents

- [How it works](#how-it-works)
- [The four phases](#the-four-phases)
  - [Phase 1: Scaffold](#phase-1-scaffold-fast-no-observability)
  - [Phase 2: Break it](#phase-2-break-it-on-purpose)
  - [Phase 3: Reproduce](#phase-3-reproduce-the-black-box)
  - [Phase 4: Diagnose](#phase-4-instrument-and-diagnose)
- [Getting started](#getting-started)
- [Cleaning up everything](#cleaning-up-everything)
- [Project structure](#project-structure)
- [Results](#results)
- [Troubleshooting](#troubleshooting)
- [License](#license)

## How it works

The endpoint `GET /phase1/patients/{id}` is deliberately made slow in two independent
ways in later phases:

| Time bomb | How it is implemented | Cost |
| --- | --- | --- |
| Unindexed lookup | `FirstOrDefaultAsync(p => p.Name == ...)` over 1M rows, no index on `Name` | tens to hundreds of ms (cache dependent) |
| Fake external call | `HttpClient` backed by `FakeHttpMessageHandler`, which sleeps 2 seconds and returns 200 | ~2000 ms, every request |

Both fail silently. Nothing logs a request, so the 2 seconds are unexplainable. Phase 4
adds a correlation ID and stopwatches, which pin the blame on the external call.

## The four phases

### Phase 1: Scaffold, fast, no observability

`GET /phase1/patients/{id}` loads a patient with its prescriptions by primary key. No
logging, no metrics, no middleware.

```bash
curl http://localhost:5176/phase1/patients/1
```

Instant, no perceptible delay.

### Phase 2: Break it on purpose

Two time bombs go into the handler: the unindexed name lookup and the fake 2-second
external call described above.

```bash
curl http://localhost:5176/phase2/patients/1
```

The same JSON comes back, but it now takes ~2 seconds.

### Phase 3: Reproduce, the black box

Phase 3 changes no code: it is the Phase 2 handler under a separate route, run just to
observe the console.

Hit the endpoint five times and read the console.

```bash
for i in 1 2 3 4 5; do curl http://localhost:5176/phase3/patients/$i; done
```

The console shows EF's SQL lines (`Executed DbCommand (89ms)`) but nothing about the
request: no method, no path, no duration. The 2-second gap is invisible. Captured output
in [snapshots/phase3](snapshots/phase3-curl.txt).

### Phase 4: Instrument and diagnose

Three small changes in `Phases/Phase4.cs`:

1. A correlation ID per request: `var reqId = Guid.NewGuid();`, included in every log message
2. A `Stopwatch` around the DB call and one around the external call
3. Structured log lines: `[{ReqId}] DB lookup: {Ms}ms`, `[{ReqId}] External call: {Ms}ms`, and `[{ReqId}] Total: {Ms}ms`

Replay the same request:

```bash
curl http://localhost:5176/phase4/patients/1
```

Expected console:

```text
info: Program[0]
      [9e6af021-c12d-48c5-a58f-d86a354e4232] DB lookup: 645ms
info: Program[0]
      [9e6af021-c12d-48c5-a58f-d86a354e4232] External call: 2011ms
info: Program[0]
      [9e6af021-c12d-48c5-a58f-d86a354e4232] Total: 2866ms
```

The verdict: the external call is a constant ~2000 ms on every request. The DB scan is
expensive only on the first, cold hit and drops to tens of ms once the pages are cached.
The external call is the problem, and the logs now say so. Before/after diff:
[snapshots/phase-diff.txt](snapshots/phase-diff.txt).

## Getting started

Prerequisites: .NET SDK and Docker.

```bash
# 1. Start Postgres (port 5432, db labdb, user/pass lab/lab)
docker compose up -d --wait

# 2. Run the API. Creates the schema from the EF migration, 
# then seeds 1,000,000 patients (~15s)
dotnet run

# 3. In a second terminal, request a patient
curl http://localhost:5176/phase1/patients/1
```

Seeding runs only when the `Patients` table is empty. The schema itself comes from an EF
Core migration: `Program.cs` calls `db.Database.Migrate()` at startup, so a fresh database
gets all tables automatically. To apply the migration manually instead:

```bash
dotnet ef database update
```

For a fully fresh replay: `docker compose down -v`, then the commands above again.

## Cleaning up everything

The whole lab is disposable and recreated from scratch by `docker compose up -d --wait`
plus `dotnet run`. To tear it all down:

```bash
# 1. Stop the API (Ctrl+C in its terminal, or:)
pkill -f "dotnet run"

# 2. Remove the database container, its data volume, and the compose network
docker compose down -v

# 3. If you linked the database to your pgAdmin network, remove that link too
docker network disconnect <your-pgadmin-network> observability-lab-pg

# 4. Confirm nothing is left behind
docker ps -a | grep observability-lab-pg        # no container
docker volume ls | grep observability-lab-pg    # no data volume
docker network ls | grep observability-lab-net  # no network
```

The next `dotnet run` re-applies the migration and seeds the million rows again.

## Project structure

| File | Purpose |
| --- | --- |
| `Program.cs` | host setup, seeding, maps all four phase routes |
| `Phases/Phase1.cs` | fast endpoint by primary key |
| `Phases/Phase2.cs` | unindexed lookup + fake 2s external call |
| `Phases/Phase3.cs` | reuses the Phase 2 handler under its own route |
| `Phases/Phase4.cs` | correlation ID + stopwatches, structured logs |
| `Models/Patient.cs` | `Patient` and `Prescription` entities |
| `Models/Responses.cs` | API response DTOs |
| `Data/AppDbContext.cs` | EF Core context |
| `Migrations/` | EF Core schema migration (applied at startup via `Migrate()`) |
| `Infrastructure/FakeHttpMessageHandler.cs` | fake external API: 2s delay, HTTP 200 |
| `docker-compose.yml` | Postgres on port 5432, network `observability-lab-net` |
| `appsettings.Development.json` | connection string, seed size |
| `scripts/capture-snapshots.sh` | regenerates `snapshots/` (DB, API, requests, files) |
| `snapshots/` | captured console and curl output for phases 3 and 4 |
| `ObservabilityPart1.http` | ready-made requests for VS Code / Rider |

Configuration:

| Setting | Where | Effect |
| --- | --- | --- |
| `SeedPatients` | `appsettings.Development.json` | seed size (1,000,000 default); the unindexed scan gets faster as you lower it |
| Connection string | `appsettings.Development.json` | `labdb` on `localhost:5432` |

**Note:** Increase seed count value in `appsettings.Development.json`, and uncomment indexing code to see the time difference. Check `Data/AppDbContext.cs`.

## Results

Console snapshots live in [`snapshots/`](snapshots/), and are regenerated by running
[`scripts/capture-snapshots.sh`](scripts/capture-snapshots.sh) (starts Postgres, runs the
API, makes the requests, and writes the five files):

| File | What it proves |
| --- | --- |
| `phase3-curl.txt` | 5 requests to `/phase3/...`, all ~2.1 s, all HTTP 200 |
| `phase3-console.txt` | zero request log lines, 2 s gap invisible |
| `phase4-curl.txt` | replayed requests to `/phase4/...` after instrumentation |
| `phase4-console.txt` | correlated logs: DB lookup vs external call vs total |
| `phase-diff.txt` | before/after diff of the console output |

## Troubleshooting

- **pgAdmin.** The database sits on the compose network `observability-lab-net`. If your
  pgAdmin runs in its own container, link them once:
  `docker network connect <your-pgadmin-network> observability-lab-pg`. Then register a
  server: host `observability-lab-pg`, port `5432`, user `lab`, password `lab`,
  database `labdb`.
- **Transient HTTP 500.** Restarting Postgres under a running app can yield one 500 while
  stale pooled connections reconnect. The next request succeeds.
- **The external call's response is discarded.** That dead call is half the story: nobody
  can explain why it is there, and it is the slowest thing in the API.
- All examples use `http://localhost:5176`, the Development URL.

## License

[GPL-3.0](../../LICENSE)
