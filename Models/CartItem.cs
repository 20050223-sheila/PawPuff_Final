
using System;
using System.ComponentModel.DataAnnotations;

namespace PawPuff.Models;

public class CartItem
{
    // 购物车记录的唯一编号（主键）
    public int Id { get; set; }

    // 购物车属于哪个用户
    [Required]
    public string UserId { get; set; } = "";

    // 购物车中是哪件商品
    public int ProductId { get; set; }

    // 购买数量，至少为 1
    [Range(1, 100000)]
    public int Quantity { get; set; } = 1;

    // 加入购物车的时间（UTC）
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 最后修改的时间（UTC）
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 关联的用户
    public ApplicationUser User { get; set; } = null!;

    // 关联的商品
    public Product Product { get; set; } = null!;
}
