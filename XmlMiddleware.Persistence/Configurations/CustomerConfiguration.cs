using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Persistence.Configurations;

public class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(
        EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(x => x.CustomerSK);

        builder.Property(x => x.CustomerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .HasMaxLength(100);

        builder.Property(x => x.Email)
            .HasMaxLength(255);

        builder.HasIndex(x => x.CustomerId)
            .IsUnique();

        builder.HasIndex(x => x.Email)
            .IsUnique();
    }
}