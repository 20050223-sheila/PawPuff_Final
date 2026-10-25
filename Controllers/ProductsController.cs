using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data; using PawPuff.Models;
namespace PawPuff.Controllers;
public class ProductsController(ShopDbContext db) : Controller {
 public async Task<IActionResult> Index(string? search,string? petType,string? category,decimal? minPrice,decimal? maxPrice,int page=1) {
  const int pageSize=8; var q=db.Products.Where(p=>p.IsActive).AsQueryable();
  if(!string.IsNullOrWhiteSpace(search)) q=q.Where(p=>p.Name.Contains(search)||p.Description.Contains(search)||p.Category.Contains(search));
  if(!string.IsNullOrWhiteSpace(petType)) q=q.Where(p=>p.PetType==petType); if(!string.IsNullOrWhiteSpace(category)) q=q.Where(p=>p.Category==category);
  if(minPrice.HasValue) q=q.Where(p=>p.Price>=minPrice); if(maxPrice.HasValue) q=q.Where(p=>p.Price<=maxPrice);
  ViewBag.Search=search; ViewBag.PetType=petType; ViewBag.Category=category; ViewBag.MinPrice=minPrice; ViewBag.MaxPrice=maxPrice; ViewBag.Page=page; ViewBag.TotalPages=(int)Math.Ceiling(await q.CountAsync()/(double)pageSize);
  return View(await q.OrderBy(p=>p.Name).Skip((Math.Max(1,page)-1)*pageSize).Take(pageSize).ToListAsync());
 }
 public async Task<IActionResult> Details(int id){var p=await db.Products.FirstOrDefaultAsync(x=>x.Id==id&&x.IsActive);return p is null?NotFound():View(p);}
 [Authorize(Roles="Seller,Admin")] public IActionResult Create()=>View(new Product());
 [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="Seller,Admin")] public async Task<IActionResult> Create(Product p){if(!ModelState.IsValid)return View(p);db.Products.Add(p);await db.SaveChangesAsync();return RedirectToAction(nameof(Index));}
 [Authorize(Roles="Seller,Admin")] public async Task<IActionResult> Edit(int id){var p=await db.Products.FindAsync(id);return p is null?NotFound():View(p);}
 [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="Seller,Admin")] public async Task<IActionResult> Edit(int id,Product p){if(id!=p.Id)return BadRequest();if(!ModelState.IsValid)return View(p);db.Products.Update(p);await db.SaveChangesAsync();return RedirectToAction(nameof(Index));}
 [Authorize(Roles="Admin")] public async Task<IActionResult> Delete(int id){var p=await db.Products.FindAsync(id);return p is null?NotFound():View(p);}
 [HttpPost,ActionName("Delete"),ValidateAntiForgeryToken,Authorize(Roles="Admin")] public async Task<IActionResult> DeleteConfirmed(int id){var p=await db.Products.FindAsync(id);if(p!=null){p.IsActive=false;await db.SaveChangesAsync();}return RedirectToAction(nameof(Index));}
}
