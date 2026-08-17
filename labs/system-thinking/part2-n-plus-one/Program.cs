using Microsoft.EntityFrameworkCore;
using SystemThinkingPart2.Data;
using SystemThinkingPart2.Services;

var builder = WebApplication.CreateBuilder(args);

// Lab Step 1: wire the layers. Register the DbContext so EF Core can talk
// to PostgreSQL (connection string comes from appsettings), and register
// the service the controller calls.
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<OrderService>();
builder.Services.AddControllers();

var app = builder.Build();

// Lab Step 2: give every request a correlation ID. Every log line from the
// middleware and the endpoints carries this ID, so one request's query storm
// can be told apart from another's in a busy console.
app.Use(async (ctx, next) =>
{
    var id = Guid.NewGuid().ToString("N")[..8];
    ctx.Items["CorrelationId"] = id;
    app.Logger.LogInformation("[{Id}] request started", id);
    await next();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Apply EF migrations at startup
    db.Database.Migrate();

    // Seed orders with 2 items each via SQL bulk insert (fast startup).
    // The order count comes from appsettings (Seed:OrderCount, default 50)
    // so the N in N+1 can be scaled without touching code.
    var orderCount = Math.Max(1, builder.Configuration.GetValue("Seed:OrderCount", 50));
    const int itemsPerOrder = 2;

    if (!db.Orders.Any())
    {
        db.Database.ExecuteSqlInterpolated(
            $"INSERT INTO \"Orders\" (\"CustomerName\") SELECT 'Customer ' || lpad(i::text, 3, '0') FROM generate_series(1, {orderCount}) i");

        db.Database.ExecuteSqlInterpolated(
            $"INSERT INTO \"OrderItems\" (\"ProductName\", \"OrderId\") SELECT 'Product ' || lpad(i::text, 3, '0'), ((i - 1) / {itemsPerOrder}) + 1 FROM generate_series(1, {orderCount * itemsPerOrder}) i");
    }
}

app.MapControllers();

app.Run();
