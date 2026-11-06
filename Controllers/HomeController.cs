using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;

namespace PawPuff.Controllers;

public class HomeController(ShopDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.NewArrivals = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.IsNewArrival)
            .OrderByDescending(p => p.Id).Take(4).ToListAsync();

        var featured = await db.Products.AsNoTracking()
            .Where(p => p.IsActive).OrderBy(p => p.Name).Take(8).ToListAsync();
        return View(featured);
    }

    public IActionResult Privacy() => View();
}
