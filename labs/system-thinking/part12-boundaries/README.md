<div align="center">

# System Thinking, Part 12

**One shared class, three modules, and the boundary nobody drew**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

Two API surfaces build the same three interactions, shipping an order, charging a
payment, sending a notification, on the same customer. `POST /coupled/*` routes through
`Coupled/`, where `OrderService`, `PaymentService` and `NotificationService` all take
`Shared.Models.Customer` as a parameter and read `customer.Address` straight off it.
`POST /decoupled/*` routes through `Decoupled/`, where those same three concerns never
see `Shared.Models.Customer` at all: `Decoupled.CustomerModule.CustomerDirectory` owns
the field and announces `CustomerAddressChanged`, and `Order`/`Payment` each keep a
small, module-owned view built from that event.

No database and no Docker are required. Both paths persist to an in-process
dictionary, so the lab isolates one variable: whether a module reaches into a
shared class, or asks for the small view it actually needs.

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

| Endpoint | Where the request is handled | What it reads |
| --- | --- | --- |
| `POST /coupled/orders/{id}/ship` | `Coupled/Services/OrderService.cs` | `Shared.Models.Customer.Address` directly |
| `POST /coupled/payments/{id}/charge` | `Coupled/Services/PaymentService.cs` | `Shared.Models.Customer.Address` directly |
| `POST /coupled/notifications/{id}` | `Coupled/Services/NotificationService.cs` | `Shared.Models.Customer.Address` directly |
| `PUT /decoupled/customers/{id}/address` | `Decoupled/CustomerModule/CustomerDirectory.cs` | its own `CustomerRecord`, then publishes `CustomerAddressChanged` |
| `GET /decoupled/orders/{id}` | `Decoupled/Services/OrderService.cs` | its own `OrderCustomer` view, updated only from the event |
| `GET /decoupled/payments/{id}` | `Decoupled/Services/PaymentService.cs` | its own `PaymentCustomer` view, updated only from the event |

## The two paths

### Coupled: one class, three readers

```csharp
public class OrderService
{
    public string Ship(Customer customer)
    {
        string shipTo = customer.Address;
        return $"Shipping order to {shipTo}";
    }
}
```

`PaymentService` and `NotificationService` repeat the same shape: take the whole
`Customer`, read `Address` off it, assume it is a `string`. Nothing here names a
contract between Order and Customer, or Payment and Customer. There is one class,
and three modules that happen to agree on its shape today.

### Decoupled: one event, two small views

```csharp
public record OrderCustomer(int Id, string ShipTo);

public class OrderService
{
    private readonly Dictionary<int, OrderCustomer> _views = new();

    public void Handle(CustomerAddressChanged e)
    {
        _views[e.CustomerId] = _views[e.CustomerId] with { ShipTo = e.NewAddress };
        Console.WriteLine("[Order]    ShipTo cache updated");
    }
}
```

`OrderService` and `PaymentService` in `Decoupled/` never take a `Customer`
parameter and never import `Shared.Models`. `Program.cs` is the only file that
wires `CustomerDirectory.AddressChanged` to both services' `Handle` methods, so
it is the only place that knows all three modules exist.

## Reproducing the four steps

These map directly onto Part 12's "Try It Yourself" slides.

**Step 1, set up the lab.** Already shipped. `Shared/Models/Customer.cs` is the
trap: `Id`, `Name`, `Address`, all public, all a plain `string`. Three feature
areas, Coupled, Decoupled, and Customer's own directory, sit next to it.

**Step 2, build the trap on purpose.** Already shipped as `Coupled/Services/`.
Read `OrderService.cs`, `PaymentService.cs` and `NotificationService.cs`: three
files, three separate reads of `customer.Address`, none of them aware the other
two exist.

**Step 3, observe.** Change `Address`'s type on the shared model,
`Shared/Models/Customer.cs`:

```csharp
public Address Address { get; set; } = new("", "");
```

```csharp
public record Address(string Line1, string City);
```

Then run:

```bash
dotnet build
```

Expect four errors:

```text
Coupled/Store/CustomerStore.cs(16,71): error CS0029: Cannot implicitly convert
  type 'string' to 'SystemThinkingPart12.Shared.Models.Address'
Coupled/Services/OrderService.cs(11,25): error CS0029: Cannot implicitly
  convert type 'SystemThinkingPart12.Shared.Models.Address' to 'string'
Coupled/Services/PaymentService.cs(12,33): error CS0029: ...
Coupled/Services/NotificationService.cs(11,26): error CS0029: ...
```

`CustomerStore.cs` is the Customer module's own seed data. It is expected to
change: it owns the field. The other three are the actual lesson. `OrderService`,
`PaymentService` and `NotificationService` never asked to depend on `Address`
being a `string`; they inherited that assumption by taking the whole class.
Slide 16 of the deck calls this "one field changed it, three modules failed to
build." This lab's fourth error is the seed data paying the same bill as the
model it owns, not a fourth surprised module.

Revert the change (`git checkout -- Shared/Models/Customer.cs`, or undo it by
hand) before continuing.

**Step 4, fix it with a boundary.** Already shipped as `Decoupled/`. Start the
API and run the same kind of change, this time at runtime instead of compile
time, since `Decoupled/` never named the shared class in the first place:

```bash
dotnet run
```

```bash
curl -X PUT http://localhost:5172/decoupled/customers/1/address \
  -H "Content-Type: application/json" -d '{"newAddress": "88 Baker Street"}'
```

The console prints:

```text
[Customer] CustomerAddressChanged published
[Order]    ShipTo cache updated
[Payment]  BillTo cache updated
```

`GET /decoupled/orders/1` and `GET /decoupled/payments/1` both reflect the new
address; `PaymentCustomer.TaxId` is untouched, because the Customer module never
knew it existed. To confirm the build-time half of the lesson, change
`Shared/Models/Customer.cs`'s `Address` type again and run `dotnet build`.
`Decoupled/Services/OrderService.cs` and `PaymentService.cs` do not appear in
the error list, because neither file imports `Shared.Models`.

## Getting started

Prerequisites: the .NET SDK, nothing else.

```bash
# 1. Run the API. No database, no Docker, no external service.
dotnet run

# 2. In a second terminal, exercise the coupled path
curl -X POST http://localhost:5172/coupled/customers -H "Content-Type: application/json" \
  -d '{"id": 1, "name": "Ada Lovelace", "address": "12 Analytical Engine Way"}'
curl -X POST http://localhost:5172/coupled/orders/1/ship
curl -X POST http://localhost:5172/coupled/payments/1/charge -H "Content-Type: application/json" \
  -d '{"amount": 249.99}'
curl -X POST http://localhost:5172/coupled/notifications/1

# 3. Exercise the decoupled path
curl -X POST http://localhost:5172/decoupled/customers -H "Content-Type: application/json" \
  -d '{"id": 1, "name": "Ada Lovelace", "address": "12 Analytical Engine Way", "taxId": "TAX-90210"}'
curl -X PUT http://localhost:5172/decoupled/customers/1/address -H "Content-Type: application/json" \
  -d '{"newAddress": "88 Baker Street"}'
curl http://localhost:5172/decoupled/orders/1
curl http://localhost:5172/decoupled/payments/1
```

## Project structure

| File | Purpose |
| --- | --- |
| `Shared/Models/Customer.cs` | the trap: one class with `Id`, `Name`, `Address`, read directly by every coupled module |
| `Coupled/Store/CustomerStore.cs` | the seed data and lookup for the coupled path, owns `Customer` |
| `Coupled/Services/OrderService.cs`, `PaymentService.cs`, `NotificationService.cs` | each takes `Customer` and reads `Address` off it |
| `Decoupled/CustomerModule/CustomerDirectory.cs` | owns `CustomerRecord` and publishes `CustomerAddressChanged` |
| `Decoupled/Contracts/OrderCustomer.cs`, `PaymentCustomer.cs` | the small views Order and Payment keep instead of the shared class |
| `Decoupled/Events/CustomerAddressChanged.cs` | the event that replaces a shared reference |
| `Decoupled/Services/OrderService.cs`, `PaymentService.cs` | own a view, update it only from `Handle(CustomerAddressChanged)` |
| `Contracts/` | request DTOs both endpoint groups bind to |
| `Program.cs` | host setup, both paths' endpoints, and the one place that wires the event to its two subscribers |
| `SystemThinkingPart12.csproj` | package references |
| `SystemThinkingPart12.http` | ready-made requests for VS Code / Rider |

## Troubleshooting

- **Port conflicts.** This lab listens on 5172 (HTTP) and 7172 (HTTPS). Change
  either in `Properties/launchSettings.json` if the port is taken.
- **Step 3's errors do not show up.** Confirm the edit landed on
  `Shared/Models/Customer.cs`. If only `CustomerStore.cs` fails and the three
  services still compile, check whether `OrderService.Ship`,
  `PaymentService.Charge` or `NotificationService.Notify` still assign
  `customer.Address` to a `string` local before interpolating it; string
  interpolation alone calls `ToString()` and hides the type mismatch.
- **`GET /decoupled/payments/1` shows the old `TaxId` after an address
  change.** That is the point: `CustomerAddressChanged` carries only the
  address, so `PaymentCustomer.TaxId` is untouched. Compare it against
  `BillTo` on the same response, which does update.
- All examples use `http://localhost:5172`, the Development URL.

## Analytical questions

1. `Coupled/Services/OrderService.cs` and `Decoupled/Services/OrderService.cs`
   both compile without error today. What does "compiles" fail to tell a
   reviewer about how expensive a future change to `Customer` will be?
2. Step 3 produces four `CS0029` errors, not three. Why does
   `Coupled/Store/CustomerStore.cs` belong in a different category from the
   other three, even though it shows the same error code?
3. `Decoupled/CustomerModule/CustomerDirectory.cs` defines `CustomerRecord`
   privately and never exposes `Shared.Models.Customer`. What would change
   about the decoupled path if `CustomerDirectory` returned
   `Shared.Models.Customer` from `Find` instead of its own record?
4. `PaymentCustomer.TaxId` has no equivalent field on `Shared.Models.Customer`.
   Where did that field come from, and what does its existence say about
   which module should be allowed to define a customer's shape for billing
   purposes?
5. `Program.cs` is the only file that subscribes `OrderService.Handle` and
   `PaymentService.Handle` to `CustomerDirectory.AddressChanged`. What
   category of change to the event's shape would still require editing both
   service files despite the event boundary, and does that category exist on
   the coupled side too?
6. This lab keeps both paths in one project so Step 3 and Step 4 can be run
   side by side. `Coupled/` and `Decoupled/` never call each other. What is
   the risk of two teams maintaining a `Coupled`-shaped module and a
   `Decoupled`-shaped module next to each other in a real codebase, once the
   original author leaves?
7. `CustomerAddressChanged` carries `CustomerId` and `NewAddress`, nothing
   else. If `Order` needed the customer's name as well, would adding a
   `Name` field to the event cost less or more than the three modules
   Step 3 breaks, and why?
8. `Coupled/Services/NotificationService.cs` reads `customer.Name` and
   `customer.Address` in the same method. Step 3 only changes `Address`.
   What would Step 3's error list look like if the change had targeted
   `Name` instead, and does the boundary argument still hold?

## License

[GPL-3.0](../../LICENSE)
