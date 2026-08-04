using Microsoft.EntityFrameworkCore;
using ObservabilityPart1.Data;
using ObservabilityPart1.Infrastructure;
using ObservabilityPart1.Models;
using ObservabilityPart1.Phases;

var builder = WebApplication.CreateBuilder(args);

// EF Core with Npgsql (Postgres)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

// HttpClient routed to a fake handler: no real network, always sleeps 2s
builder.Services.AddSingleton(new HttpClient(new FakeHttpMessageHandler()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Apply EF migrations at startup
    db.Database.Migrate();

    // Seed only on an empty table
    if (!db.Patients.Any())
    {
        // Bulk-insert names via generate_series (fast way to make 1M rows)
        var count = app.Configuration.GetValue<int>("SeedPatients");
        db.Database.ExecuteSqlRaw(
            "INSERT INTO \"Patients\" (\"Name\") SELECT 'Patient' || lpad(i::text, 6, '0') "
                + "FROM generate_series(1, {0}) i",
            count
        );

        // Give the first 200 patients two prescriptions each
        var first = await db.Patients.OrderBy(p => p.Id).Take(200).ToListAsync();
        foreach (var patient in first)
        {
            patient.Prescriptions.AddRange([
                new Prescription { Drug = $"{patient.Name}-Drug-A" },
                new Prescription { Drug = $"{patient.Name}-Drug-B" },
            ]);
        }
        await db.SaveChangesAsync();
    }
}

// Each phase is its own endpoint so all four can be run and compared in one process.
Phase1.Map(app);
Phase2.Map(app);
Phase3.Map(app);
Phase4.Map(app);

app.Run();
