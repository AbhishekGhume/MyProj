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

        builder.Property(x => x.OriginalBlobPath)
            .HasMaxLength(1000)
            .IsRequired();

        // SHA-256 hex = 64 chars. Nullable: not known for files rejected before hashing.
        builder.Property(x => x.FileHash)
            .HasMaxLength(64);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CurrentStep)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DuplicateCase)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.RecordCount)
            .IsRequired();

        builder.Property(x => x.InvalidRecordCount)
            .IsRequired();

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        // One correlation id per delivered message.
        builder.HasIndex(x => x.CorrelationId)
            .IsUnique();

        // NOT unique any more: every duplicate arrival gets its own row.
        builder.HasIndex(x => x.FileHash);

        builder.HasIndex(x => x.FileName);

        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.OutputFiles)
            .WithOne(x => x.ProcessingBatch)
            .HasForeignKey(x => x.BatchId);

        builder.HasMany(x => x.ProcessingEvents)
            .WithOne(x => x.ProcessingBatch)
            .HasForeignKey(x => x.BatchId);

        // Self reference: duplicate / reprocessed batch -> the earlier batch it matched.
        builder.HasOne(x => x.OriginalBatch)
            .WithMany()
            .HasForeignKey(x => x.OriginalBatchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}