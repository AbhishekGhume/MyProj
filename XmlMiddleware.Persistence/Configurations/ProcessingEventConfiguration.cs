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

        builder.Property(x => x.Status)
            .HasMaxLength(100);

        builder.Property(x => x.FunctionName)
            .HasMaxLength(200);

        builder.Property(x => x.Message)
            .HasMaxLength(4000);

        builder.HasOne(x => x.OutputFile)
            .WithMany()
            .HasForeignKey(x => x.OutputFileId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.CorrelationId);

        builder.HasIndex(x => x.EventType);
    }
}