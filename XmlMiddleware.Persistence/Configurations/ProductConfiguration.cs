using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Persistence.Configurations;

public class ProductConfiguration
    : IEntityTypeConfiguration<Product>
{
    public void Configure(
        EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.ProductSK);

        builder.Property(x => x.ProductCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.ProductCode)
            .IsUnique();
    }
}