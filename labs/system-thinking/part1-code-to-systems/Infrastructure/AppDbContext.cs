using SystemThinkingPart1.Domain;
using Microsoft.EntityFrameworkCore;

namespace SystemThinkingPart1.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}