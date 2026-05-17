using Application.Shared;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Unit.Tests.RepositoryTests.Entities.EmployeeDb;

namespace Unit.Tests.RepositoryTests.Base;

public class EmployeeTestDbContext : DbBaseContext
{
    public EmployeeTestDbContext(
        DbContextOptions<EmployeeTestDbContext> options,
        ICurrentTenant? currentTenant = null,
        string schema = "public"
    )
        : base(options, schema) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
