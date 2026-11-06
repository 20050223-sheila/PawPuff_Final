
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
using PawPuff.Services;
using PawPuff.Models;

namespace PawPuff.Controllers;

public class CartController(
    ShopDbContext db,
    CartService cart) : Controller
{
    public async Task<IActionResult> Index()
    {
        var lines =
            await cart.GetForCurrentUserAsync();

        return View(lines);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        int productId, int quantity = 1)
    {
        var product = await db.Products
            .FindAsync(productId);

        if (product == null || !product.IsActive)
            return NotFound();

        var lines =
            await cart.GetForCurrentUserAsync();

        var line = lines.FirstOrDefault(
            x => x.ProductId == productId);

        var desired = (long)(line?.Quantity ?? 0)
            + Math.Max(1, quantity);

        if (desired > product.Stock)
        {
            TempData["Error"] =
                "Not enough stock available.";

            return RedirectToAction(
                "Details", "Products",
                new { id = productId });
        }

        if (line == null)
        {
            lines.Add(new CartLine
            {
                ProductId = product.Id,
                Name = product.Name,
                Price = product.Price,
                Quantity = (int)desired,
                Stock = product.Stock,
                ImageUrl = product.ImageUrl
            });
        }
        else
        {
            line.Quantity = (int)desired;
            line.Stock = product.Stock;
            line.Price = product.Price;
        }

        try
        {
            await cart.SaveForCurrentUserAsync(lines);
            TempData["Message"] =
                "Added to your cart.";
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] =
                "Product availability changed. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        int productId, int quantity)
    {
        var lines =
            await cart.GetForCurrentUserAsync();

        var line = lines.FirstOrDefault(
            x => x.ProductId == productId);

        var product = await db.Products
            .FindAsync(productId);

        if (line == null)
            return RedirectToAction(nameof(Index));

        if (product == null ||
            !product.IsActive ||
            product.Stock <= 0)
        {
            TempData["Error"] =
                "This product is unavailable. Remove it from your cart.";
            return RedirectToAction(nameof(Index));
        }

        line.Quantity =
            Math.Clamp(quantity, 1, product.Stock);

        line.Stock = product.Stock;
        line.Price = product.Price;

        try
        {
            await cart.SaveForCurrentUserAsync(lines);
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] =
                "Stock changed. Please refresh your cart.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(
        int productId)
    {
        var lines =
            await cart.GetForCurrentUserAsync();

        lines.RemoveAll(
            x => x.ProductId == productId);

        await cart.SaveForCurrentUserAsync(lines);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout()
    {
        var lines =
            await cart.GetForCurrentUserAsync();

        if (lines.Count == 0)
            return RedirectToAction(nameof(Index));

        foreach (var line in lines)
        {
            var product = await db.Products
                .FindAsync(line.ProductId);

            if (product == null ||
                !product.IsActive ||
                product.Stock < line.Quantity)
            {
                TempData["Error"] =
                    $"Stock changed for {line.Name}. Please update your cart.";

                return RedirectToAction(nameof(Index));
            }
        }

        return RedirectToAction(
            "Index", "Checkout");
    }
}
