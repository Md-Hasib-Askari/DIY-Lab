using Microsoft.EntityFrameworkCore;
using ObservabilityPart1.Models;

namespace ObservabilityPart1.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();

    /// Uncomment the following code to create an index on the Name property of the Patient entity
    /// Make sure to run the migration commands after uncommenting this code 
    /// to apply the changes to the database schema.

    // protected override void OnModelCreating(ModelBuilder modelBuilder)
    // {
    //     modelBuilder.Entity<Patient>().HasIndex(p => p.Name);
    // }
}
