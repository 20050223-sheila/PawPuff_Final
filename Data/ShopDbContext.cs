
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PawPuff.Models;

namespace PawPuff.Data;

public class ShopDbContext : IdentityDbContext<ApplicationUser>
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options)
        : base(options)
    {
    }

    // 已有的商品表
    public DbSet<Product> Products => Set<Product>();

    // 已有的订单表
    public DbSet<Order> Orders => Set<Order>();

    // 已有的订单明细表
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // 新增：购物车表
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // 保留 ASP.NET Core Identity 的用户和角色配置
        base.OnModelCreating(builder);

        // 商品价格保留两位小数
        builder.Entity<Product>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        // 订单金额保留两位小数
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

        // 订单明细金额保留两位小数
        builder.Entity<OrderItem>()
            .Property(i => i.LineTotal)
            .HasPrecision(18, 2);

        builder.Entity<OrderItem>()
            .Property(i => i.UnitPrice)
            .HasPrecision(18, 2);

        // 新增：配置购物车表
        builder.Entity<CartItem>(entity =>
        {
            // 一位用户的一种商品只能有一条购物车记录
            entity.HasIndex(c => new { c.UserId, c.ProductId })
                .IsUnique();

            // 购物车与用户建立关联
            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 购物车与商品建立关联
            entity.HasOne(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
