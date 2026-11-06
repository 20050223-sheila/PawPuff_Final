using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
using PawPuff.Models;

namespace PawPuff.Controllers;

public class ProductsController(ShopDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, string? petType, string? category,
        decimal? minPrice, decimal? maxPrice, int page = 1)
    {
        const int pageSize = 8;
        var q = db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.Name.Contains(search) || p.Description.Contains(search) || p.Category.Contains(search));
        if (!string.IsNullOrWhiteSpace(petType)) q = q.Where(p => p.PetType == petType);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(p => p.Category == category);
        if (minPrice.HasValue) q = q.Where(p => p.Price >= minPrice);
        if (maxPrice.HasValue) q = q.Where(p => p.Price <= maxPrice);
        var total = await q.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Search = search;
        ViewBag.PetType = petType;
        ViewBag.Category = category;
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        return View(await q.OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        return p is null ? NotFound() : View(p);
    }

    [Authorize(Roles = "Operator,Admin")]
    public IActionResult Create() => View(new Product());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Operator,Admin")]
    public async Task<IActionResult> Create(Product p)
    {
        if (!ModelState.IsValid) return View(p);
        var isOperator = User.IsInRole("Operator") && !User.IsInRole("Admin");
        p.IsNewArrival = isOperator;
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Products.Add(p);
        await db.SaveChangesAsync();
        if (isOperator)
        {
            db.ProductReviewRequests.Add(new ProductReviewRequest
            {
                ProductId = p.Id, ProductName = p.Name,
                SubmittedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ActionType = "Published", Summary = $"New product: {p.Name}; price £{p.Price:0.00}; stock {p.Stock}; visible {p.IsActive}.",
                Status = "Pending"
            });
        }
        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = p.Id, ProductName = p.Name,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            QuantityChanged = p.Stock, StockAfter = p.Stock,
            Reason = "Initial"
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Message"] = isOperator ? "Product listed; admin approval is pending." : "Product created.";
        return RedirectToAction("Index", "Operations");
    }

    [Authorize(Roles = "Operator,Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await db.Products.FindAsync(id);
        return p is null ? NotFound() : View(p);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Operator,Admin")]
    public async Task<IActionResult> Edit(int id, Product p, int originalStock)
    {
        if (id != p.Id) return BadRequest();
        if (!ModelState.IsValid) return View(p);
        await using var tx = await db.Database.BeginTransactionAsync();
        // Lock current stock while editing, so a checkout cannot be overwritten mid-purchase.
        var existing = await db.Products.FromSqlInterpolated(
            $"SELECT * FROM dbo.Products WITH (UPDLOCK, ROWLOCK) WHERE Id={id}")
            .SingleOrDefaultAsync();
        if (existing == null) return NotFound();
        if (existing.Stock != originalStock)
        {
            ModelState.AddModelError("", "Stock changed after you opened this form. Reload this product to avoid overwriting a customer purchase.");
            return View(p);
        }
        int oldStock = existing.Stock;
        decimal oldPrice = existing.Price;
        bool oldActive = existing.IsActive;
        string oldName = existing.Name;
        string oldDescription = existing.Description;
        string oldImage = existing.ImageUrl;
        string oldCategory = existing.Category;
        string oldPetType = existing.PetType;
        existing.Name = p.Name;
        existing.Category = p.Category;
        existing.PetType = p.PetType;
        existing.Description = p.Description;
        existing.Price = p.Price;
        existing.Stock = p.Stock;
        existing.ImageUrl = p.ImageUrl;
        existing.IsActive = p.IsActive;
        // Preserve IsNewArrival, which is not edited on this form.

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (oldStock != p.Stock)
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = existing.Id, ProductName = p.Name,
                UserId = actorId, QuantityChanged = p.Stock - oldStock,
                StockAfter = p.Stock, Reason = p.Stock > oldStock ? "Restock" : "Adjustment"
            });
        }
        var isOperator = User.IsInRole("Operator") && !User.IsInRole("Admin");
        bool changed = oldName != p.Name || oldCategory != p.Category || oldPetType != p.PetType ||
            oldDescription != p.Description || oldPrice != p.Price || oldStock != p.Stock ||
            oldImage != p.ImageUrl || oldActive != p.IsActive;
        if (isOperator && changed)
        {
            string action = oldActive != p.IsActive ? (p.IsActive ? "Listed" : "Unlisted") : "Updated";
            db.ProductReviewRequests.Add(new ProductReviewRequest
            {
                ProductId = existing.Id, ProductName = p.Name, SubmittedByUserId = actorId,
                ActionType = action,
                Summary = $"{oldName} → {p.Name}; price £{oldPrice:0.00} → £{p.Price:0.00}; stock {oldStock} → {p.Stock}; visible {oldActive} → {p.IsActive}.",
                Status = "Pending"
            });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Message"] = isOperator && changed ? "Changes saved; admin review is pending." : "Product updated.";
        return RedirectToAction("Index", "Operations");
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Products.FindAsync(id);
        return p is null ? NotFound() : View(p);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var p = await db.Products.FindAsync(id);
        if (p != null)
        {
            p.IsActive = false; // Soft delete; order records stay intact.
            await db.SaveChangesAsync();
        }
        TempData["Message"] = "Product taken off the shop; order history retained.";
        return RedirectToAction("Index", "Operations");
    }
}
