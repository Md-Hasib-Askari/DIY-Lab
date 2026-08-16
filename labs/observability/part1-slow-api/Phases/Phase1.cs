using Microsoft.EntityFrameworkCore;
using ObservabilityPart1.Data;
using ObservabilityPart1.Models;

namespace ObservabilityPart1.Phases;

public static class Phase1
{
    public static void Map(WebApplication app)
    {
        app.MapGet(
            "/phase1/patients/{id}",
            async (int id, AppDbContext db) =>
            {
                var patient = await db
                    .Patients.Include(p => p.Prescriptions)
                    .FirstOrDefaultAsync(p => p.Id == id);

                return patient is null
                    ? Results.NotFound()
                    : Results.Ok(
                        new PatientResponse(
                            patient.Id,
                            patient.Name,
                            [
                                .. patient.Prescriptions.Select(p => new PrescriptionResponse(
                                    p.Id,
                                    p.Drug
                                )),
                            ]
                        )
                    );
            }
        );
    }
}
