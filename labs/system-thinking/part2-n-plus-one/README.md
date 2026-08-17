<div align="center">

# System Thinking, Part 2

**The N+1 query problem, on purpose, then fixed**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-Npgsql-86BCDA)](https://learn.microsoft.com/ef/core/)
[![Docker](https://img.shields.io/badge/Docker-24db7ed?logo=docker&logoColor=white)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

An Orders API that looks harmless in code but issues one query per order, plus
one: 51 queries for a single request with the default seed of 50 orders. This lab
builds that N+1 problem on purpose, makes it visible with a per-request correlation
ID and EF Core's own SQL logging, then fixes it with eager loading. N+1 queries
become 1.

Each step is exposed as its own endpoint (`/phase1/orders` to `/phase4/orders`) so the
broken and the fixed versions can be run and compared in a single process.

> **Repo layout:** this lab lives at `labs/system-thinking/part2-n-plus-one` on the `main`
> branch. Run it from its own folder: `cd labs/system-thinking/part2-n-plus-one`, then the
> commands below. From the repo root you can also run
> `dotnet run --project labs/system-thinking/part2-n-plus-one`.

## Table of contents

- [How it works](#how-it-works)
- [The four phases](#the-four-phases)
  - [Phase 1: Scaffold](#phase-1-scaffold-done-right)
  - [Phase 2: Break it](#phase-2-break-it-on-purpose)
  - [Phase 3: Observe](#phase-3-observe)
  - [Phase 4: Fix it](#phase-4-fix-it-with-eager-loading)
- [Getting started](#getting-started)
- [Cleaning up everything](#cleaning-up-everything)
- [Project structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Analytical questions](#analytical-questions)
- [License](#license)

## How it works

The database holds N orders (50 by default, set via `Seed:OrderCount` in
`appsettings.json`), each with 2 items. Four endpoints return the same JSON,
but they load it very differently:

| Endpoint | What it does | Queries per request |
| --- | --- | --- |
| `GET /phase1/orders` | Loads orders and items in one query with `Include` | 1 |
| `GET /phase2/orders` | Loads all orders, then loops and queries items per order | N+1 |
| `GET /phase3/orders` | Same code as phase 2, used for observing the console | N+1 |
| `GET /phase4/orders` | The fix: `Include` + `AsNoTracking` | 1 |

Two pieces make the problem visible instead of guessed at:

1. A middleware gives every request a correlation ID and logs `request started`. Every
   log line from that request carries the ID.
2. EF Core's built-in command logging prints every SQL statement. The endpoint logs the
   total query count and duration with the same correlation ID.

So one curl to phase 3 produces a console trail you can count: 1 query for the orders,
then N more, one per order.

## The four phases

### Phase 1: Scaffold, done right

`GET /phase1/orders` loads orders and their items in one query. `Include` issues a JOIN,
so the whole graph costs a single round trip.

```bash
curl http://localhost:5151/phase1/orders
```

### Phase 2: Break it on purpose

`GET /phase2/orders` loads all orders with one query, then loops through each one and
runs a separate query for its items. That is the N+1 problem: N orders means N+1 round
trips.

```bash
curl http://localhost:5151/phase2/orders
```

### Phase 3: Observe

Phase 3 changes no code: it reuses the Phase 2 query path under its own route, so you can
hit it while watching the console. One request produces this trail (EF Core prints each
SQL command):

```text
info: Program[0]
      [8f3a1c2b] request started
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT o."Id", o."CustomerName" FROM "Orders" AS o
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[@__order_Id_0='1'], CommandType='Text', CommandTimeout='30']
      SELECT i."Id", i."ProductName", i."OrderId" FROM "OrderItems" AS i WHERE i."OrderId" = @__order_Id_0
      ...
info: SystemThinkingPart2.Controllers.OrdersController[0]
      [8f3a1c2b] total queries: 51  duration: 99ms
```

Count the `Executed DbCommand` lines: N+1 queries for one single request (51 with the
default 50-order seed).

```bash
curl http://localhost:5151/phase3/orders
```

### Phase 4: Fix it with eager loading

`GET /phase4/orders` loads the same graph with `.Include(o => o.Items)` and
`.AsNoTracking()`. The related rows come back in the same query as a JOIN instead of one
extra query per order.

```bash
curl http://localhost:5151/phase4/orders
```

Same JSON, one query:

```text
info: Program[0]
      [c72e9d41] request started
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT o."Id", o."CustomerName", i."Id", i."ProductName", i."OrderId"
      FROM "Orders" AS o
      LEFT JOIN "OrderItems" AS i ON o."Id" = i."OrderId"
      ORDER BY o."Id"
info: SystemThinkingPart2.Controllers.OrdersController[0]
      [c72e9d41] total queries: 1  duration: 3ms
```

## Getting started

Prerequisites: .NET SDK and Docker.

```bash
# 1. Start Postgres (port 5432, db diy_lab, user/pass lab/lab)
docker compose up -d --wait

# 2. Run the API. Applies the EF migration and seeds N orders (default 50)
#    with 2 items each
dotnet run

# 3. In a second terminal, hit the phases and watch the console
curl http://localhost:5151/phase3/orders
curl http://localhost:5151/phase4/orders
```

Seeding runs only when the `Orders` table is empty. The seed size is configurable:
change `Seed:OrderCount` in `appsettings.json` (default 50) to scale the lab's N.
The schema comes from an EF Core migration: `Program.cs` calls
`db.Database.Migrate()` at startup. To apply the migration manually instead:

```bash
dotnet ef database update
```

For a fully fresh replay: `docker compose down -v`, then the commands above again.
Changing `Seed:OrderCount` also needs a fresh volume (or an empty `Orders` table),
because seeding is skipped once rows exist.

## Cleaning up everything

```bash
# 1. Stop the API (Ctrl+C in its terminal)

# 2. Remove the database container and its data volume
docker compose down -v
```

The next `dotnet run` re-applies the migration and seeds again.

## Project structure

| File | Purpose |
| --- | --- |
| `Program.cs` | host setup, correlation ID middleware, migration + seeding, maps controllers |
| `Controllers/OrdersController.cs` | the four phase endpoints with comment explanations |
| `Services/OrderService.cs` | the three ways of loading orders: scaffold, naive (N+1), fixed |
| `Models/Order.cs` | `Order` and `OrderItem` entities |
| `Data/AppDbContext.cs` | EF Core context with an index on `OrderItems.OrderId` |
| `Migrations/` | EF Core schema migration (applied at startup via `Migrate()`) |
| `docker-compose.yml` | Postgres on port 5432, container `system-thinking-part2-pg` |
| `appsettings.json` | connection string |
| `SystemThinkingPart2.http` | ready-made requests for VS Code / Rider |

## Troubleshooting

- **Port conflicts.** Part 1 of this series runs Postgres on host port 5433, and this lab
  uses 5432. If 5432 is taken on your machine, change the port in `docker-compose.yml`
  and in both `appsettings.json` files.
- **First request is slower.** The first phase 3 request includes a cold connection and
  query plan cache; later requests settle to tens of ms.
- All examples use `http://localhost:5151`, the Development URL.

## Analytical questions

1. Phase 2 logs 51 queries for 50 orders. Where does the extra query come from? What
   does the count become with 1,000 orders, and how do you get the lab to seed that many?
2. Phase 2's per-order query filters `OrderItems.OrderId`. What would the total query time
   look like without the index on that column? Does the index make the N+1 problem go
   away, or just hide it?
3. Why does a JOIN fix the problem instead of merely reducing query count? What does the
   single JOIN query cost that 51 simple queries do not?
4. With the default seed, the database holds 50 orders with 2 items each. What happens
   to the JOIN's result set size if orders average 20 items? When does eager loading
   become the wrong fix?
5. What happens if you `Include` two separate collections (`Include(o => o.Items)` and
   `Include(o => o.Payments)`)? How many rows does the database return, and what is this
   effect called?
6. Phase 4 adds `AsNoTracking`. What does change tracking do to the naive phase 2 code
   that it does not do in phase 4, and how does it affect memory?
7. Phase 2 loops in C# and queries per order; phase 4 delegates the join to the database.
   Which one would win at 50 orders if the database were on another continent? Which one
   wins at 50,000 orders with the database next door?
8. How could a caching layer (e.g. `IQueryable` vs. `IEnumerable`, or an L2 cache) change
   the answer to question 7? Which kind of cache does EF Core's tracking provide by
   itself?
9. The lab makes the problem visible with a correlation ID and EF Core SQL logging. In a
   production app with hundreds of requests per second, what signal would you look for
   instead, and why is the console trail from this lab not practical there?
10. When is an N+1 query acceptable to leave in place? Give a concrete scenario and a
    concrete threshold for rejecting it.
11. What changes if the API returns only 10 orders per page instead of all 50? Which phase
    suffers most, and what does that say about N+1 fixes and premature optimization?
12. The naive and the fixed endpoints return the same JSON. If two endpoints return
    identical data but one costs 51 queries, what does that say about testing API
    correctness only, and what kind of test would catch this regression?

## License

[GPL-3.0](../../LICENSE)
