using Microsoft.EntityFrameworkCore;
using SystemThinkingPart9.Domain;
using SystemThinkingPart9.Legacy;

namespace SystemThinkingPart9.Infrastructure;

// One database, two tables: Orders (the layered path) and LegacyOrders (the
// fat-controller path), so both endpoints can run side by side and be
// compared against the same running instance.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<LegacyOrder> LegacyOrders => Set<LegacyOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .Property(o => o.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<Order>()
            .Property(o => o.TotalPrice).HasPrecision(18, 2);

        modelBuilder.Entity<LegacyOrder>()
            .Property(o => o.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<LegacyOrder>()
            .Property(o => o.TotalPrice).HasPrecision(18, 2);
    }
}
