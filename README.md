<div align="center">

# DIY-Lab

**Backend engineering ideas from LinkedIn posts, each backed by runnable code.**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)

</div>

A collection of hands-on labs for exploring system design patterns, performance
tweaks, and real implementations. Every lab lives in this repository, on the `main`
branch, as its own standalone project.

## Repository layout

```
DIY-Lab.slnx                     the solution that links every lab
labs/
├── observability/               current series: observability
│   ├── part1-slow-api/          ObservabilityPart1
│   ├── part2-structured-logging/ ObservabilityPart2
│   ├── part4-correlation-id/    ObservabilityPart4
│   ├── part5-metrics/           ObservabilityPart5
│   ├── part6-tail-latency/      ObservabilityPart6
│   └── part7-distributed-tracing/  AppointmentApi + PrescriptionApi
└── system-thinking/             current series: system thinking
    ├── part1-code-to-systems/   SystemThinkingPart1
    ├── part2-n-plus-one/        SystemThinkingPart2
    ├── part5-cpu-vs-io/         SystemThinkingPart5
    ├── part9-layered-architecture/  SystemThinkingPart9 + Tests
    ├── part10-vertical-slice-architecture/  SystemThinkingPart10
    └── part11-dependency-coupling/  SystemThinkingPart11
```

Labs are grouped by series, and each series by part number. Each lab folder is a
self-contained set of .NET projects with its own README, configuration, and load tests, so
labs stay isolated from one another and new series can be added as a new folder under
`labs/`. Part 7 contains two projects, one per service, and is run from two terminals.

## Labs

| Series | Lab | Project | Topic |
| --- | --- | --- | --- |
| Observability | Part 1 | [`labs/observability/part1-slow-api`](labs/observability/part1-slow-api) | Diagnosing a slow .NET API with only built-in tooling: `ILogger`, `Stopwatch`, and per-request correlation IDs. |
| Observability | Part 2 | [`labs/observability/part2-structured-logging`](labs/observability/part2-structured-logging) | Two identical order endpoints, one logging with `Console.WriteLine`, one with structured `ILogger` properties. |
| Observability | Part 3 | — | No lab in this repo. That post was theory only, with nothing runnable to build. |
| Observability | Part 4 | [`labs/observability/part4-correlation-id`](labs/observability/part4-correlation-id) | A correlation ID that survives a service hop: middleware, Serilog `LogContext`, and a forwarding `DelegatingHandler`. |
| Observability | Part 5 | [`labs/observability/part5-metrics`](labs/observability/part5-metrics) | Four endpoints, one per metric (latency, throughput, error rate, saturation), hammered by `loadtests/` and instrumented server-side: `System.Diagnostics.Metrics` histograms plus resource-usage gauges, exposed via a minimal OpenTelemetry Prometheus `/metrics` scrape. |
| Observability | Part 6 | [`labs/observability/part6-tail-latency`](labs/observability/part6-tail-latency) | A patient lookup API with a hidden 5% slow path, showing why tail percentiles expose what averages hide. |
| Observability | Part 7 | [`labs/observability/part7-distributed-tracing`](labs/observability/part7-distributed-tracing) | Two services, one trace: AppointmentApi calls PrescriptionApi, whose "validation" method hides a 3-second delay. A console exporter names the guilty span; a background job moves it off the request path. |
| System Thinking | Part 1 | [`labs/system-thinking/part1-code-to-systems`](labs/system-thinking/part1-code-to-systems) | A `/products` API that goes from instant to 500ms on purpose, then gets hammered by k6 at 1 and 50 virtual users. The same code tells two different stories; an `IMemoryCache` fix shows what a single knob can do. |
| System Thinking | Part 2 | [`labs/system-thinking/part2-n-plus-one`](labs/system-thinking/part2-n-plus-one) | An Orders API that issues 51 queries per request on purpose: the classic EF Core N+1 problem. A correlation ID plus EF's own SQL logging make the query storm countable, and `.Include` + `.AsNoTracking` bring it down to 1. |
| System Thinking | Part 5 | [`labs/system-thinking/part5-cpu-vs-io`](labs/system-thinking/part5-cpu-vs-io) | A CPU-bound `/report` and an I/O-bound `/users` sharing one thread pool. Run together, `/users` goes from a 51ms p95 to 11 seconds with zero errors and no code change. `await` fixes one endpoint; only a background worker fixes the other. |
| System Thinking | Part 9 | [`labs/system-thinking/part9-layered-architecture`](labs/system-thinking/part9-layered-architecture) | The same approval rule built two ways: a fat controller reading `AppDbContext` directly, and a Domain/Application/Infrastructure/Api split where the rule lives in a framework-free class. The payoff is a unit test that needs zero setup. |
| System Thinking | Part 10 | [`labs/system-thinking/part10-vertical-slice-architecture`](labs/system-thinking/part10-vertical-slice-architecture) | The same three operations (create, read, cancel an order) built two ways: a shared controller/service/repository split, and one file per operation under `Features/Orders/`. Adding a fourth operation and running `git diff --stat` on both shows the payoff: four existing files touched versus one new file and one registration line. |
| System Thinking | Part 11 | [`labs/system-thinking/part11-dependency-coupling`](labs/system-thinking/part11-dependency-coupling) | The same three services built two ways: each constructing `SqlOrderRepository` directly with `new`, versus each depending on an `IOrderRepository` registered once in `Program.cs`. Adding a required constructor parameter to the repository breaks three files on the coupled path and zero on the decoupled path. |

## Getting started

```bash
# Build every lab at once
dotnet build

# Run a single lab (each listens on its own port)
dotnet run --project labs/observability/part2-structured-logging
```

Each lab's README has the exact setup steps for that lab. Some require Docker for a
backing database or extra tooling (k6, Python) for load tests; check the lab's own
README for the details.

## License

[GPL-3.0](LICENSE)

