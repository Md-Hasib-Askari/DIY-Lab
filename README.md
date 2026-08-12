<div align="center">

# DIY-Lab

**Backend engineering ideas from LinkedIn posts, each backed by runnable code.**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)

</div>

A collection of hands-on labs for exploring system design patterns, performance
tweaks, and real implementations. Pick a lab, run it, and see the idea in action.

## Labs

| Lab | Topic |
| --- | --- |
| [Observability Part 1](ObservabilityPart1/README.md) | Diagnosing a slow .NET API with only built-in tooling: `ILogger`, `Stopwatch`, and per-request correlation IDs. |
| [Observability Part 2](ObservabilityPart2/README.md) | Two identical order endpoints, one logging with `Console.WriteLine`, one with structured `ILogger` properties. |
| Observability Part 3 | No lab in this repo. That post was theory only, with nothing runnable to build. |
| [Observability Part 4](ObservabilityPart4/README.md) | A correlation ID that survives a service hop: middleware, Serilog `LogContext`, and a forwarding `DelegatingHandler`. |
| [Observability Part 5](ObservabilityPart5/README.md) | Four endpoints, one per metric (latency, throughput, error rate, saturation), hammered by `loadtests/` and instrumented server-side: `System.Diagnostics.Metrics` histograms plus resource-usage gauges, exposed via a minimal OpenTelemetry Prometheus `/metrics` scrape. |

## Getting started

Each lab lives in its own folder and runs independently with `dotnet run` (some
require Docker for a backing database). Check the lab's own README for setup steps.

## License

[GPL-3.0](LICENSE)
