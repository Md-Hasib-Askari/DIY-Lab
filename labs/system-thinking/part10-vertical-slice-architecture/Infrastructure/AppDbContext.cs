using Microsoft.EntityFrameworkCore;
using SystemThinkingPart10.Features.Orders;
using SystemThinkingPart10.Layered.Domain;

namespace SystemThinkingPart10.Infrastructure;

// One database, two tables: LayeredOrders (the layered path) and
// SliceOrders (the vertical-slice path), so both can run side by side
// against the same running instance and be compared directly.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LayeredOrder> LayeredOrders => Set<LayeredOrder>();
    public DbSet<SliceOrder> SliceOrders => Set<SliceOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LayeredOrder>()
            .Property(o => o.Total).HasPrecision(18, 2);
        modelBuilder.Entity<LayeredOrder>()
            .Property(o => o.Status).HasConversion<string>();

        modelBuilder.Entity<SliceOrder>()
            .Property(o => o.Total).HasPrecision(18, 2);
        modelBuilder.Entity<SliceOrder>()
            .Property(o => o.Status).HasConversion<string>();
    }
}
