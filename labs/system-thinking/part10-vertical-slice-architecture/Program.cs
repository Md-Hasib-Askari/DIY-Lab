using Microsoft.EntityFrameworkCore;
using SystemThinkingPart10.Features.Orders;
using SystemThinkingPart10.Infrastructure;
using SystemThinkingPart10.Layered.Application;
using SystemThinkingPart10.Layered.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Lab Step 1 and Step 2: one DbContext serves both paths. The layered path
// registers its service and its repository one interface at a time. The
// slice path registers nothing per operation: one scan of the assembly
// picks up every Handler under Features/, so adding a slice adds no line
// here (see Features/Orders/*.cs).
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddControllers();

// One scan finds every IRequestHandler in this assembly, including the
// nested Handler class inside each slice.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SliceOrder>());

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
