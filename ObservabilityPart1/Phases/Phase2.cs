using Microsoft.EntityFrameworkCore;
using ObservabilityPart1.Data;
using ObservabilityPart1.Models;

namespace ObservabilityPart1.Phases;

public static class Phase2
{
    public static async Task<IResult> Handler(int id, AppDbContext db, HttpClient http)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Name == $"Patient{id:D6}");

        var results = await http.GetAsync("https://mock-lab-api/results/" + id);

        if (patient is not null)
        {
            await db.Entry(patient).Collection(p => p.Prescriptions).LoadAsync();
        }

        return patient is null
            ? Results.NotFound()
            : Results.Ok(
                new PatientResponse(
                    patient.Id,
                    patient.Name,
                    [.. patient.Prescriptions.Select(p => new PrescriptionResponse(p.Id, p.Drug))]
                )
            );
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/phase2/patients/{id}", Handler);
    }
}
