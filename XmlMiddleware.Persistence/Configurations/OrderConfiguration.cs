using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Persistence.Configurations;

public class OrderConfiguration
    : IEntityTypeConfiguration<Order>
{
    public void Configure(
        EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.OrderSK);

        builder.Property(x => x.OrderId)
            .HasMaxLength(100)
            .IsRequired();

        // Database-level guard behind the application's duplicate-order check
        // (two files racing with the same OrderId can no longer both succeed).
        builder.HasIndex(x => x.OrderId)
            .IsUnique();

        builder.HasOne(x => x.Batch)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.BatchId);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerSK);
    }
}