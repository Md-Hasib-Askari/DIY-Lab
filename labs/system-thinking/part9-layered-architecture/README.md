<div align="center">

# System Thinking, Part 9

**One rule, two controllers: the fat controller versus the layered split**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-Npgsql-86BCDA)](https://learn.microsoft.com/ef/core/)
[![Docker](https://img.shields.io/badge/Docker-24db7ed?logo=docker&logoColor=white)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

Two endpoints accept the same request and apply the same rule: an order over 10,000
needs approval. `POST /legacy/orders` decides that rule inside the controller, reading
and writing `AppDbContext` directly. `POST /orders` decides the same rule inside a
plain `Order` class that never sees ASP.NET Core, EF Core, or HTTP. Both run in the
same process against the same database, so the two can be compared side by side.

The payoff sits in `Tests/OrderTests.cs`. Testing the legacy rule needs a running
controller and a database. Testing the layered rule needs one line: `new Order(...)`.

## Table of contents

- [How it works](#how-it-works)
- [The two paths](#the-two-paths)
  - [Legacy: the fat controller](#legacy-the-fat-controller)
  - [Layered: Domain, Application, Infrastructure, Api](#layered-domain-application-infrastructure-api)
  - [The payoff: a zero-setup unit test](#the-payoff-a-zero-setup-unit-test)
- [Getting started](#getting-started)
- [Cleaning up everything](#cleaning-up-everything)
- [Project structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Analytical questions](#analytical-questions)
- [License](#license)

## How it works

One database holds two tables, `LegacyOrders` and `Orders`, so both paths persist
through the same `AppDbContext` and can run at the same time.

| Endpoint | Where the rule lives | What decides `OrderStatus` |
| --- | --- | --- |
| `POST /legacy/orders` | Inside `LegacyOrdersController` | The controller compares `TotalPrice` to a constant, then sets the field itself |
| `POST /orders` | Inside `Domain/Order.cs` | The `Order` constructor decides `Status` when the object is built, before anything else runs |

Both endpoints accept the same body, `{ productId, quantity, unitPrice }`, and return
the same shape. `status: 0` means `Approved`, `status: 1` means `NeedsApproval`.

## The two paths

### Legacy: the fat controller

`LegacyOrdersController` validates the request, computes `TotalPrice`, decides
`Status` against a threshold constant, and calls `SaveChangesAsync`, all inside one
action method. `LegacyOrder` itself is anemic: a bag of public setters with no rule of
its own. This is the version introductory tutorials teach.

```bash
curl -X POST http://localhost:5159/legacy/orders \
  -H "Content-Type: application/json" \
  -d '{"productId": 1, "quantity": 100, "unitPrice": 150}'
```

```json
{ "id": 1, "productId": 1, "quantity": 100, "unitPrice": 150, "totalPrice": 15000, "status": 1 }
```

### Layered: Domain, Application, Infrastructure, Api

The same rule, split across four layers that only point inward:

```
Api            OrdersController: receives the request, calls Application, shapes the response
Application    CreateOrderService: one use case, orchestrates Domain and IOrderRepository
Domain         Order: the approval rule, framework-free, decided in the constructor
Infrastructure EfOrderRepository: implements IOrderRepository with EF Core
```

`CreateOrderService` depends on `IOrderRepository`, an interface defined in
`Application/`. It has no reference to EF Core. `EfOrderRepository`, in
`Infrastructure/`, is the only class in the lab that mentions `AppDbContext` outside
the legacy path. Swapping EF Core for another store means changing that one class.

```bash
curl -X POST http://localhost:5159/orders \
  -H "Content-Type: application/json" \
  -d '{"productId": 1, "quantity": 100, "unitPrice": 150}'
```

```json
{ "id": 1, "productId": 1, "quantity": 100, "unitPrice": 150, "totalPrice": 15000, "status": 1 }
```

A quantity of zero or less breaks the domain's own guard clause, which throws
`ArgumentException`. A single middleware in `Program.cs` turns that into a 400 for
both endpoints, so neither controller carries a `try`/`catch`.

```bash
curl -i -X POST http://localhost:5159/orders \
  -H "Content-Type: application/json" \
  -d '{"productId": 1, "quantity": 0, "unitPrice": 150}'
```

```text
HTTP/1.1 400 Bad Request
Quantity must be positive (Parameter 'quantity')
```

### The payoff: a zero-setup unit test

`Order` has no constructor parameter that is not a plain value, no base class, and no
attribute. `Tests/OrderTests.cs` builds one and asserts on it directly:

```csharp
[Fact]
public void Order_over_10000_needs_approval()
{
    var order = new Order(productId: 1, quantity: 100, unitPrice: 150m);

    Assert.Equal(OrderStatus.NeedsApproval, order.Status);
}
```

No `AppDbContext`, no in-memory provider, no HTTP client, no running process. The
same assertion against the legacy path would need an EF Core test double and an
instantiated controller, because the rule is fused to `SaveChangesAsync` inside the
action method.

## Getting started

Prerequisites: .NET SDK and Docker.

```bash
# 1. Start Postgres (port 5432, db diy_lab, user/pass lab/lab)
docker compose up -d --wait

# 2. Run the API. Applies the EF migration on startup.
dotnet run

# 3. In a second terminal, hit both paths
curl -X POST http://localhost:5159/legacy/orders -H "Content-Type: application/json" \
  -d '{"productId": 1, "quantity": 100, "unitPrice": 150}'
curl -X POST http://localhost:5159/orders -H "Content-Type: application/json" \
  -d '{"productId": 1, "quantity": 100, "unitPrice": 150}'
```

The schema comes from an EF Core migration: `Program.cs` calls `db.Database.Migrate()`
at startup. To apply the migration manually instead:

```bash
dotnet ef database update
```

To run the zero-setup unit test on its own, no Docker container required:

```bash
cd Tests
dotnet test
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
| `Domain/Order.cs` | the approval rule, framework-free, decided in the constructor |
| `Application/CreateOrderService.cs` | the one use case, orchestrates Domain and the repository interface |
| `Application/IOrderRepository.cs` | the interface Application depends on, with no knowledge of EF Core |
| `Infrastructure/AppDbContext.cs` | EF Core context, holds both `Orders` and `LegacyOrders` |
| `Infrastructure/EfOrderRepository.cs` | the EF Core implementation of `IOrderRepository` |
| `Api/OrdersController.cs` | the slim controller for the layered path |
| `Legacy/LegacyOrder.cs`, `Legacy/LegacyOrdersController.cs` | the fat-controller path, kept side by side for comparison |
| `Contracts/CreateOrderRequest.cs` | the one request shape both endpoints accept |
| `Program.cs` | host setup, DI wiring, the `ArgumentException`-to-400 middleware, migration |
| `Migrations/` | EF Core schema migration (applied at startup via `Migrate()`) |
| `Tests/OrderTests.cs` | the zero-setup unit tests against `Domain.Order` |
| `docker-compose.yml` | Postgres on port 5432, container `system-thinking-part9-pg` |
| `appsettings.json` | connection string |
| `SystemThinkingPart9.http` | ready-made requests for VS Code / Rider |

## Troubleshooting

- **Port conflicts.** This lab uses host port 5432 for Postgres and 5159 for the API.
  If either is taken, change it in `docker-compose.yml` or `Properties/launchSettings.json`.
- **`dotnet test` fails to find the main project.** Run it from inside `Tests/`, or from
  the lab root with `dotnet test Tests/SystemThinkingPart9.Tests.csproj`. The parent
  project excludes `Tests/**` from its own compilation, so the two projects only connect
  through the `ProjectReference` in `Tests/SystemThinkingPart9.Tests.csproj`.
- All examples use `http://localhost:5159`, the Development URL.

## Analytical questions

1. Both endpoints call `SaveChangesAsync` through the same `AppDbContext`. What makes
   the layered path testable without a database while the legacy path is not, if both
   still depend on EF Core somewhere?
2. `IOrderRepository` has one method, `AddAsync`. What would happen to `Domain/Order.cs`
   if that interface were deleted and `CreateOrderService` called `AppDbContext` directly?
3. `Order`'s only public constructor takes three values and returns a fully valid object.
   What kind of invalid `Order` can `LegacyOrder` represent that `Order` cannot?
4. The private parameterless constructor on `Order` exists only for EF Core. What would
   break if it were removed, and why does EF Core not need the three-argument constructor
   to materialize a row?
5. `CreateOrderService` takes an `IOrderRepository` through its constructor rather than
   creating an `EfOrderRepository` itself. What test becomes possible because of that
   choice, and what would `Tests/` need to add to write it?
6. Both paths run against the same `AppDbContext`. If a second aggregate were added,
   say `Payment`, where would the rule "an order needs a payment before it ships" belong,
   and why does that question have a clear answer in the layered path but not in the
   legacy one?
7. The approval threshold, 10,000, is a private constant inside `Order`. What would
   change about testability, deployment, and risk if it moved to `appsettings.json`
   instead?
8. `LegacyOrdersController` and `OrdersController` both call the same middleware for
   validation failures, but only `OrdersController`'s failure originates from a domain
   guard clause. What happens if the legacy path is given a bad quantity? Trace where the
   error would surface.
9. This lab defines four layers for one rule. At what point does that structure cost more
   than a single fat controller, and what would the next feature need to look like before
   the split earns itself back?

## License

[GPL-3.0](../../LICENSE)
