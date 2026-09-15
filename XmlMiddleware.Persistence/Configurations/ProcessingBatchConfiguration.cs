using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Persistence.Configurations;

public class ProcessingBatchConfiguration
    : IEntityTypeConfiguration<ProcessingBatch>
{
    public void Configure(
        EntityTypeBuilder<ProcessingBatch> builder)
    {
        builder.HasKey(x => x.BatchId);

        builder.Property(x => x.CorrelationId)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.BlobPath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.FileHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.RecordCount)
            .IsRequired();

        builder.Property(x => x.LeaseOwner)
            .HasMaxLength(200);

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.CorrelationId)
            .IsUnique();

        builder.HasIndex(x => x.FileHash).IsUnique();

        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.OutputFiles)
            .WithOne(x => x.ProcessingBatch)
            .HasForeignKey(x => x.BatchId);

        builder.HasMany(x => x.ProcessingEvents)
            .WithOne(x => x.ProcessingBatch)
            .HasForeignKey(x => x.BatchId);
    }
}