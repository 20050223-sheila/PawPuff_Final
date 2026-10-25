using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PawPuff.Models;
namespace PawPuff.Data;
public class ShopDbContext : IdentityDbContext<ApplicationUser> {
 public ShopDbContext(DbContextOptions<ShopDbContext> options):base(options){}
 public DbSet<Product> Products => Set<Product>(); public DbSet<Order> Orders => Set<Order>(); public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(o => o.Discount)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(o => o.Shipping)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(o => o.Subtotal)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(o => o.Total)
            .HasPrecision(18, 2);

        builder.Entity<OrderItem>()
            .Property(i => i.LineTotal)
            .HasPrecision(18, 2);

        builder.Entity<OrderItem>()
            .Property(i => i.UnitPrice)
            .HasPrecision(18, 2);
    }

}
