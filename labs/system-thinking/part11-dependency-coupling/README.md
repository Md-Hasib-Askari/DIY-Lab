<div align="center">

# System Thinking, Part 11

**Two folders, one repository class: who is allowed to build it**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

Two API surfaces build the same three services on the same entity: `OrderService`,
`InvoiceService` and `NotificationService`, each with one `Run(Order)` method that saves the
order. `POST /coupled/orders` routes through `Coupled/`, where all three services declare
`private readonly SqlOrderRepository _repo = new();` and construct their own repository.
`POST /decoupled/orders` routes through `Decoupled/`, where all three services take an
`IOrderRepository` constructor parameter instead, and `Program.cs` is the only place that
names the concrete `SqlOrderRepository` type.

No database and no Docker are required. Both repositories persist to an in-process list, so
the lab isolates one variable: who is allowed to write `new SqlOrderRepository()`.

## Table of contents

- [How it works](#how-it-works)
- [The two paths](#the-two-paths)
- [Reproducing the four steps](#reproducing-the-four-steps)
- [Getting started](#getting-started)
- [Project structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Analytical questions](#analytical-questions)
- [License](#license)

## How it works

| Endpoint | Where the request is handled | Who constructs the repository |
| --- | --- | --- |
| `POST /coupled/orders` | `Coupled/Services/OrderService.cs`, `InvoiceService.cs`, `NotificationService.cs` | Each service, with `new SqlOrderRepository()` |
| `POST /decoupled/orders` | `Decoupled/Services/OrderService.cs`, `InvoiceService.cs`, `NotificationService.cs` | `Program.cs`, once, through `AddScoped<IOrderRepository, SqlOrderRepository>()` |

Both paths accept `{ "customerId": "...", "total": 249.99 }` and return the same shape. A
`GET` on either path returns every order saved on that path so far.

## The two paths

### Coupled: three classes, three constructors

```csharp
public class OrderService
{
    private readonly SqlOrderRepository _repo = new();

    public void Run(Order order) => _repo.Save(order);
}
```

`InvoiceService` and `NotificationService` repeat the same line. Nothing in `Program.cs`
registers `SqlOrderRepository`, because nothing needs to: each service is self-sufficient,
and that is exactly the property that makes the class expensive to change.

### Decoupled: one interface, one registration

```csharp
public class OrderService(IOrderRepository repo)
{
    public void Run(Order order) => repo.Save(order);
}
```

`Program.cs` decides which concrete type fills `IOrderRepository` for all three services at
once:

```csharp
builder.Services.AddScoped<DecoupledContracts.IOrderRepository, DecoupledRepository.SqlOrderRepository>();
```

Neither `OrderService`, `InvoiceService` nor `NotificationService` names `SqlOrderRepository`
anywhere in the `Decoupled/` folder.

## Reproducing the four steps

These map directly onto Part 11's "Try It Yourself" slides.

**Step 1, create the problem.** Already shipped as `Coupled/Services/`. Read `OrderService.cs`,
`InvoiceService.cs` and `NotificationService.cs`: three files, three separate
instantiations of the same concrete class.

**Step 2, observe.** Add a required constructor parameter to
`Coupled/Repository/SqlOrderRepository.cs`:

```csharp
public class SqlOrderRepository
{
    private readonly ILogger<SqlOrderRepository> _logger;

    public SqlOrderRepository(ILogger<SqlOrderRepository> logger)
    {
        _logger = logger;
    }

    // Save and All stay the same.
}
```

Then run:

```bash
dotnet build
```

Expect three errors, one per file that calls `new SqlOrderRepository()` directly:

```text
Coupled/Services/OrderService.cs(8,49): error CS7036: There is no argument given that
  corresponds to the required parameter 'logger' of
  'SqlOrderRepository.SqlOrderRepository(ILogger<SqlOrderRepository>)'
Coupled/Services/InvoiceService.cs(8,49): error CS7036: ...
Coupled/Services/NotificationService.cs(8,49): error CS7036: ...
```

Revert the change (`git checkout -- Coupled/Repository/SqlOrderRepository.cs`, or undo it
by hand) before continuing.

**Step 3, fix it with DI.** Already shipped as `Decoupled/`. Make the identical constructor
change to `Decoupled/Repository/SqlOrderRepository.cs` this time, then run `dotnet build`
again. Zero errors. `OrderService`, `InvoiceService` and `NotificationService` never
construct `SqlOrderRepository`, so a new required parameter on it is invisible to all
three.

**Step 4, verify.** Change one line in `Program.cs`:

```csharp
builder.Services.AddScoped<DecoupledContracts.IOrderRepository, DecoupledRepository.InMemoryOrderRepository>();
```

Run `dotnet build`. It still succeeds, and none of the three service files changed. Restart
the API, post to `/decoupled/orders`, then `GET /decoupled/orders`. The list now starts empty
on every restart, because `InMemoryOrderRepository` holds its list on the instance rather
than in the static field `Decoupled/Repository/SqlOrderRepository.cs` uses to simulate a
shared database.

## Getting started

Prerequisites: the .NET SDK, nothing else.

```bash
# 1. Run the API. No database, no Docker, no external service.
dotnet run

# 2. In a second terminal, hit both paths
curl -X POST http://localhost:5171/coupled/orders -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 249.99}'
curl -X POST http://localhost:5171/decoupled/orders -H "Content-Type: application/json" \
  -d '{"customerId": "11111111-1111-1111-1111-111111111111", "total": 249.99}'

# 3. Confirm both paths persisted what they saved
curl http://localhost:5171/coupled/orders
curl http://localhost:5171/decoupled/orders
```

## Project structure

| File | Purpose |
| --- | --- |
| `Order.cs` | the entity both paths share: `Id`, `CustomerId`, `Total` |
| `Contracts/CreateOrderRequest.cs` | the shared request DTO both endpoints bind to |
| `Coupled/Repository/SqlOrderRepository.cs` | the repository, constructed directly by every coupled service |
| `Coupled/Services/OrderService.cs`, `InvoiceService.cs`, `NotificationService.cs` | each declares its own `SqlOrderRepository` with `new` |
| `Decoupled/Contracts/IOrderRepository.cs` | the interface every decoupled service depends on instead |
| `Decoupled/Repository/SqlOrderRepository.cs` | the production implementation, registered once in `Program.cs` |
| `Decoupled/Repository/InMemoryOrderRepository.cs` | the test-friendly implementation, one line away in `Program.cs` |
| `Decoupled/Services/OrderService.cs`, `InvoiceService.cs`, `NotificationService.cs` | each takes `IOrderRepository` as a constructor parameter |
| `Program.cs` | host setup, the coupled path's plain registrations, the decoupled path's one interface registration, and all four endpoints |
| `SystemThinkingPart11.csproj` | package references |
| `SystemThinkingPart11.http` | ready-made requests for VS Code / Rider |

## Troubleshooting

- **Port conflicts.** This lab listens on 5171 (HTTP) and 7171 (HTTPS). Change either in
  `Properties/launchSettings.json` if the port is taken.
- **Step 2's errors do not show up.** Confirm the edit landed on
  `Coupled/Repository/SqlOrderRepository.cs`, not `Decoupled/Repository/SqlOrderRepository.cs`.
  Only the coupled file should break the build.
- **`GET /decoupled/orders` returns fewer orders than expected after Step 4.** That is the
  point: `InMemoryOrderRepository` does not share state across restarts. Compare it against
  `GET /coupled/orders`, whose static field survives restarts within the same process for as
  long as the API keeps running.
- All examples use `http://localhost:5171`, the Development URL.

## Analytical questions

1. `Coupled/Services/OrderService.cs` and `Decoupled/Services/OrderService.cs` both compile without error
   today. What does "compiles" fail to tell a reviewer about how expensive a future change to
   `SqlOrderRepository` will be?
2. Step 2 produces three identical `CS7036` errors, one per coupled service. If a fourth
   service were added to `Coupled/` tomorrow, what would the error count become, and what
   does that number represent?
3. `Program.cs` registers `Coupled.OrderService`, `InvoiceService` and `NotificationService`
   with `AddTransient`, even though none of them take a constructor parameter. What would
   change about the coupled path if that registration were removed entirely and the endpoint
   constructed them with `new` too?
4. Both `Coupled/Repository/SqlOrderRepository.cs` and `Decoupled/Repository/SqlOrderRepository.cs` use a `static`
   field to simulate a shared database. What real behavior does `static` stand in for here,
   and what would break about the simulation if it were an instance field instead?
5. `Decoupled/Repository/InMemoryOrderRepository.cs` implements `IOrderRepository` exactly like
   `SqlOrderRepository.cs` does, but holds its list on the instance. Which of the two facts,
   the shared interface or the different storage lifetime, is the one that makes it safe to
   swap in for tests?
6. `Program.cs` is the only file that names `DecoupledRepository.SqlOrderRepository`. What category of
   change to that class would still require editing a service file despite the interface
   boundary, and does that category exist on the coupled side too?
7. This lab keeps both paths in one project so Step 2 and Step 3 can be run side by side.
   What would have to change about the comparison if `Coupled/` and `Decoupled/` needed to
   ship as separate deployable services instead?
8. `IOrderRepository` has two methods, `Save` and `All`. If a ninth method were added to
   support a new feature, every implementation would need it whether that feature applies to
   it or not. Is that a cost of this interface specifically, or of interfaces with more than
   a handful of methods in general?

## License

[GPL-3.0](../../LICENSE)
