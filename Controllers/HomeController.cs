using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
namespace PawPuff.Controllers;
public class HomeController(ShopDbContext db) : Controller {
 public async Task<IActionResult> Index()=>View(await db.Products.Where(p=>p.IsActive).OrderBy(p=>p.Name).Take(8).ToListAsync());
 public IActionResult Privacy()=>View();
}
