using System.Reflection;
using Application.Shared;
using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Interceptors;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure;

public class ApplicationDbContext : DbBaseContext
{
    private readonly ICurrentTenant _currentTenant;

    public DbSet<ExceptionLog> ExceptionLogs => Set<ExceptionLog>();

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenant currentTenant,
        string schema = "public"
    )
        : base(options, schema)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Design-time / migrations constructor. Tenant context defaults to a
    /// no-op so model building succeeds without a request scope; the global
    /// query filter falls back to "TenantId IS NULL" (global rows only).
    /// </summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : this(options, new CurrentTenant()) { }

    protected ApplicationDbContext(DbContextOptions options)
        : base(options)
    {
        _currentTenant = new CurrentTenant();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
        optionsBuilder.AddInterceptors(new TenantStampingInterceptor(_currentTenant));
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var idProperty = entityType.FindProperty("Id");
            if (idProperty is not null && idProperty.ClrType == typeof(long))
            {
                idProperty.SetColumnType("bigint");
                idProperty.ValueGenerated = ValueGenerated.OnAdd;
                idProperty.SetBeforeSaveBehavior(PropertySaveBehavior.Throw);
            }

            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetDefaultValueSql("now() AT TIME ZONE 'UTC'");
                }
            }

            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    private static readonly MethodInfo ApplyTenantFilterMethod = typeof(ApplicationDbContext)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        // Compiler emits the closure on _currentTenant; EF Core parameterizes
        // _currentTenant.TenantId at query time so model caching stays sound.
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            e.TenantId == null || e.TenantId == _currentTenant.TenantId
        );
    }
}
