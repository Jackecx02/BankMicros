using Microsoft.EntityFrameworkCore;
using Orders.Api.Models;

namespace Orders.Api.Data;

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);

            entity.OwnsMany(o => o.Items, items =>
            {
                items.WithOwner().HasForeignKey("OrderId");
                items.Property<int>("Id");
                items.HasKey("Id");
                items.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
                items.Property(i => i.Price).HasPrecision(18, 2);
            });
        });
    }
}
