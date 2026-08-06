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

## Getting started

Each lab lives in its own folder and runs independently with `dotnet run` (some
require Docker for a backing database). Check the lab's own README for setup steps.

## License

[GPL-3.0](LICENSE)
