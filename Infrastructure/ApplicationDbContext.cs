using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure;

public class ApplicationDbContext : DbBaseContext
{
    public DbSet<TradingTrade> TradingTrades => Set<TradingTrade>();

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        string schema = "public"
    )
        : base(options, schema) { }

    protected ApplicationDbContext(DbContextOptions options)
        : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var idProperty = entityType.FindProperty("Id");
            if (idProperty is null || idProperty.ClrType != typeof(long))
                continue;
            idProperty.SetColumnType("bigint");
            idProperty.ValueGenerated = ValueGenerated.OnAdd;
            idProperty.SetBeforeSaveBehavior(PropertySaveBehavior.Throw);

            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetDefaultValueSql("now() AT TIME ZONE 'UTC'");
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
