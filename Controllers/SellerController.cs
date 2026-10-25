using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;using PawPuff.Data;
namespace PawPuff.Controllers;
[Authorize(Roles="Seller,Admin")] public class SellerController(ShopDbContext db):Controller {public async Task<IActionResult> Index()=>View(await db.Products.OrderBy(p=>p.Name).ToListAsync());}
