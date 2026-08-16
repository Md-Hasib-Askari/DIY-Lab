using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ObservabilityPart1.Data;
using ObservabilityPart1.Models;

namespace ObservabilityPart1.Phases;

public static class Phase4
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/phase4/patients/{id}", async (int id, AppDbContext db, HttpClient http, ILogger<Program> logger) =>
        {
            var reqId = Guid.NewGuid();
            var swTotal = Stopwatch.StartNew();

            var swDb = Stopwatch.StartNew();
            var patient = await db.Patients.FirstOrDefaultAsync(p => p.Name == $"Patient{id:D6}");
            swDb.Stop();
            logger.LogInformation("[{ReqId}] DB lookup: {Ms}ms", reqId, swDb.ElapsedMilliseconds);

            var swHttp = Stopwatch.StartNew();
            var results = await http.GetAsync("https://mock-lab-api/results/" + id);
            swHttp.Stop();
            logger.LogInformation("[{ReqId}] External call: {Ms}ms", reqId, swHttp.ElapsedMilliseconds);

            if (patient is not null)
            {
                await db.Entry(patient).Collection(p => p.Prescriptions).LoadAsync();
            }

            swTotal.Stop();
            logger.LogInformation("[{ReqId}] Total: {Ms}ms", reqId, swTotal.ElapsedMilliseconds);

            return patient is null
                ? Results.NotFound()
                : Results.Ok(
                    new PatientResponse(
                        patient.Id,
                        patient.Name,
                        patient.Prescriptions.Select(p => new PrescriptionResponse(p.Id, p.Drug)).ToList()
                    )
                );
        });
    }
}
