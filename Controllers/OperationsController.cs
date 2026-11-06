using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
using PawPuff.Models;
using PawPuff.Services;

namespace PawPuff.Controllers;

[Authorize(Roles = "Operator,Admin")]
public class OperationsController : Controller
{
    private readonly ShopDbContext db;
    private readonly IWebHostEnvironment environment;

    public OperationsController(ShopDbContext db, IWebHostEnvironment environment)
    {
        this.db = db;
        this.environment = environment;
    }

    public async Task<IActionResult> Index(string? search, string? petType, string? category,
        bool lowStockOnly = false, int page = 1)
    {
        const int pageSize = 25;
        var allProducts = db.Products.AsNoTracking();
        var products = allProducts;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            products = products.Where(p => p.Name.Contains(term) ||
                p.Category.Contains(term) || p.PetType.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(petType)) products = products.Where(p => p.PetType == petType);
        if (!string.IsNullOrWhiteSpace(category)) products = products.Where(p => p.Category == category);
        if (lowStockOnly) products = products.Where(p => p.IsActive && p.Stock <= InventoryRules.LowStockThreshold);

        var count = await products.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var model = new OperationsDashboardViewModel
        {
            Search = search, PetType = petType, Category = category, LowStockOnly = lowStockOnly,
            Page = page,
            TotalPages = totalPages,
            ProductCount = await allProducts.CountAsync(),
            LowStockCount = await allProducts.CountAsync(p => p.IsActive && p.Stock <= InventoryRules.LowStockThreshold),
            OrderCount = await db.Orders.CountAsync(),
            CustomerCount = await db.Users.CountAsync(),
            Products = await products.OrderByDescending(p => p.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            LowStockProducts = await allProducts.Where(p => p.IsActive && p.Stock <= InventoryRules.LowStockThreshold)
                .OrderBy(p => p.Stock).ThenBy(p => p.Name).Take(12).ToListAsync(),
            RecentInventoryMovements = await db.InventoryMovements.AsNoTracking()
                .OrderByDescending(m => m.Id).Take(20).ToListAsync(),
            RecentOrders = await db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt)
                .Take(8).ToListAsync()
        };
        return View(model);
    }

    // Near-real-time stock figures without reloading the whole dashboard.
    [HttpGet]
    public async Task<IActionResult> InventorySnapshot([FromQuery] int[] ids)
    {
        var safeIds = ids.Distinct().Take(60).ToArray();
        var items = await db.Products.AsNoTracking()
            .Where(p => safeIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Stock, p.IsActive })
            .ToListAsync();
        var lowStockCount = await db.Products.CountAsync(p =>
            p.IsActive && p.Stock <= InventoryRules.LowStockThreshold);
        var newestMovementId = await db.InventoryMovements.Select(x => (int?)x.Id).MaxAsync() ?? 0;
        return Json(new { lowStockCount, newestMovementId, items });
    }

    [HttpGet]
    public IActionResult Create() => View(new OperationsNewProductViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create(OperationsNewProductViewModel model)
    {
        if (model.PetType != "Dog" && model.PetType != "Cat")
            ModelState.AddModelError(nameof(model.PetType), "Please choose Dog or Cat.");

        var imageFile = model.ImageFile;
        bool hasUpload = imageFile is { Length: > 0 };
        bool hasUrl = !string.IsNullOrWhiteSpace(model.ImageUrl);
        if (!hasUpload && !hasUrl)
            ModelState.AddModelError(nameof(model.ImageFile), "Upload a product image or provide an image URL.");

        if (hasUrl && !hasUpload &&
            (!Uri.TryCreate(model.ImageUrl, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
            ModelState.AddModelError(nameof(model.ImageUrl), "Provide a valid HTTP or HTTPS image URL.");

        if (hasUpload && imageFile != null)
        {
            if (imageFile.Length > 5 * 1024 * 1024)
                ModelState.AddModelError(nameof(model.ImageFile), "Image must be 5 MB or smaller.");
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"))
                ModelState.AddModelError(nameof(model.ImageFile), "Use JPEG, PNG, WEBP or GIF.");
        }

        if (!ModelState.IsValid)
            return View(model);

        string? newImagePath = null;
        try
        {
            string imageUrl;
            if (hasUpload && imageFile != null)
            {
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                await using var stream = imageFile.OpenReadStream();
                var signature = new byte[12];
                var bytesRead = await stream.ReadAsync(signature.AsMemory(0, 12));
                if (!LooksLikeImage(signature, bytesRead, extension))
                {
                    ModelState.AddModelError(nameof(model.ImageFile), "The selected file is not a supported image.");
                    return View(model);
                }

                // Create a random file name: never use user-supplied file names as paths.
                var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
                var imagesFolder = Path.Combine(webRoot, "uploads", "products");
                Directory.CreateDirectory(imagesFolder);
                string fileName = $"{Guid.NewGuid():N}{extension}";
                newImagePath = Path.Combine(imagesFolder, fileName);
                await using (var output = System.IO.File.Create(newImagePath))
                {
                    // Signature bytes were read above; continue from the same stream.
                    await output.WriteAsync(signature.AsMemory(0, bytesRead));
                    await stream.CopyToAsync(output);
                }
                imageUrl = $"/uploads/products/{fileName}";
            }
            else
            {
                imageUrl = model.ImageUrl!.Trim();
            }

            var product = new Product
            {
                Name = model.Name.Trim(),
                Category = model.Category.Trim(),
                PetType = model.PetType,
                Description = model.Description.Trim(),
                Price = model.Price,
                Stock = model.Stock,
                ImageUrl = imageUrl,
                IsActive = true,
                IsNewArrival = true
            };

            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin"))
            {
                db.ProductReviewRequests.Add(new ProductReviewRequest
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    SubmittedByUserId = actorId,
                    ActionType = "Published",
                    Summary = $"New product: {product.Name}; price £{product.Price:0.00}; stock {product.Stock}; visible immediately.",
                    Status = "Pending"
                });
            }
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id, ProductName = product.Name,
                UserId = actorId, QuantityChanged = product.Stock, StockAfter = product.Stock,
                Reason = "Initial"
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Message"] = User.IsInRole("Admin")
                ? "Product published by admin."
                : "New product is live; an approval request was sent to the admin dashboard.";
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            if (newImagePath != null && System.IO.File.Exists(newImagePath))
                System.IO.File.Delete(newImagePath);
            throw;
        }
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetListing(int id, bool active)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return NotFound();

        if (product.IsActive == active)
        {
            TempData["Message"] = active ? "Product is already live." : "Product is already hidden.";
            return RedirectToAction(nameof(Index));
        }

        bool oldActive = product.IsActive;
        product.IsActive = active;

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOperator = User.IsInRole("Operator") && !User.IsInRole("Admin");
        if (isOperator)
        {
            db.ProductReviewRequests.Add(new ProductReviewRequest
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SubmittedByUserId = actorId,
                ActionType = active ? "Relisted" : "Unlisted",
                Summary = active
                    ? $"Operator relisted product: {product.Name}; visible {oldActive} → {product.IsActive}."
                    : $"Operator unpublished product: {product.Name}; visible {oldActive} → {product.IsActive}.",
                Status = "Pending"
            });
        }

        await db.SaveChangesAsync();
        TempData["Message"] = isOperator
            ? (active ? "Product relisted; admin review notice sent." : "Product unpublished; admin review notice sent.")
            : (active ? "Product is live again." : "Product unpublished.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProduct(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return NotFound();

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOperator = User.IsInRole("Operator") && !User.IsInRole("Admin");

        product.IsActive = false;
        product.IsNewArrival = false;

        if (isOperator)
        {
            db.ProductReviewRequests.Add(new ProductReviewRequest
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SubmittedByUserId = actorId,
                ActionType = "Deleted",
                Summary = $"Operator removed product from sale: {product.Name}. This is a safe soft delete; the product stays in the database and order history is retained.",
                Status = "Pending"
            });
        }

        await db.SaveChangesAsync();
        TempData["Message"] = isOperator
            ? "Product removed from sale; admin review notice sent."
            : "Product removed from sale.";
        return RedirectToAction(nameof(Index));
    }

    private static bool LooksLikeImage(byte[] bytes, int count, string extension)
    {
        if (extension is ".jpg" or ".jpeg")
            return count >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;
        if (extension == ".png")
            return count >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        if (extension == ".webp")
            return count >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
                System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
        if (extension == ".gif")
            return count >= 6 && (System.Text.Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a");
        return false;
    }
}
