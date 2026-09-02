using Microsoft.EntityFrameworkCore;
using SystemThinkingPart10.Features.Orders;
using SystemThinkingPart10.Infrastructure;
using SystemThinkingPart10.Layered.Application;
using SystemThinkingPart10.Layered.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Lab Step 1 and Step 2: one DbContext serves both paths. The layered path
// also needs its service and repository registered through DI; the slice
// path needs nothing registered here at all, because each endpoint reaches
// AppDbContext directly (see Features/Orders/*.cs).
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddControllers();

var app = builder.Build();

// Both paths throw ArgumentException on the same kind of bad input. One
// middleware turns that into a 400 for the layered controller and every
// slice endpoint alike, so neither path carries its own try/catch.
app.Use(
    async (ctx, next) =>
    {
        try
        {
            await next();
        }
        catch (ArgumentException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync(ex.Message);
        }
    }
);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Layered path: one controller class, found through attribute routing.
app.MapControllers();

// Slice path: one MapEndpoint call per feature file. Program.cs only lists
// which slices exist; it holds none of their request-handling logic.
CreateOrder.MapEndpoint(app);
GetOrder.MapEndpoint(app);
CancelOrder.MapEndpoint(app);

app.Run();
