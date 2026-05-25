using DZ_HealthChecks.Models;
using Microsoft.EntityFrameworkCore;

namespace DZ_HealthChecks.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
}
