<div align="center">

# System Thinking, Part 10

**Three operations, two organizations: layers versus vertical slices**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-Npgsql-86BCDA)](https://learn.microsoft.com/ef/core/)
[![Docker](https://img.shields.io/badge/Docker-24db7ed?logo=docker&logoColor=white)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

Two API surfaces implement the same three operations on the same resource: create an
order, read an order, cancel an order. `POST /layered/orders`, `GET /layered/orders/{id}`
and `POST /layered/orders/{id}/cancel` route through a shared `OrdersController`, a shared
`OrderService`, and a shared `EfOrderRepository`, exactly the way `part9-layered-architecture`
split one operation across `Domain`/`Application`/`Infrastructure`/`Api`. `POST /slices/orders`,
`GET /slices/orders/{id}` and `POST /slices/orders/{id}/cancel` route through three
independent files under `Features/Orders/`, each holding its own request shape, validation,
persistence call, and response mapping.

The payoff is not in either path's code, it is in what happens to the two folders when a
fourth operation is added. See [The payoff: adding a fourth operation](#the-payoff-adding-a-fourth-operation).

## Table of contents

- [How it works](#how-it-works)
- [The two paths](#the-two-paths)
  - [Layered: one controller, one service, one repository](#layered-one-controller-one-service-one-repository)
  - [Slices: one file per operation](#slices-one-file-per-operation)
- [The payoff: adding a fourth operation](#the-payoff-adding-a-fourth-operation)
- [Getting started](#getting-started)
- [Cleaning up everything](#cleaning-up-everything)
- [Project structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Analytical questions](#analytical-questions)
- [License](#license)

## How it works

One database holds two tables, `LayeredOrders` and `SliceOrders`, so both paths persist
through the same `AppDbContext` and can run at the same time.

| Endpoint | Where the request is handled | What decides `Status` |
| --- | --- | --- |
| `POST /layered/orders` | `OrdersController` → `OrderService` → `EfOrderRepository` | `LayeredOrder`'s constructor, in `Layered/Domain/` |
| `POST /slices/orders` | `Features/Orders/CreateOrder.cs`, alone | Inline inside the same endpoint delegate |

Both paths accept `{ "customerId": "...", "total": 249.99 }` and return the same shape.
`Status` carries a `[JsonConverter(typeof(JsonStringEnumConverter))]` attribute on both
enums, so it serializes as `"Pending"` or `"Cancelled"` rather than `0`/`1`.

## The two paths

### Layered: one controller, one service, one repository

`OrdersController` (`Layered/Api/`) has three actions: `Create`, `Get`, `Cancel`. Each one
calls `OrderService` (`Layered/Application/`), which has three matching methods. Each of
those calls `IOrderRepository`, implemented by `EfOrderRepository`
(`Layered/Infrastructure/`), which also has three matching methods. Three operations, four
files, three methods repeated in each file.

```bash
curl -X POST http://localhost:5160/layered/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 249.99}'
```

```json
{ "id": "279f1810-...", "customerId": "11111111-...", "total": 249.99, "status": "Pending" }
```

Reading `CancelAsync` in `OrderService` means understanding what `GetAsync` and
`CreateAsync` in the same class already assume about the shape of `LayeredOrder`, because
all three live in one file and share one constructor injection of `IOrderRepository`.

### Slices: one file per operation

`Features/Orders/CreateOrder.cs`, `GetOrder.cs` and `CancelOrder.cs` each define a static
class with one `MapEndpoint` method. `CreateOrder` validates the request, builds a
`SliceOrder`, calls `AppDbContext` directly, and shapes the response, all inside one
lambda. `GetOrder` and `CancelOrder` do the same for their own operation and touch nothing
else. `Program.cs` lists all three with one line each:

```csharp
CreateOrder.MapEndpoint(app);
GetOrder.MapEndpoint(app);
CancelOrder.MapEndpoint(app);
```

```bash
curl -X POST http://localhost:5160/slices/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 249.99}'
```

```json
{ "id": "bc398c04-...", "customerId": "11111111-...", "total": 249.99, "status": "Pending" }
```

Reading `CancelOrder.cs` requires nothing from `CreateOrder.cs` or `GetOrder.cs`. The three
files share one thing: `SliceOrder`, the entity, because all three still read and write the
same row. Vertical slice architecture does not remove sharing, it removes the four-folder
detour a single operation has to take to reach the code that handles it.

Both paths throw `ArgumentException` on invalid input and share one middleware in
`Program.cs` that turns it into a 400, so neither the controller nor any slice carries a
`try`/`catch` of its own.

```bash
curl -i -X POST http://localhost:5160/slices/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 0}'
```

```text
HTTP/1.1 400 Bad Request
Total must be positive (Parameter 'total')
```

## The payoff: adding a fourth operation

Add one operation to both paths: `ListOrdersByCustomer`, a `GET` that returns every order
for a given `customerId`. Do the layered path first.

1. `Layered/Application/IOrderRepository.cs`: add `Task<List<LayeredOrder>> ListByCustomerAsync(Guid customerId);`
2. `Layered/Infrastructure/EfOrderRepository.cs`: implement it against `db.LayeredOrders`.
3. `Layered/Application/OrderService.cs`: add a matching `ListByCustomerAsync` that forwards to the repository.
4. `Layered/Api/OrdersController.cs`: add a `[HttpGet("by-customer/{customerId:guid}")]` action that calls the service.

Now the slice path: create one new file.

5. `Features/Orders/ListOrdersByCustomer.cs`: a static class with one `MapEndpoint`, querying `db.SliceOrders` directly.
6. `Program.cs`: add `ListOrdersByCustomer.MapEndpoint(app);` next to the other three lines.

Stage both sets of changes separately and diff them:

```bash
git add Layered
git diff --stat --cached -- Layered

git add Features Program.cs
git diff --stat --cached -- Features Program.cs
```

Building this lab and running that exact sequence produces:

```text
 Layered/Api/OrdersController.cs             | 7 +++++++
 Layered/Application/IOrderRepository.cs     | 1 +
 Layered/Application/OrderService.cs         | 3 +++
 Layered/Infrastructure/EfOrderRepository.cs | 3 +++
 4 files changed, 14 insertions(+)
```

```text
 Features/Orders/ListOrdersByCustomer.cs | 16 ++++++++++++++++
 Program.cs                              |  1 +
 2 files changed, 17 insertions(+)
```

The slice path touches more lines, all of them new and isolated in one file. The layered
path touches fewer lines, but four files that already existed, none of which were about
listing orders until this change reached into them. That second number is the one that
matters under review: four files changed means four places a reviewer, and a merge, has to
reconcile with whatever else is in flight on `Layered/` that week. One new file plus one
registration line means one file to review and near-zero surface for a merge conflict with
unrelated work on `CreateOrder.cs` or `CancelOrder.cs`.

This is not evidence that slices are strictly better. A fifth operation that genuinely needs
`GetAsync`'s query, not a copy of it, is a real cost on the slice side that this exercise does
not show. Weigh the file count against that risk before generalizing from three operations to
thirty.

## Getting started

Prerequisites: .NET SDK and Docker.

```bash
# 1. Start Postgres (port 5432, db diy_lab, user/pass lab/lab)
docker compose up -d --wait

# 2. Run the API. Applies the EF migration on startup.
dotnet run

# 3. In a second terminal, hit both paths
curl -X POST http://localhost:5160/layered/orders -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 249.99}'
curl -X POST http://localhost:5160/slices/orders -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 249.99}'
```

The schema comes from an EF Core migration: `Program.cs` calls `db.Database.Migrate()`
at startup. To apply the migration manually instead:

```bash
dotnet ef database update
```

## Cleaning up everything

```bash
# 1. Stop the API (Ctrl+C in its terminal)

# 2. Remove the database container and its data volume
docker compose down -v
```

The next `dotnet run` re-applies the migration against a fresh database.

## Project structure

| File | Purpose |
| --- | --- |
| `Layered/Domain/LayeredOrder.cs` | the entity and its `Cancel()` transition for the layered path |
| `Layered/Application/IOrderRepository.cs`, `OrderService.cs` | the interface and the one service class covering all three operations |
| `Layered/Infrastructure/EfOrderRepository.cs` | the EF Core implementation of `IOrderRepository` |
| `Layered/Api/OrdersController.cs` | the controller with all three actions |
| `Features/Orders/SliceOrder.cs` | the entity shared by all three slices in this feature folder |
| `Features/Orders/CreateOrder.cs`, `GetOrder.cs`, `CancelOrder.cs` | one file per operation, each with its own `MapEndpoint` |
| `Infrastructure/AppDbContext.cs` | EF Core context, holds both `LayeredOrders` and `SliceOrders` |
| `Program.cs` | host setup, DI wiring for the layered path, the shared 400 middleware, migration, and the three `MapEndpoint` calls |
| `Migrations/` | EF Core schema migration (applied at startup via `Migrate()`) |
| `docker-compose.yml` | Postgres on port 5432, container `system-thinking-part10-pg` |
| `appsettings.json` | connection string |
| `SystemThinkingPart10.http` | ready-made requests for VS Code / Rider |

## Troubleshooting

- **Port conflicts.** This lab uses host port 5432 for Postgres and 5160 for the API.
  If either is taken, change it in `docker-compose.yml` or `Properties/launchSettings.json`.
- **`git diff --stat` shows nothing.** The commands in
  [The payoff](#the-payoff-adding-a-fourth-operation) assume the lab folder is its own git
  checkout or that changes are staged with `git add` before diffing against the previous
  commit. Run `git init` inside the lab folder first if it is not already tracked.
- All examples use `http://localhost:5160`, the Development URL.

## Analytical questions

1. Both paths call `SaveChangesAsync` through the same `AppDbContext`. What is actually
   different between them if both still depend on EF Core in the same way?
2. `EfOrderRepository` has three methods, one per layered operation. What happens to that
   file, and to `IOrderRepository`, the day a fifth operation is added?
3. `Features/Orders/SliceOrder.cs` is read and written by all three slices. Does that file
   violate the point of organizing by feature? What is the difference between sharing an
   entity and sharing a service class like `OrderService`?
4. `CreateOrder.cs` and `GetOrder.cs` do not call each other and do not share a repository
   interface. What would have to change in this lab if `CreateOrder` needed to check
   whether a customer already has a pending order, the exact query `GetOrder` already runs?
5. The `git diff --stat` exercise measured file count and line count for one new operation.
   Which number predicts merge-conflict risk better when two engineers add different
   operations to the same resource at the same time, and why?
6. `OrdersController` depends on `OrderService`, which depends on `IOrderRepository`. The
   slice endpoints depend on `AppDbContext` directly. What test would be harder to write
   against the slice path than against the layered path, and what would make it easier?
7. If `SliceOrder` needed a computed property, say a `DaysSinceCreated` field returned only
   by `GetOrder`, where should that computation live: on `SliceOrder` itself, or inside
   `GetOrder.cs`? What changes about the answer if `CreateOrder` needed the same value?
8. This lab keeps both paths in one project so they can be compared side by side. What
   would break about that comparison, or about the lab itself, if `Layered/` and
   `Features/` needed to evolve independently in a real codebase?

## License

[GPL-3.0](../../LICENSE)
