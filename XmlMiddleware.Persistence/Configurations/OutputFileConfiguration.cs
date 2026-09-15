using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Persistence.Configurations;

public class OutputFileConfiguration
    : IEntityTypeConfiguration<OutputFile>
{
    public void Configure(
        EntityTypeBuilder<OutputFile> builder)
    {
        builder.HasKey(x => x.OutputFileId);

        builder.Property(x => x.OutputBlobPath)
            .HasMaxLength(1000);

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(x => new
        {
            x.BatchId,
            x.OutputType
        })
        .IsUnique();
    }
}