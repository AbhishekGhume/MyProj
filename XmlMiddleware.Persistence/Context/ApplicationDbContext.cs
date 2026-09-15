using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}