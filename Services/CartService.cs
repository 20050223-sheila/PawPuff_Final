
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
using PawPuff.Models;

namespace PawPuff.Services;

public class CartService(
    IHttpContextAccessor accessor,
    ShopDbContext db)
{
    private const string Key = "PAWSHOP_CART";

    private ISession Session =>
        accessor.HttpContext!.Session;

    public List<CartLine> Get() =>
        JsonSerializer.Deserialize<List<CartLine>>(
            Session.GetString(Key) ?? "[]"
        ) ?? new();

    public void Save(List<CartLine> lines) =>
        Session.SetString(
            Key, JsonSerializer.Serialize(lines));

    public void Clear() => Session.Remove(Key);

    private string? CurrentUserId()
    {
        var user = accessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirstValue(
            ClaimTypes.NameIdentifier);
    }

    // 登录后，将旧 Session 购物车合并到数据库
    public async Task MergeSessionCartAsync()
    {
        var userId = CurrentUserId();

        if (userId == null)
            return;

        var sessionLines = Get();

        if (sessionLines.Count == 0)
            return;

        var existing = await db.CartItems
            .Where(x => x.UserId == userId)
            .ToListAsync();

        foreach (var line in sessionLines
            .Where(x => x.Quantity > 0)
            .GroupBy(x => x.ProductId))
        {
            var product = await db.Products
                .FindAsync(line.Key);

            if (product == null ||
                !product.IsActive ||
                product.Stock <= 0)
                continue;

            var item = existing.FirstOrDefault(
                x => x.ProductId == line.Key);

            var oldQuantity = item?.Quantity ?? 0;
            var requested = line.Sum(x => x.Quantity);

            var quantity = (int)Math.Min(
                (long)product.Stock,
                (long)oldQuantity + requested);

            if (item == null)
            {
                db.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    ProductId = product.Id,
                    Quantity = quantity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                item.Quantity = quantity;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();

        // 只有数据库保存成功后才清空旧购物车
        Clear();
    }

    public async Task<List<CartLine>>
        GetForCurrentUserAsync()
    {
        var userId = CurrentUserId();

        if (userId == null)
            return Get();

        await MergeSessionCartAsync();

        var items = await db.CartItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.UserId == userId)
            .ToListAsync();

        return items.Select(x => new CartLine
        {
            ProductId = x.ProductId,
            Name = x.Product.Name,
            Price = x.Product.Price,
            Quantity = x.Quantity,
            Stock = x.Product.Stock,
            ImageUrl = x.Product.ImageUrl
        }).ToList();
    }

    public async Task SaveForCurrentUserAsync(
        List<CartLine> lines)
    {
        var userId = CurrentUserId();

        if (userId == null)
        {
            Save(lines);
            return;
        }

        // 先合并尚未迁移的 Session 商品
        await MergeSessionCartAsync();

        var requested = lines
            .Where(x => x.Quantity > 0)
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        var existing = await db.CartItems
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var ids = requested.Select(x => x.ProductId)
            .ToList();

        var products = await db.Products
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        foreach (var item in requested)
        {
            if (!products.TryGetValue(
                    item.ProductId, out var product)
                || !product.IsActive
                || item.Quantity > product.Stock)
            {
                throw new InvalidOperationException(
                    "Cart contains unavailable items.");
            }
        }

        db.CartItems.RemoveRange(
            existing.Where(x =>
                !ids.Contains(x.ProductId)));

        foreach (var item in requested)
        {
            var row = existing.FirstOrDefault(
                x => x.ProductId == item.ProductId);

            if (row == null)
            {
                db.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                row.Quantity = item.Quantity;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task ClearForCurrentUserAsync()
    {
        var userId = CurrentUserId();

        if (userId != null)
        {
            var items = await db.CartItems
                .Where(x => x.UserId == userId)
                .ToListAsync();

            db.CartItems.RemoveRange(items);
            await db.SaveChangesAsync();
        }

        Clear();
    }
}
