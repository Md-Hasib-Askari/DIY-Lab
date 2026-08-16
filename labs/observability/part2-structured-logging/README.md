<div align="center">

# Observability, Part 2

**Two identical endpoints, two very different error logs**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

A tiny in-memory order API that shows why `ILogger` with structured properties beats a
bare `Console.WriteLine` when things go wrong. The same exception is logged two ways,
side by side, so the difference is visible in the console without any log shipper, APM,
or third-party package.

> **Repo layout:** this lab lives at `labs/observability/part2-structured-logging` on the
> `main` branch. Run it from its own folder: `cd labs/observability/part2-structured-logging`,
> then the commands below. From the repo root you can also run
> `dotnet run --project labs/observability/part2-structured-logging`.

## Table of contents

- [How it works](#how-it-works)
- [The two endpoints](#the-two-endpoints)
- [Getting started](#getting-started)
- [Project structure](#project-structure)
- [Results](#results)
- [License](#license)

## How it works

Both endpoints accept the same JSON body and delegate to the same `OrderService.CreateOrder`,
which maps the DTO to an `Order`, assigns the next Id, stores it in an in-memory list, and
returns `201 Created`.

They differ only in the `catch` block:

| Endpoint | Logging in the catch block | What the console shows |
| --- | --- | --- |
| `POST /orders/wrong` | `Console.WriteLine($"Order failed: {ex.Message}")` | A flat string. No level, no category, no exception type, no stack trace. |
| `POST /orders/correct` | `logger.LogError(ex, "Order failed for customer {CustomerId}", dto.CustomerId)` | A `fail` level entry with `CustomerId` as a queryable named property and the full exception and stack trace. |

Both return HTTP 500. The wrong one leaves you guessing; the correct one tells you which
customer was involved and exactly where it blew up.

## The two endpoints

### Happy path

Send a complete body and both return `201 Created`:

```bash
curl -X POST http://localhost:5225/orders/correct \
  -H "Content-Type: application/json" \
  -d '{"customerId":42,"customerName":"Alice","items":[{"product":"Screwdriver","quantity":2,"unitPrice":9.99}]}'
```

### Trigger the catch block

Omit the `items` field. `OrderDto.Items` defaults to `null`, so the request binds
successfully, `OrderService.CreateOrder` dereferences the null list, and a
`NullReferenceException` is thrown inside the endpoint's `try`. Both endpoints return 500
and log it:

```bash
curl -X POST http://localhost:5225/orders/wrong \
  -H "Content-Type: application/json" \
  -d '{"customerId":42,"customerName":"Alice"}'

curl -X POST http://localhost:5225/orders/correct \
  -H "Content-Type: application/json" \
  -d '{"customerId":42,"customerName":"Alice"}'
```

Console output after `POST /orders/wrong`:

```text
Order failed: Value cannot be null. (Parameter 'source')
```

Console output after `POST /orders/correct`:

```text
fail: ObservabilityPart2.Controllers.OrderController[0]
      Order failed for customer 42
      System.ArgumentNullException: Value cannot be null. (Parameter 'source')
         at System.Linq.ThrowHelper.ThrowArgumentNullException(ExceptionArgument argument)
         at System.Linq.Enumerable.Select[TSource,TResult](IEnumerable`1 source, Func`2 selector)
         at ObservabilityPart2.Services.OrderService.CreateOrder(OrderDto dto)
```

## Getting started

Prerequisite: the .NET 10 SDK.

```bash
# 1. Run the API
dotnet run

# 2. In a second terminal, create an order
curl -X POST http://localhost:5225/orders/correct \
  -H "Content-Type: application/json" \
  -d '{"customerId":42,"customerName":"Alice","items":[{"product":"Hammer","quantity":1,"unitPrice":19.99}]}'
```

Ready-made requests (happy path plus catch-triggering bodies for both endpoints) live in
[`ObservabilityPart2.http`](ObservabilityPart2.http) for VS Code or Rider's HTTP client.

## Project structure

| File | Purpose |
| --- | --- |
| `Controllers/OrderController.cs` | `CreateOrderWrong` and `CreateOrderCorrect`, the lab's two endpoints |
| `Services/OrderService.cs` | business logic: DTO-to-model mapping and the in-memory store |
| `Models/Order.cs` | `Order` and `OrderItem` entities |
| `DTOs/OrderDto.cs` | `OrderDto` and `OrderItemDto` request records |
| `Program.cs` | host setup, registers `OrderService` as a singleton |
| `ObservabilityPart2.http` | ready-made requests for both routes and the error case |
| `appsettings.json` | logging configuration |

All data is stored in memory (`IList<Order>` in `OrderService`). The service is registered
as a singleton, so orders persist across requests and only reset when the app restarts.
No database is involved.

## Results

The two console lines captured above are the whole lesson:

1. The plain log from `Console.WriteLine` has no structure: no level, no category, no
   named fields, no stack trace. Searching or filtering it across log files is guesswork.
2. The `ILogger` line is a proper `fail` entry, scoped to the controller category, with
   `CustomerId` exposed as a named property and the full exception attached. Any log
   pipeline (console, files, cloud) can index `CustomerId` and render the stack trace.

## License

[GPL-3.0](../../LICENSE)
