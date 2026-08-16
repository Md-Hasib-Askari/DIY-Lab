using SystemThinkingPart1.Domain;
using SystemThinkingPart1.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

var dbConnectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dbConnectionString));
builder.Services.AddMemoryCache();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/products", async (AppDbContext db, IMemoryCache cache, IConfiguration config) =>
{
    var useCache = config.GetValue<bool>("Cache:Enabled");
    if (useCache)
    {
        var cached = await cache.GetOrCreateAsync("products", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromSeconds(30);
            return await LoadProducts(db, config);
        });
        return Results.Ok(cached);
    }

    return Results.Ok(await LoadProducts(db, config));
});

app.Run();

static async Task<List<Product>> LoadProducts(AppDbContext db, IConfiguration config)
{
    var simulatedMs = config.GetValue<int>("SimulatedDelayMs");
    if (simulatedMs > 0)
    {
        await db.Database.OpenConnectionAsync();
        await Task.Delay(simulatedMs);
    }

    return await db.Products.ToListAsync();
}