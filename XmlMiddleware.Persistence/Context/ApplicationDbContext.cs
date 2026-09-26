using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Domain.Common;
using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Persistence.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProcessingBatch> ProcessingBatches => Set<ProcessingBatch>();

    public DbSet<OutputFile> OutputFiles => Set<OutputFile>();

    public DbSet<ProcessingEvent> ProcessingEvents => Set<ProcessingEvent>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // UpdatedDateTime was never being maintained; stamp it on every modified row.
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedDateTime = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}