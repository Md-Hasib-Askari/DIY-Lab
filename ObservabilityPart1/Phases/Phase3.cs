using ObservabilityPart1.Data;
using ObservabilityPart1.Models;

namespace ObservabilityPart1.Phases;

public static class Phase3
{
    public static void Map(WebApplication app)
    {
        // Phase 3 changes nothing in code: it is the Phase 2 behavior, observed.
        app.MapGet("/phase3/patients/{id}", Phase2.Handler);
    }
}
