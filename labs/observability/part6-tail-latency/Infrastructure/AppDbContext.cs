using ObservabilityPart6.Domain;
using Microsoft.EntityFrameworkCore;

namespace ObservabilityPart6.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
}

