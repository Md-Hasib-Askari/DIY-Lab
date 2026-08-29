using Microsoft.EntityFrameworkCore;
using SystemThinkingPart9.Application;
using SystemThinkingPart9.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Lab Step 1: wire the layers. Register the DbContext so EF Core can talk to
// PostgreSQL (connection string comes from appsettings), then register the
// application service and the repository it depends on through the
// interface, not the concrete type.
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<CreateOrderService>();
builder.Services.AddControllers();

var app = builder.Build();

// The domain throws ArgumentException when a request breaks a business
// rule (see Domain/Order.cs). One line here turns that into a 400 for both
// endpoints, so the slim controller never needs a try/catch of its own.
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

app.MapControllers();

app.Run();
