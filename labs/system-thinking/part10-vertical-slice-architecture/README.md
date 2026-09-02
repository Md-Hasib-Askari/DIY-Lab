<div align="center">

# System Thinking, Part 10

**Three operations, two organizations: layers versus vertical slices**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-Npgsql-86BCDA)](https://learn.microsoft.com/ef/core/)
[![MediatR](https://img.shields.io/badge/MediatR-12.5.0-6E4C9E)](https://github.com/jbogard/MediatR)
[![Docker](https://img.shields.io/badge/Docker-24db7ed?logo=docker&logoColor=white)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

Two API surfaces implement the same three operations on the same resource: create an
order, read an order, cancel an order. `POST /layered/orders`, `GET /layered/orders/{id}`
and `POST /layered/orders/{id}/cancel` route through a shared `OrdersController`, a shared
`OrderService`, and a shared `EfOrderRepository`, exactly the way `part9-layered-architecture`
split one operation across `Domain`/`Application`/`Infrastructure`/`Api`. `POST /slices/orders`,
`GET /slices/orders/{id}` and `POST /slices/orders/{id}/cancel` route through three
independent files under `Features/Orders/`, each holding its own request record, its own
response record, its own `Handler`, and its own persistence call.

Each slice's `Handler` implements MediatR's `IRequestHandler<TRequest, TResponse>`, so the
endpoint delegate does nothing but translate HTTP: it sends the request and turns the
result into a status code. That keeps the slice boundary a class boundary rather than a
lambda, which matters for question 6 at the bottom of this page.

One thing stays out of the slices on purpose. The rules an order must satisfy live on
`SliceOrder`, the entity the three slices share, exactly as they live on `LayeredOrder` for
the other path. The two paths are organized differently; neither gives up its domain model,
and that is the point. Keeping the entity equally strict on both sides is what makes the
comparison mean something, because then organization is the only variable left.

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
| `POST /slices/orders` | `Features/Orders/CreateOrder.cs`, alone: endpoint → `CreateOrder.Handler` | `SliceOrder`'s constructor, in the same feature folder |

Both paths accept `{ "customerId": "...", "total": 249.99 }` and return the same shape.
Neither returns its entity. Each path maps to a response record whose `Status` is already a
`string`, so the JSON reads `"Pending"` or `"Cancelled"` without either enum carrying a
`[JsonConverter]` attribute. The wire format is a decision of the response record, not of
the domain type.

Where the two paths differ is how many response records there are. The layered path has one
`OrderResponse` shared by all three actions. The slice path has three, one per file.

## The two paths

### Layered: one controller, one service, one repository

`OrdersController` (`Layered/Api/`) has three actions: `Create`, `Get`, `Cancel`. Each one
calls `OrderService` (`Layered/Application/`), which has three matching methods. Each of
those calls `IOrderRepository`, implemented by `EfOrderRepository`
(`Layered/Infrastructure/`), which also has three matching methods. Three operations, four
files, three methods repeated in each file.

The controller also owns the resource's one response shape, `OrderResponse`, which all
three actions map to before returning. `LayeredOrder` itself never reaches the client.

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
class holding four things: a request record, a response record, a nested `Handler`, and one
`MapEndpoint` method. `CreateOrder.Handler` constructs a `SliceOrder`, saves it through
`AppDbContext` directly, and maps it to `CreateOrder.Response`; the endpoint sends the
command and turns the result into a `201`. `GetOrder` and `CancelOrder` do the same for
their own operation and touch nothing else.

```csharp
public static class CreateOrder
{
    public record Command(Guid CustomerId, decimal Total) : IRequest<Response>;

    public record Response(Guid Id, Guid CustomerId, decimal Total, string Status);

    public class Handler(AppDbContext db) : IRequestHandler<Command, Response>
    {
        public async Task<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = new SliceOrder(command.CustomerId, command.Total);
            // db.SliceOrders.Add, db.SaveChangesAsync, map to Response
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) { /* MapPost, sender.Send */ }
}
```

Note what `Handler` does not do. It does not check that `Total` is positive, because
`SliceOrder`'s constructor will not build an invalid order in the first place, and
`CancelOrder.Handler` calls `order.Cancel()` rather than assigning `Status` itself. A fifth
slice that cancels an order for some other reason gets the same transition instead of a
copy of it. The slice owns when; the entity owns what.

The three `Handler` classes are never registered one by one. `Program.cs` scans the
assembly once and lists the three endpoints with one line each:

```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SliceOrder>());

CreateOrder.MapEndpoint(app);
GetOrder.MapEndpoint(app);
CancelOrder.MapEndpoint(app);
```

Compare that with the layered path's two explicit registrations, `IOrderRepository` and
`OrderService`, which grow with every new abstraction the layers introduce.

```bash
curl -X POST http://localhost:5160/slices/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 249.99}'
```

```json
{ "id": "bc398c04-...", "customerId": "11111111-...", "total": 249.99, "status": "Pending" }
```

Reading `CancelOrder.cs` requires nothing from `CreateOrder.cs` or `GetOrder.cs`. The three
files share `SliceOrder`, because all three read and write the same row and must obey the
same rules about it, and `IRequestHandler`, which each implements at a different pair of
type arguments. Neither is a class the three have to renegotiate with each other to change.
Vertical slice architecture does not remove sharing, it removes the four-folder detour a
single operation has to take to reach the code that handles it.

The three `Response` records are the opposite case. They are identical today, and
deliberately not shared. That duplication is what lets `GetOrder.Response` gain a field
tomorrow without `CreateOrder` or `CancelOrder` having an opinion about it. Sharing the
entity is cheap because its rules are genuinely common; sharing a response shape is
expensive because the three operations have no reason to answer with the same fields
forever. Question 3 below is about exactly this line.

Both paths throw `ArgumentException` on invalid input and share one middleware in
`Program.cs` that turns it into a 400, so neither the controller nor any slice carries a
`try`/`catch` of its own. The exception travels out of `Handler.Handle`, through
`ISender.Send`, and reaches that middleware unchanged.

```bash
curl -i -X POST http://localhost:5160/slices/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 0}'
```

```text
HTTP/1.1 400 Bad Request
Total must be positive (Parameter 'total')
```

Both paths produce that message from the same place in spirit: the entity's constructor,
which is the only way to build an order in either path.

## The payoff: adding a fourth operation

Add one operation to both paths: `ListOrdersByCustomer`, a `GET` that returns every order
for a given `customerId`. Do the layered path first.

1. `Layered/Application/IOrderRepository.cs`: add `Task<List<LayeredOrder>> ListByCustomerAsync(Guid customerId);`
2. `Layered/Infrastructure/EfOrderRepository.cs`: implement it against `db.LayeredOrders`.
3. `Layered/Application/OrderService.cs`: add a matching `ListByCustomerAsync` that forwards to the repository.
4. `Layered/Api/OrdersController.cs`: add a `[HttpGet("by-customer/{customerId:guid}")]` action that calls the service.

Now the slice path: create one new file.

5. `Features/Orders/ListOrdersByCustomer.cs`: a static class with a `Query` record, its own `Response` record, a `Handler` querying `db.SliceOrders` directly, and one `MapEndpoint`.
6. `Program.cs`: add `ListOrdersByCustomer.MapEndpoint(app);` next to the other three lines. The `Handler` needs no registration; the existing assembly scan finds it.

Note that step 4 reuses `OrderResponse` rather than adding a response type, which is the
layered path's advantage here: the fourth operation returns a shape three others already
defined. Step 5 writes a fourth `Response` record for a shape it could have reused.

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
 Features/Orders/ListOrdersByCustomer.cs | 35 +++++++++++++++++++++++++++++++++++
 Program.cs                              |  1 +
 2 files changed, 36 insertions(+)
```

The slice path touches more lines, all of them new and isolated in one file. The layered
path touches fewer lines, but four files that already existed, none of which were about
listing orders until this change reached into them. That second number is the one that
matters under review: four files changed means four places a reviewer, and a merge, has to
reconcile with whatever else is in flight on `Layered/` that week. One new file plus one
registration line means one file to review and near-zero surface for a merge conflict with
unrelated work on `CreateOrder.cs` or `CancelOrder.cs`.

The gap between 14 and 36 is worth naming honestly, because most of it is not the slice
organization at all. The same operation written as a bare endpoint delegate returning the
entity is 16 lines. The `Handler` and its request record account for roughly eleven more,
and the `Response` record and its mapping for most of the rest. Both are choices this lab
makes on the slice side and the layered path pays for once, up front, across files it
already had.

What those extra lines buy is a class that can be constructed and called without a host, a
response shape that can change without touching another operation, and a `Program.cs` that
still gains exactly one line, because the assembly scan finds the new `Handler` on its own.
The layered path's fourth operation adds no registration either, but only because
`OrderService` and `IOrderRepository` were registered before it existed and it fits inside
both.

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
| `Layered/Domain/LayeredOrder.cs` | the entity, its constructor validation, and its `Cancel()` transition for the layered path |
| `Layered/Application/IOrderRepository.cs`, `OrderService.cs` | the interface and the one service class covering all three operations |
| `Layered/Infrastructure/EfOrderRepository.cs` | the EF Core implementation of `IOrderRepository` |
| `Layered/Api/OrdersController.cs` | the controller with all three actions, plus `CreateOrderRequest` and the one `OrderResponse` they share |
| `Features/Orders/SliceOrder.cs` | the entity shared by all three slices, holding the rules every order obeys whichever slice is running |
| `Features/Orders/CreateOrder.cs`, `GetOrder.cs`, `CancelOrder.cs` | one file per operation, each with its own request record, its own `Response`, a `Handler : IRequestHandler<,>`, and `MapEndpoint` |
| `Infrastructure/AppDbContext.cs` | EF Core context, holds both `LayeredOrders` and `SliceOrders` |
| `Program.cs` | host setup, DI wiring for the layered path, the MediatR assembly scan, the shared 400 middleware, migration, and the three `MapEndpoint` calls |
| `SystemThinkingPart10.csproj` | package references, including MediatR 12.5.0 |
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
- **404 on a slice endpoint that should exist.** The endpoint is registered by
  `MapEndpoint`, but its `Handler` is found by the assembly scan in `Program.cs`. If a
  handler moves to another assembly, `AddMediatR` has to be told about that assembly too,
  or `ISender.Send` throws at request time rather than at startup.
- **MediatR version.** This lab pins 12.5.0, the last release under Apache-2.0. Version 13
  and later moved to a commercial license, so upgrading is a licensing decision, not just a
  version bump.
- All examples use `http://localhost:5160`, the Development URL.

## Analytical questions

1. Both paths call `SaveChangesAsync` through the same `AppDbContext`. What is actually
   different between them if both still depend on EF Core in the same way?
2. `EfOrderRepository` has three methods, one per layered operation. What happens to that
   file, and to `IOrderRepository`, the day a fifth operation is added?
3. `Features/Orders/SliceOrder.cs` is read and written by all three slices, while the three
   `Response` records are identical and deliberately kept separate. Justify both decisions
   at once: why is sharing the entity right when sharing the response shape is not, and
   what distinguishes either from sharing a service class like `OrderService`?
4. `CreateOrder.cs` and `GetOrder.cs` do not call each other and do not share a repository
   interface, but both handlers now receive an `ISender`-shaped world in which either could
   send the other's request. If `CreateOrder` needed to check whether a customer already
   has a pending order, the exact query `GetOrder` already runs, is sending `GetOrder.Query`
   from inside `CreateOrder.Handler` better or worse than copying the two-line query? What
   does each choice do to the claim that the two files can change independently?
5. The `git diff --stat` exercise measured file count and line count for one new operation.
   Which number predicts merge-conflict risk better when two engineers add different
   operations to the same resource at the same time, and why?
6. `OrdersController` depends on `OrderService`, which depends on `IOrderRepository`, an
   interface a test can substitute. Each slice `Handler` depends on `AppDbContext`, a class
   it cannot. Both are now plain classes a test can construct directly, so the endpoint is
   no longer the obstacle it was when the logic lived in a lambda. What is still harder to
   test on the slice side, and does answering it with an in-memory or containerized
   database cost less than the interface the layered path maintains for the same purpose?
7. Say `GetOrder` alone must return `DaysSinceCreated`. There are now three places it could
   live: on `SliceOrder`, on `GetOrder.Response`, or computed in `GetOrder.Handler` during
   mapping. Which is right, and does the answer change if the value is derived purely from
   fields the entity already has? What changes if `CreateOrder` later needs the same value?
8. This lab keeps both paths in one project so they can be compared side by side. What
   would break about that comparison, or about the lab itself, if `Layered/` and
   `Features/` needed to evolve independently in a real codebase?
9. The three `Handler` classes reach DI through one assembly scan, while `OrderService` and
   `IOrderRepository` are registered by name. What does each style tell a reader who opens
   `Program.cs` looking for where a request is served, and which mistake surfaces sooner: a
   handler the scan missed, or a service registration someone forgot to add?
10. Neither `OrderStatus` enum carries a `[JsonConverter]` attribute any more, because the
    response records expose `Status` as a `string` they map themselves. What did the domain
    types gain by giving that up, and what did the two paths lose by having to write the
    mapping by hand in four places instead of declaring it once on the enum?

## License

[GPL-3.0](../../LICENSE)
