<div align="center">

# Observability, Part 7

**One trace across two services: see the slow call you cannot explain**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-1.17-425CC7?logo=opentelemetry&logoColor=white)](https://opentelemetry.io/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](../../LICENSE)

</div>

**AppointmentApi** calls **PrescriptionApi** over HTTP after saving an appointment. A
hidden `Task.Delay(3000)` in a "validation" method makes the call take 3.2s, with no clue
why. OpenTelemetry with a console exporter turns that into two spans on one trace ID,
pinning the 3 seconds to the validation method.

The distributed-tracing sequel to [Part 4](../part4-correlation-id/README.md): instead of
forwarding a correlation ID by hand, the standard `traceparent` header does it for free.

> **Repo layout:** this lab lives at `labs/observability/part7-distributed-tracing` on the
> `main` branch. It contains two real projects, one per service. Run them from two
> terminals as shown below. No database, no Docker, no tracing backend: output goes to the
> console.

## Table of contents

- [The four steps](#the-four-steps)
  - [Step 1: Setup](#step-1-setup)
  - [Step 2: Introduce the problem](#step-2-introduce-the-problem)
  - [Step 3: Observe](#step-3-observe)
  - [Step 4: Fix it](#step-4-fix-it)
- [Getting started](#getting-started)
- [Project structure](#project-structure)
- [Results](#results)
- [Notes and gotchas](#notes-and-gotchas)
- [License](#license)

## The four steps

### Step 1: Setup

Two projects, one caller, one callee:

| Service | Project | Port | Route |
| --- | --- | --- | --- |
| AppointmentApi | `src/AppointmentApi` | `5091` | `POST /appointments` |
| PrescriptionApi | `src/PrescriptionApi` | `5092` | `POST /prescriptions/validate` |

`POST /appointments` saves the appointment, then calls `POST /prescriptions/validate`
over HTTP. `PrescriptionApi` runs the validation and replies.

### Step 2: Introduce the problem

The time bomb lives inside `Services/ValidationService.cs`, hidden in a method that
sounds harmless:

```csharp
using var activity = Source.StartActivity("validate-prescription");

_logger.LogInformation(
    "Validating prescription for appointment {AppointmentId}", appointment.Id);

await Task.Delay(3000);   // hidden inside a "validation" method

_logger.LogInformation(
    "Prescription for appointment {AppointmentId} validated", appointment.Id);
```

Run both services (see [Getting started](#getting-started)), then call AppointmentApi
normally, from Postman or curl, and watch the response time:

```bash
curl -w '\n%{time_total}s\n' -X POST http://localhost:5091/appointments \
  -H 'Content-Type: application/json' \
  -d '{"patientName":"Alice","doctorName":"Dr. Smith","reason":"Routine checkup"}'
```

```text
{"id":1,"patientName":"Alice","doctorName":"Dr. Smith","reason":"Routine checkup"}
3.285251s
```

The console shows plain log lines and nothing else:

```text
info: appointments[0]
      Appointment 1 saved
info: System.Net.Http.HttpClient.prescriptions.ClientHandler[100]
      Sending HTTP request POST http://localhost:5092/prescriptions/validate
info: appointments[0]
      PrescriptionApi replied 200
```

"AppointmentApi took 3.2s," with no way to tell which part is slow. That is the problem
this lab exists to solve.

### Step 3: Observe

Add OpenTelemetry with a console exporter and run it again. The wiring is already written,
just commented out, so this step is literal: uncomment it.

**3a. Install the packages.** From each project folder, run the `dotnet add package`
commands for that service:

```bash
# Terminal 1 (src/PrescriptionApi)
dotnet add package OpenTelemetry.Extensions.Hosting --version 1.17.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore --version 1.17.0
dotnet add package OpenTelemetry.Exporter.Console --version 1.17.0

# Terminal 2 (src/AppointmentApi)
dotnet add package OpenTelemetry.Extensions.Hosting --version 1.17.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore --version 1.17.0
dotnet add package OpenTelemetry.Instrumentation.Http --version 1.17.0
dotnet add package OpenTelemetry.Exporter.Console --version 1.17.0
```

AppointmentApi gets `Instrumentation.Http` too, because it is the one that makes the
outgoing call.

**3b. Uncomment the tracing block in `Program.cs`** of both services. In AppointmentApi:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter());
```

In PrescriptionApi, one extra line is included so the span created inside
`ValidationService` is picked up:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing
            .AddSource(ValidationService.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter());
```

Also uncomment the two `using OpenTelemetry;` directives at the top of each `Program.cs`.

**3c. Restart both services and replay the same request.** After the exporter's batch
flush (a few seconds), the console prints the trace. Read it bottom-up, from the most
nested span:

```text
Activity.TraceId:            81fb4c903850e60461015f63f693f96c   ← one ID for the whole story
Activity.DisplayName:        validate-prescription               ← PrescriptionApi
Activity.Duration:           00:00:03.0005459                    ← the 3000ms, exposed

Activity.TraceId:            81fb4c903850e60461015f63f693f96c
Activity.ParentSpanId:       ...
Activity.DisplayName:        POST prescriptions/validate         ← PrescriptionApi
Activity.Duration:           00:00:03.0929916

Activity.TraceId:            81fb4c903850e60461015f63f693f96c   ← AppointmentApi
Activity.DisplayName:        POST appointments
Activity.Duration:           00:00:03.4589546
```

Same `TraceId` on both services. The HTTP client instrumentation stamped the
`traceparent` header on the outgoing request; the server instrumentation on the other
side read it back. No middleware, no manual header code, and the 3000ms span points
straight at the validation method.

### Step 4: Fix it

Move the slow validation off the request path, into a background job. The machinery is
already in AppointmentApi, waiting: `PrescriptionQueue` (a `Channel<Appointment>`) and
`PrescriptionBackgroundService` (a `BackgroundService` that drains the queue and calls the
same validation endpoint). Step 4 is one edit in
`Controllers/AppointmentController.cs`: comment the blocking call, uncomment the enqueue.

```csharp
// ---- STEPS 1-3 (broken): the request blocks on PrescriptionApi, whose
// ---- validation method hides a Task.Delay(3000). Total ~3.2s.
// var client = httpClientFactory.CreateClient("prescriptions");
// var response = await client.PostAsJsonAsync(
//     "/prescriptions/validate",
//     new { appointment.Id }
// );
// _logger.LogInformation(
//     "PrescriptionApi replied {StatusCode}",
//     (int)response.StatusCode
// );

// ---- STEP 4 (fix): comment the three lines above, uncomment these two.
queue.Enqueue(appointment);
_logger.LogInformation("Validation queued for appointment {AppointmentId}", appointment.Id);
```

Restart AppointmentApi and replay the same request:

```bash
curl -w '\n%{time_total}s\n' -X POST http://localhost:5091/appointments \
  -H 'Content-Type: application/json' \
  -d '{"patientName":"Bob","doctorName":"Dr. Jones","reason":"Follow-up"}'
```

```text
{"id":2,"patientName":"Bob","doctorName":"Dr. Jones","reason":"Follow-up"}
0.106602s
```

The request now completes in about a tenth of a second. The 3-second validation still
happens, but under its own trace, created by the background job instead of the user's
request:

```text
Activity.TraceId:            4cec62cbef93e3e3fb18e4b35f594580   ← fast request trace
Activity.DisplayName:        POST appointments
Activity.Duration:           00:00:00.0774983

Activity.TraceId:            7cd33ba751ea5f678d60409679de47a2   ← separate background trace
Activity.DisplayName:        POST /prescriptions/validate
Activity.Duration:           00:00:03.1410913
```

Two traces now: the user's fast request, and the slow background work that used to hide
inside it.

## Getting started

Prerequisite: the .NET 10 SDK. Nothing else, no database, no Docker.

```bash
# 1. Start PrescriptionApi (terminal 1)
dotnet run --project src/PrescriptionApi

# 2. Start AppointmentApi (terminal 2)
dotnet run --project src/AppointmentApi
```

Both listen on `http://localhost:<port>` per the table above. You are at Step 2 of the
walkthrough: `POST http://localhost:5091/appointments` takes ~3.2s.

Ready-made requests live in [`ObservabilityPart7.http`](ObservabilityPart7.http) for
VS Code or Rider's HTTP client.

## Project structure

```
part7-distributed-tracing/
├── ObservabilityPart7.http          ready-made requests for both services
└── src/
    ├── AppointmentApi/              the service that starts the trace (port 5091)
    │   ├── Controllers/AppointmentController.cs   POST /appointments
    │   ├── Services/AppointmentStore.cs           in-memory store
    │   ├── Services/PrescriptionQueue.cs          Channel<Appointment> for the fix
    │   ├── Services/PrescriptionBackgroundService.cs  the background job (step 4)
    │   ├── Models/Appointment.cs, DTOs/AppointmentDto.cs
    │   ├── Program.cs               commented OpenTelemetry block (step 3)
    │   └── Properties/launchSettings.json
    └── PrescriptionApi/             the downstream service (port 5092)
        ├── Controllers/PrescriptionController.cs POST /prescriptions/validate
        ├── Services/ValidationService.cs         the hidden Task.Delay(3000) in a span
        ├── Models/AppointmentReference.cs
        ├── Program.cs               commented OpenTelemetry block (step 3)
        └── Properties/launchSettings.json
```

Configuration:

| Setting | Where | Effect |
| --- | --- | --- |
| `PrescriptionServiceUrl` | `src/AppointmentApi/appsettings.json` | base address of PrescriptionApi (`http://localhost:5092`) |
| `applicationUrl` | each project's `Properties/launchSettings.json` | the ports (`5091`, `5092`) |

## Results

| Step | Response time | What the console shows |
| --- | --- | --- |
| 2 (broken) | ~3.2s | plain log lines, the 3s gap invisible |
| 3 (observed) | ~3.2s | two spans, one `TraceId`, 3000ms on `validate-prescription` |
| 4 (fixed) | ~0.1s | fast request trace; slow validation in its own background trace |

The lesson in one line: tracing replaces "AppointmentApi took 3.2s" with a span tree that
names the guilty method, across service boundaries, with no manual header plumbing.

## Notes and gotchas

- **The console exporter flushes in batches.** Spans appear a few seconds after the
  request, not instantly. If you see nothing, wait a moment or press Ctrl+C, which forces
  a final flush.
- **`traceparent` does the forwarding.** The W3C `traceparent` header is injected by the
  HttpClient instrumentation and parsed by the ASP.NET Core instrumentation. That is the
  entire wiring between the two services; delete the header and the spans stop sharing a
  trace ID.
- **`AddSource(...)` is what publishes your own spans.** The ASP.NET Core and HttpClient
  instrumentation emit their spans automatically. The `validate-prescription` span is
  created by an `ActivitySource` in `ValidationService`, so PrescriptionApi must list that
  source with `AddSource`, or the span is created but never exported.
- **Startup spans export first.** Because the batch processor emits spans in order, the
  nested `validate-prescription` span prints before its parent `POST /prescriptions/validate`.
  Read the `TraceId`, not the print order.
- **One unread-parameter warning is expected.** In the broken state the controller's
  `queue` parameter is unused (the fix is commented out); in the fixed state it is
  `httpClientFactory` that goes unused. The warnings flip exactly as you move through the
  steps, and are a reliable sign that you are on the right step.
- **PrescriptionApi started first is a nice-to-have, not a requirement.** AppointmentApi
  fails that call only if the downstream is down; here both live in the same session, so
  start order does not matter.

## License

[GPL-3.0](../../LICENSE)