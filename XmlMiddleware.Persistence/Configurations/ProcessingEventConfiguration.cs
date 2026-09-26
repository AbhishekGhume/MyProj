using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Persistence.Configurations;

public class ProcessingEventConfiguration
    : IEntityTypeConfiguration<ProcessingEvent>
{
    public void Configure(
        EntityTypeBuilder<ProcessingEvent> builder)
    {
        builder.HasKey(x => x.EventId);

        builder.Property(x => x.FileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(100);

        builder.Property(x => x.FunctionName)
            .HasMaxLength(200);

        builder.Property(x => x.Message)
            .HasMaxLength(4000);

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(x => x.FromState)
            .HasMaxLength(100);

        builder.Property(x => x.ToState)
            .HasMaxLength(100);

        builder.HasOne(x => x.OutputFile)
            .WithMany()
            .HasForeignKey(x => x.OutputFileId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ProcessingBatch)
            .WithMany(x => x.ProcessingEvents)
            .HasForeignKey(x => x.BatchId);

        // Search by any of the three identifiers.
        builder.HasIndex(x => x.CorrelationId);

        builder.HasIndex(x => x.FileName);

        builder.HasIndex(x => new { x.BatchId, x.CreatedDateTime });

        builder.HasIndex(x => x.EventType);
    }
}