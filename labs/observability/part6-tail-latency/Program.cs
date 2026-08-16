using ObservabilityPart6.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var dbConnectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dbConnectionString));

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet(
    "/patients/{id}",
    async (int id, AppDbContext db) =>
    {
        if (Random.Shared.Next(0, 100) < 5)
        {
            await Task.Delay(4000);
        }

        var patient = await db.Patients.FindAsync(id);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    }
);

app.Run();
