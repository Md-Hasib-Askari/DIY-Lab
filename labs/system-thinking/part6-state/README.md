# Part 6: State

A hands-on lab showing why **state stored in memory breaks across multiple server instances**, and how a **distributed cache** fixes it.

The demo is a tiny session/login flow:

1. `POST /login?user=hasib` creates a session and stores it.
2. `GET /profile?sessionId=...` reads the session back.

## What is broken

The app stores sessions in an **in-memory cache** (`IMemoryCache`). In-memory state lives inside a single process. When you run more than one instance behind a load balancer, a session created on Server 1 is invisible to Server 2, so `GET /profile` on Server 2 returns `401 Unauthorized`.

This is the classic "it works on one server, fails in production" bug.

## Run the broken demo

### 1. Start two instances

```bash
dotnet run --urls=http://localhost:5001
dotnet run --urls=http://localhost:5002
```

(The two terminals simulate two scaled-out instances of the same app.)

### 2. Run the requests

Open [`part6-state.http`](./part6-state.http) in an HTTP client (VS Code REST Client, JetBrains HTTP Client, etc.) and run the steps in order:

- **Step 1** `POST /login` on Server 1, creates a session.
- **Step 2a** `GET /profile` on Server 1, same server, returns `200 Welcome back, hasib`.
- **Step 2b** `GET /profile` on Server 2, different server, returns `401 Unauthorized`. Broken.

## The fix: distributed cache

A `IDistributedCache` (Redis) stores sessions in a shared location that all instances read from. Uncomment the Redis registration and the distributed endpoints in [`Program.cs`](./Program.cs):

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "SampleInstance";
});
```

Then a session created on any instance is readable from every instance.

### Start Redis

```bash
docker compose up -d
```

This starts Redis on `localhost:6379` (see [`docker-compose.yml`](./docker-compose.yml)).

## Files

- `Program.cs` - the app. In-memory (broken) flow is active; distributed (Redis) flow is commented.
- `part6-state.http` - the two-step request guide.
- `docker-compose.yml` - Redis for the distributed cache fix.

## Takeaway

Stateful in-memory storage does not survive horizontal scaling. Move shared state (sessions, caches, rate limits) into a distributed store when you run more than one instance.
