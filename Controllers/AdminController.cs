using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
using PawPuff.Models;
using PawPuff.Services;

namespace PawPuff.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ShopDbContext db;
    private readonly UserManager<ApplicationUser> users;

    public AdminController(ShopDbContext db, UserManager<ApplicationUser> users)
    {
        this.db = db;
        this.users = users;
    }

    public async Task<IActionResult> Index(int ordersPage = 1)
    {
        const int ordersPageSize = 40;
        int allOrders = await db.Orders.CountAsync();
        int totalOrderPages = Math.Max(1, (int)Math.Ceiling(allOrders / (double)ordersPageSize));
        ordersPage = Math.Clamp(ordersPage, 1, totalOrderPages);
        string[] saleStatuses = ["DemoPaid", "Paid (Demo)", "Completed", "Shipped"];
        var paidOrders = db.Orders.AsNoTracking().Where(o => saleStatuses.Contains(o.Status));
        var soldLines = db.OrderItems.AsNoTracking().Where(i =>
            db.Orders.Any(o => o.Id == i.OrderId && saleStatuses.Contains(o.Status)));
        var operatorUsers = await users.GetUsersInRoleAsync("Operator");
        var now = DateTimeOffset.UtcNow;
        var model = new AdminDashboardViewModel
        {
            OrdersPage = ordersPage,
            TotalOrderPages = totalOrderPages,
            AllOrderCount = allOrders,
            ActiveProductCount = await db.Products.CountAsync(p => p.IsActive),
            PaidOrderCount = await paidOrders.CountAsync(),
            UserCount = await db.Users.CountAsync(),
            SoldUnits = await soldLines.SumAsync(i => (int?)i.Quantity) ?? 0,
            DemoSalesTotal = await paidOrders.SumAsync(o => (decimal?)o.Total) ?? 0m,
            PendingReviewCount = await db.ProductReviewRequests.CountAsync(r => r.Status == "Pending"),
            LowStockCount = await db.Products.CountAsync(p => p.IsActive && p.Stock <= InventoryRules.LowStockThreshold),
            RecentOrders = await db.Orders.AsNoTracking().Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt).Skip((ordersPage - 1) * ordersPageSize)
                .Take(ordersPageSize).ToListAsync(),
            PendingReviews = await db.ProductReviewRequests.AsNoTracking()
                .Where(r => r.Status == "Pending").OrderByDescending(r => r.SubmittedAt).ToListAsync(),
            ReviewHistory = await db.ProductReviewRequests.AsNoTracking()
                .Where(r => r.Status != "Pending").OrderByDescending(r => r.ReviewedAt)
                .Take(15).ToListAsync(),
            TopSellingProducts = await soldLines
                .GroupBy(i => new { i.ProductId, i.ProductName })
                .Select(g => new TopSellingProductRow
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    Quantity = g.Sum(i => i.Quantity),
                    DemoRevenue = g.Sum(i => i.LineTotal)
                }).OrderByDescending(x => x.Quantity).Take(10).ToListAsync(),
            OperatorAccounts = operatorUsers.Select(u => new OperatorAccountRow
            {
                Id = u.Id,
                Email = u.Email ?? "",
                UserName = u.UserName ?? "",
                IsDisabled = u.LockoutEnabled && u.LockoutEnd.HasValue && u.LockoutEnd.Value > now
            }).OrderBy(o => o.UserName).ToList()
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PendingReviewCount()
    {
        return Json(new { count = await db.ProductReviewRequests.CountAsync(x => x.Status == "Pending") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewProduct(int id, string decision)
    {
        if (decision is not ("Approve" or "Reject")) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Prevent a second admin request from reviewing the same pending item twice.
        var review = await db.ProductReviewRequests
            .FromSqlInterpolated($"SELECT * FROM dbo.ProductReviewRequests WITH (UPDLOCK, ROWLOCK) WHERE Id={id}")
            .SingleOrDefaultAsync();
        if (review == null) return NotFound();
        if (review.Status != "Pending")
        {
            TempData["Message"] = "This item has already been reviewed.";
            return RedirectToAction(nameof(Index));
        }
        review.Status = decision == "Approve" ? "Approved" : "Rejected";
        review.ReviewedAt = DateTime.UtcNow;
        review.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (decision == "Reject")
        {
            // A rejected already-live product is removed from customer-facing pages.
            var product = await db.Products.FindAsync(review.ProductId);
            if (product != null) product.IsActive = false;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Message"] = decision == "Approve" ? "Review approved." : "Review rejected; product removed from the shop.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult CreateOperator() => View(new CreateOperatorViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOperator(CreateOperatorViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (await users.FindByEmailAsync(model.Email.Trim()) != null ||
            await users.FindByNameAsync(model.UserName.Trim()) != null)
        {
            ModelState.AddModelError("", "Email or username is already taken.");
            return View(model);
        }
        var user = new ApplicationUser
        {
            Email = model.Email.Trim(), UserName = model.UserName.Trim(),
            DisplayName = "PawPuff Operations", LockoutEnabled = true
        };
        var result = await users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }
        var roleResult = await users.AddToRoleAsync(user, "Operator");
        if (!roleResult.Succeeded)
        {
            // Never leave a newly created account with incomplete operator setup.
            await users.DeleteAsync(user);
            foreach (var error in roleResult.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }
        TempData["Message"] = "Operator account created. Share credentials privately.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOperatorEnabled(string id, bool enabled)
    {
        var user = await users.FindByIdAsync(id);
        if (user == null || !await users.IsInRoleAsync(user, "Operator") ||
            await users.IsInRoleAsync(user, "Admin")) return NotFound();
        var result = await users.SetLockoutEnabledAsync(user, true);
        if (result.Succeeded)
            result = await users.SetLockoutEndDateAsync(user, enabled ? null : DateTimeOffset.MaxValue);
        if (result.Succeeded)
            result = await users.UpdateSecurityStampAsync(user);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? (enabled ? "Operator enabled." : "Operator disabled.")
            : string.Join("; ", result.Errors.Select(x => x.Description));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        string[] allowed = ["Pending", "Shipped", "Completed", "Cancelled", "DemoPaid"];
        if (!allowed.Contains(status)) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id={id}")
            .Include(o => o.Items).SingleOrDefaultAsync();
        if (order == null) return NotFound();
        if (order.Status == "Cancelled" && status != "Cancelled")
        {
            TempData["Error"] = "A cancelled order cannot be reactivated. Create a new order instead.";
            return RedirectToAction(nameof(Index));
        }
        if (status == "Cancelled" && order.Status != "Cancelled")
        {
            // Restore stock ONCE when cancelling a previously placed order.
            foreach (var item in order.Items)
            {
                int affected = await db.Products.Where(p => p.Id == item.ProductId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Stock, p => p.Stock + item.Quantity));
                if (affected == 1)
                {
                    int after = await db.Products.AsNoTracking()
                        .Where(p => p.Id == item.ProductId).Select(p => p.Stock).SingleAsync();
                    db.InventoryMovements.Add(new InventoryMovement
                    {
                        ProductId = item.ProductId, ProductName = item.ProductName,
                        OrderId = order.Id, UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                        QuantityChanged = item.Quantity, StockAfter = after, Reason = "Cancellation"
                    });
                }
            }
        }
        order.Status = status;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Message"] = "Order status updated.";
        return RedirectToAction(nameof(Index));
    }
}
