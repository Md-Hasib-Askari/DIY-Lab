// using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

// Broken state test: in-memory cache is per-instance, so a session created on
// one server is invisible to another. Login and profile are the two steps.
// builder.Services.AddMemoryCache();

// Distributed cache (the fix) is commented out for the broken state demo.
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "SampleInstance";
});

var app = builder.Build();

// Step 1: login creates a session in this instance's memory.
// app.MapPost(
//     "/login",
//     (string user, IMemoryCache cache) =>
//     {
//         var sessionId = Guid.NewGuid().ToString("N");

//         var options = new MemoryCacheEntryOptions
//         {
//             AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
//         };
//         cache.Set($"session:{sessionId}", user, options);

//         return Results.Ok(new { sessionId });
//     }
// );

// Step 2: profile reads the session from this instance's memory.
// Broken across servers: a session from another instance is not found here.
// app.MapGet(
//     "/profile",
//     (string sessionId, IMemoryCache cache) =>
//         cache.Get<string>($"session:{sessionId}") is string user
//             ? Results.Ok($"Welcome back, {user}")
//             : Results.Unauthorized()
// );

// Distributed cache version (the fix) - commented out for the broken demo.
app.MapPost(
    "/login",
    (string user, IDistributedCache cache) =>
    {
        var sessionId = Guid.NewGuid().ToString("N");
        byte[] userBytes = System.Text.Encoding.UTF8.GetBytes(user);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        cache.Set($"session:{sessionId}", userBytes, options);
        return Results.Ok(new { sessionId });
    }
);

app.MapGet(
    "/profile",
    (string sessionId, IDistributedCache cache) =>
    {
        var userBytes = cache.Get($"session:{sessionId}");
        return userBytes is not null
            ? Results.Ok($"Welcome back, {System.Text.Encoding.UTF8.GetString(userBytes)}")
            : Results.Unauthorized();
    }
);

app.Run();
