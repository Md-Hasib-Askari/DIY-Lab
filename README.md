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
│   └── part6-tail-latency/      ObservabilityPart6
└── system-thinking/             next series (coming soon)
```

Labs are grouped by series, and each series by part number. Each lab folder is a
self-contained .NET project with its own README, configuration, and load tests, so
labs stay isolated from one another and new series can be added as a new folder under
`labs/`.

## Labs

| Series | Lab | Project | Topic |
| --- | --- | --- | --- |
| Observability | Part 1 | [`labs/observability/part1-slow-api`](labs/observability/part1-slow-api) | Diagnosing a slow .NET API with only built-in tooling: `ILogger`, `Stopwatch`, and per-request correlation IDs. |
| Observability | Part 2 | [`labs/observability/part2-structured-logging`](labs/observability/part2-structured-logging) | Two identical order endpoints, one logging with `Console.WriteLine`, one with structured `ILogger` properties. |
| Observability | Part 3 | — | No lab in this repo. That post was theory only, with nothing runnable to build. |
| Observability | Part 4 | [`labs/observability/part4-correlation-id`](labs/observability/part4-correlation-id) | A correlation ID that survives a service hop: middleware, Serilog `LogContext`, and a forwarding `DelegatingHandler`. |
| Observability | Part 5 | [`labs/observability/part5-metrics`](labs/observability/part5-metrics) | Four endpoints, one per metric (latency, throughput, error rate, saturation), hammered by `loadtests/` and instrumented server-side: `System.Diagnostics.Metrics` histograms plus resource-usage gauges, exposed via a minimal OpenTelemetry Prometheus `/metrics` scrape. |
| Observability | Part 6 | [`labs/observability/part6-tail-latency`](labs/observability/part6-tail-latency) | A patient lookup API with a hidden 5% slow path, showing why tail percentiles expose what averages hide. |

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

