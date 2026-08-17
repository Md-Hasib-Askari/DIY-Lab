using Microsoft.EntityFrameworkCore;
using SystemThinkingPart2.Models;

namespace SystemThinkingPart2.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Index the foreign key so the per-order lookup and the fix's JOIN
        // both use it instead of scanning OrderItems.
        modelBuilder.Entity<OrderItem>()
            .HasIndex(i => i.OrderId);
    }
}
