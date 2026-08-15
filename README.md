<div align="center">

# DIY-Lab

**Backend engineering ideas from LinkedIn posts, each backed by runnable code.**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)

</div>

A collection of hands-on labs for exploring system design patterns, performance
tweaks, and real implementations. Pick a lab, check out its branch, run it, and see
the idea in action.

## Labs

Each lab lives on its own branch, with the project at the repository root. Check out
the branch and follow that lab's README.

| Lab | Branch | Topic |
| --- | --- | --- |
| Observability Part 1 | [`Part1`](https://github.com/Md-Hasib-Askari/DIY-Lab/tree/Part1) | Diagnosing a slow .NET API with only built-in tooling: `ILogger`, `Stopwatch`, and per-request correlation IDs. |
| Observability Part 2 | [`Part2`](https://github.com/Md-Hasib-Askari/DIY-Lab/tree/Part2) | Two identical order endpoints, one logging with `Console.WriteLine`, one with structured `ILogger` properties. |
| Observability Part 3 | — | No lab in this repo. That post was theory only, with nothing runnable to build. |
| Observability Part 4 | [`Part4`](https://github.com/Md-Hasib-Askari/DIY-Lab/tree/Part4) | A correlation ID that survives a service hop: middleware, Serilog `LogContext`, and a forwarding `DelegatingHandler`. |
| Observability Part 5 | [`Part5`](https://github.com/Md-Hasib-Askari/DIY-Lab/tree/Part5) | Four endpoints, one per metric (latency, throughput, error rate, saturation), hammered by `loadtests/` and instrumented server-side: `System.Diagnostics.Metrics` histograms plus resource-usage gauges, exposed via a minimal OpenTelemetry Prometheus `/metrics` scrape. |

## Getting started

```bash
# Pick a lab and check out its branch
git checkout Part1

# Follow the lab's README for setup steps, then run it
dotnet run
```

Some labs require Docker for a backing database. Check the lab's own README for the
exact steps and ports.

## License

[GPL-3.0](LICENSE)