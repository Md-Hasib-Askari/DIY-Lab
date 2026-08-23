using Microsoft.EntityFrameworkCore;
using SystemThinkingPart5.Models;

namespace SystemThinkingPart5.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
}