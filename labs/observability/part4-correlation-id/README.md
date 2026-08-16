<div align="center">

# Observability, Part 4

**One ID that follows a request from service to service**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Serilog](https://img.shields.io/badge/Serilog-LogContext-1E88E5)](https://serilog.net/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

A CRM endpoint saves a customer and then asks a notification service to send the welcome
email. Two services, one user action. When something goes wrong at 2am, the only question
that matters is which log lines belong to the same request, and by default nothing answers
it.

This lab wires a correlation ID into three places: middleware that mints it, Serilog's
`LogContext` that attaches it to every line, and a `DelegatingHandler` that forwards it on
every outgoing HTTP call. No APM vendor, no tracing backend, no distributed system to
deploy. Data is in memory.

> **Repo layout:** this lab lives at `labs/observability/part4-correlation-id` on the
> `main` branch. Run it from its own folder: `cd labs/observability/part4-correlation-id`,
> then the commands below. From the repo root you can also run
> `dotnet run --project labs/observability/part4-correlation-id`.

## Table of contents

- [The problem](#the-problem)
- [The three implementations](#the-three-implementations)
  - [01: Assign it in middleware](#01-assign-it-in-middleware)
  - [02: Push it into every log line](#02-push-it-into-every-log-line)
  - [03: Forward it to other services](#03-forward-it-to-other-services)
- [Getting started](#getting-started)
- [Project structure](#project-structure)
- [Results](#results)
- [Notes and gotchas](#notes-and-gotchas)
- [License](#license)

## The problem

Three customers are created at the same moment. Here is what the console says, with the
correlation ID column removed (the real captured run, minus one field):

```text
[01:01:24.242 INF] [crm] Creating customer Carol
[01:01:24.242 INF] [crm] Creating customer Alice
[01:01:24.242 INF] [crm] Creating customer Bob
[01:01:24.344 INF] [crm.store] Customer 3 saved
[01:01:24.344 INF] [crm.store] Customer 2 saved
[01:01:24.344 INF] [crm.store] Customer 1 saved
[01:01:24.408 INF] [notifications] Welcome email requested for customer 2
[01:01:24.408 INF] [notifications] Welcome email requested for customer 1
[01:01:24.408 INF] [notifications] Welcome email requested for customer 3
[01:01:24.528 INF] [notifications] Welcome email sent to bob@example.com
[01:01:24.528 INF] [notifications] Welcome email sent to alice@example.com
[01:01:24.528 INF] [notifications] Welcome email sent to carol@example.com
[01:01:24.538 INF] [crm] Notification service replied 202
[01:01:24.538 INF] [crm] Notification service replied 202
[01:01:24.538 INF] [crm] Notification service replied 202
```

Which customer Id did Alice get? Which notification belongs to Bob's request? The
timestamps do not answer it, because the requests overlap to the millisecond. Ordering
does not answer it either: `Creating customer Carol` printed first, yet Carol ended up as
customer 3, and `Customer 1 saved` belongs to Bob, whose line printed last. And nothing at
all connects a `crm` line to the `notifications` line it caused.

Add one field and the same lines sort themselves:

```text
[01:01:24.242 INF] [crm] [72301156-ab8d-4fe1-b015-0d7b5f5e160d] Creating customer Alice
[01:01:24.344 INF] [crm.store] [72301156-ab8d-4fe1-b015-0d7b5f5e160d] Customer 2 saved
[01:01:24.408 INF] [notifications] [72301156-ab8d-4fe1-b015-0d7b5f5e160d] Welcome email requested for customer 2
[01:01:24.528 INF] [notifications] [72301156-ab8d-4fe1-b015-0d7b5f5e160d] Welcome email sent to alice@example.com
[01:01:24.538 INF] [crm] [72301156-ab8d-4fe1-b015-0d7b5f5e160d] Notification service replied 202
```

Alice's request, end to end, across both services. Full capture in
[`snapshots/console.txt`](snapshots/console.txt).

## The three implementations

| Step | File | What it does |
| --- | --- | --- |
| 01 | `Middleware/CorrelationIdMiddleware.cs` | Reads `X-Correlation-ID` or creates one, stores it, echoes it on the response |
| 02 | `Middleware/CorrelationIdMiddleware.cs` + `Program.cs` | `LogContext.PushProperty` puts the ID on every log line written during the request |
| 03 | `Infrastructure/CorrelationIdHandler.cs` | A `DelegatingHandler` that stamps the header on every outgoing `HttpClient` call |

### 01: Assign it in middleware

The cleanest place to create or read the ID is a small piece of middleware, registered
first in the pipeline so nothing runs before it:

```csharp
var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
var correlationId = string.IsNullOrWhiteSpace(incoming)
    ? Guid.NewGuid().ToString()
    : incoming;

context.Items[ItemKey] = correlationId;
context.Response.Headers[HeaderName] = correlationId;
```

Three decisions worth noticing:

- **Read before you create.** If the caller already sent an ID, this request is part of a
  trace that started somewhere else. Minting a new one there would cut the chain.
- **`context.Items`** is per-request storage, disposed with the request. It is how the
  outgoing `DelegatingHandler` later finds the ID.
- **Echo it on the response.** The header goes out before `next()` runs, so it is set
  while the response is still unstarted. A user reporting a bug can now paste an ID that
  matches your logs.

### 02: Push it into every log line

`LogContext.PushProperty` attaches the ID to every log statement written inside the
`using` block, on any logger, in any class, on any awaited continuation of that request:

```csharp
using (LogContext.PushProperty("CorrelationId", correlationId))
{
    await next();
}
```

Two things have to line up for this to reach the console, both in `Program.cs`:

```csharp
.Enrich.FromLogContext()
.WriteTo.Console(outputTemplate:
    "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
```

Without `Enrich.FromLogContext()` the pushed property never becomes part of the log event,
and `{CorrelationId}` renders empty.

The proof that you never pass the ID manually again is `Services/CustomerService.cs`.
Nothing hands it a correlation ID, it takes no such parameter, and it does not know the
middleware exists:

```csharp
_logger.LogInformation("Customer {CustomerId} saved", customer.Id);
```

Yet its line carries the ID like every other:

```text
[01:01:24.344 INF] [crm.store] [72301156-ab8d-4fe1-b015-0d7b5f5e160d] Customer 2 saved
```

### 03: Forward it to other services

An ID that stops at the service boundary only solves half the problem. A
`DelegatingHandler` attaches it to every outgoing request, so no call site has to remember:

```csharp
if (httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] is string correlationId
    && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
{
    request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
}
```

Registered once, in `Program.cs`, against the client that talks to the notification
service:

```csharp
builder.Services.AddHttpClient("notifications", client => client.BaseAddress = new Uri(notificationServiceUrl))
    .AddHttpMessageHandler<CorrelationIdHandler>();
```

`CrmController` just calls `PostAsJsonAsync`. The header is not mentioned anywhere in the
controller. On the receiving end, the same middleware reads the incoming header instead of
generating a new ID, and the trace continues.

## Getting started

Prerequisite: the .NET 10 SDK. Nothing else, no database, no Docker.

```bash
# 1. Run the API
dotnet run

# 2. In a second terminal, create a customer
curl -i -X POST http://localhost:5038/crm/customers \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice","email":"alice@example.com"}'
```

The notification service is a second controller in the same process, called over real HTTP
via `HttpClient`. The header genuinely crosses an HTTP boundary; there is just one process
to start.

Three things worth trying:

**Watch the ID come back to you.** The response carries the header the logs are keyed by:

```text
HTTP/1.1 201 Created
X-Correlation-ID: 72301156-ab8d-4fe1-b015-0d7b5f5e160d
```

**Supply your own ID.** Anything the caller sends is reused verbatim, so an ID from an
upstream system, a job runner, or a support ticket flows straight through:

```bash
curl -i -X POST http://localhost:5038/crm/customers \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: order-4417-retry" \
  -d '{"name":"Dave","email":"dave@example.com"}'
```

```text
[01:01:24.546 INF] [crm] [order-4417-retry] Creating customer Dave
[01:01:24.626 INF] [crm.store] [order-4417-retry] Customer 4 saved
[01:01:24.630 INF] [notifications] [order-4417-retry] Welcome email requested for customer 4
[01:01:24.750 INF] [notifications] [order-4417-retry] Welcome email sent to dave@example.com
[01:01:24.752 INF] [crm] [order-4417-retry] Notification service replied 202
```

**Break the chain on purpose.** Call the notification service directly, with no header for
it to inherit, and it starts a trace of its own that leads back to nothing:

```bash
curl -i -X POST http://localhost:5038/notifications/welcome-email \
  -H "Content-Type: application/json" \
  -d '{"customerId":99,"email":"orphan@example.com"}'
```

```text
[01:01:24.774 INF] [notifications] [57b25493-dc64-410e-a266-69cdaa0f8965] Welcome email requested for customer 99
```

That orphan ID is exactly what every downstream service logs when step 03 is missing: real
IDs, correct in isolation, joined to nothing upstream.

Ready-made requests for all three live in
[`ObservabilityPart4.http`](ObservabilityPart4.http) for VS Code or Rider's HTTP client.

## Project structure

| File | Purpose |
| --- | --- |
| `Middleware/CorrelationIdMiddleware.cs` | implementations 01 and 02: mint or reuse the ID, echo it, push it into `LogContext` |
| `Infrastructure/CorrelationIdHandler.cs` | implementation 03: `DelegatingHandler` that forwards the header |
| `Controllers/CrmController.cs` | `POST /crm/customers`, the service that starts the trace |
| `Controllers/NotificationController.cs` | `POST /notifications/welcome-email`, the downstream service |
| `Services/CustomerService.cs` | in-memory store; proof that nested classes inherit the ID for free |
| `Models/Customer.cs`, `DTOs/CustomerDto.cs` | entity and request records |
| `Program.cs` | Serilog console template, middleware registration, the `HttpClient` with the handler |
| `scripts/capture-snapshots.sh` | regenerates `snapshots/` |
| `snapshots/` | captured console and curl output |
| `ObservabilityPart4.http` | ready-made requests |

Configuration:

| Setting | Where | Effect |
| --- | --- | --- |
| `NotificationServiceUrl` | `appsettings.json` | base address of the notification service (`http://localhost:5038`) |
| `applicationUrl` | `Properties/launchSettings.json` | the port the API listens on (`5038`); change both together |

## Results

Regenerate with [`scripts/capture-snapshots.sh`](scripts/capture-snapshots.sh), which
starts the API, runs the three scenarios, and writes:

| File | What it proves |
| --- | --- |
| `snapshots/console.txt` | three concurrent requests interleaved, each traceable across both services by its ID |
| `snapshots/curl.txt` | the ID echoed back on every response, including the caller-supplied one |

The lesson in one line: the ID is created in exactly one place, attached to logs by the
logging pipeline rather than by callers, and forwarded by the HTTP client rather than by
call sites. No business code mentions it.

## Notes and gotchas

- **Set the response header before `await next()`.** Once the response has started,
  writing headers throws. The middleware does it up front for that reason.
- **An empty header is not an ID.** `X-Correlation-ID:` with no value would otherwise
  produce a blank column; `string.IsNullOrWhiteSpace` treats it as absent.
- **Incoming IDs are echoed verbatim.** That is what makes traces joinable, and it means a
  public-facing service is letting callers write into its logs. Behind a gateway this is
  fine. On the edge, validate the shape (a `Guid.TryParse` or a length cap) before trusting
  it.
- **`{CorrelationId}` renders empty outside a request.** Startup and shutdown events happen
  before any request exists, so they have no trace to belong to. You will not see them here
  because they come from `Microsoft.*` categories, which are capped below `Information`.
- **`Microsoft` and `System.Net.Http.HttpClient` are capped at `Warning`** in `Program.cs`,
  purely to keep the console readable. Drop the second override to watch the outgoing
  request logs carry the ID too.

## License

[GPL-3.0](../../LICENSE)
