using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Persistence.Configurations;

public class OrderDetailConfiguration
    : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(
        EntityTypeBuilder<OrderDetail> builder)
    {
        builder.HasKey(x => x.OrderDetailSK);

        builder.Property(x => x.UnitPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.LineTotal)
            .HasColumnType("decimal(18,2)")
            .HasComputedColumnSql(
            "CONVERT(decimal(18,2),[Quantity]) * [UnitPrice]",
            true);

        builder.HasOne(x => x.Order)
            .WithMany(x => x.OrderDetails)
            .HasForeignKey(x => x.OrderSK);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.OrderDetails)
            .HasForeignKey(x => x.ProductSK);
    }
}