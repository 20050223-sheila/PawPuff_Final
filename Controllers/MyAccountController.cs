using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
using PawPuff.Models;

namespace PawPuff.Controllers;

[Authorize]
public class MyAccountController : Controller
{
    private readonly ShopDbContext db;
    private readonly UserManager<ApplicationUser> users;

    public MyAccountController(ShopDbContext db, UserManager<ApplicationUser> users)
    {
        this.db = db;
        this.users = users;
    }

    private static bool IsPaid(Order order) =>
        order.Status is "DemoPaid" or "Paid (Demo)" or "Shipped";

    private static List<string> UniqueAddresses(IEnumerable<Order> orders) =>
        orders
            .Select(o => o.ShippingAddress?.Trim() ?? "")
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(new MyAccountOverviewViewModel
        {
            UserId = user.Id,
            UserName = string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.UserName ?? "Customer" : user.DisplayName,
            Email = user.Email ?? "",
            PaidCount = orders.Count(IsPaid),
            CompletedCount = orders.Count(o => o.Status == "Completed"),
            RecentOrders = orders.Take(5).ToList(),
            SavedAddresses = UniqueAddresses(orders).Take(5).ToList()
        });
    }

    // Paid orders and completed orders are separate filters.
    public async Task<IActionResult> Orders(string status = "all")
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        status = status?.ToLowerInvariant() ?? "all";
        if (status == "paid") orders = orders.Where(IsPaid).ToList();
        else if (status == "completed")
            orders = orders.Where(o => o.Status == "Completed").ToList();
        else status = "all";

        return View(new MyOrdersViewModel
        {
            SelectedStatus = status,
            Orders = orders
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();

        // User ownership is checked in SQL, not just in the view.
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

        if (order == null) return NotFound();
        return View(order);
    }

    public async Task<IActionResult> Addresses()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(UniqueAddresses(orders));
    }
}
