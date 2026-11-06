
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
using PawPuff.Models;
using PawPuff.Services;

namespace PawPuff.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly ShopDbContext db;
        private readonly CartService cart;

        public CheckoutController(
            ShopDbContext db,
            CartService cart)
        {
            this.db = db;
            this.cart = cart;
        }

        private async Task<List<CartLine>> GetLinesAsync()
        {
            return await cart.GetForCurrentUserAsync();
        }

        private void SetTotals(List<CartLine> lines)
        {
            decimal subtotal = lines.Sum(x => x.Subtotal);

            ViewBag.Lines = lines;
            ViewBag.Subtotal = subtotal;
            ViewBag.Shipping = subtotal >= 50m ? 0m : 3.99m;
            ViewBag.Discount = 0m;
            ViewBag.StripeConfigured = false;
        }

        // Display checkout page
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var lines = await GetLinesAsync();

            if (lines.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            SetTotals(lines);

            return View(new CheckoutViewModel
            {
                PaymentMethod = "Card"
            });
        }

        // All checkout submissions use demo payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Index(
            CheckoutViewModel model)
        {
            return DemoPay(model);
        }

        // Demo payment: no real money is charged
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DemoPay(
            CheckoutViewModel model)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var lines = await GetLinesAsync();

            if (lines.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            // Validate selected demo payment method
            if (model.PaymentMethod != "Card" &&
                model.PaymentMethod != "WeChat Pay" &&
                model.PaymentMethod != "Alipay")
            {
                ModelState.AddModelError(
                    nameof(model.PaymentMethod),
                    "Please select a valid demo payment method.");
            }

            if (!ModelState.IsValid)
            {
                SetTotals(lines);
                return View("Index", model);
            }

            // Recheck products, prices and stock
            var verified = new List<CartLine>();

            foreach (var line in lines)
            {
                var product = await db.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p =>
                        p.Id == line.ProductId &&
                        p.IsActive);

                if (product == null ||
                    line.Quantity <= 0 ||
                    product.Stock < line.Quantity)
                {
                    TempData["Error"] =
                        "Product stock changed. Please check your cart.";

                    return RedirectToAction("Index", "Cart");
                }

                verified.Add(new CartLine
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    Quantity = line.Quantity,
                    Stock = product.Stock,
                    ImageUrl = product.ImageUrl
                });
            }

            decimal subtotal = verified.Sum(x => x.Subtotal);
            decimal shipping = subtotal >= 50m ? 0m : 3.99m;

            // Stock changes and order creation use one transaction
            await using var transaction =
                await db.Database.BeginTransactionAsync();

            try
            {
                foreach (var line in verified)
                {
                    // Only decrease stock when enough remains
                    int affected = await db.Products
                        .Where(p =>
                            p.Id == line.ProductId &&
                            p.IsActive &&
                            p.Stock >= line.Quantity)
                        .ExecuteUpdateAsync(setters =>
                            setters.SetProperty(
                                p => p.Stock,
                                p => p.Stock - line.Quantity));

                    if (affected != 1)
                    {
                        throw new InvalidOperationException(
                            "Stock is no longer sufficient.");
                    }
                }

                var order = new Order
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Status = "DemoPaid",
                    PaymentMethod = model.PaymentMethod + " (Demo)",
                    ShippingAddress = model.ShippingAddress,
                    Notes = model.Notes,
                    Subtotal = subtotal,
                    Shipping = shipping,
                    Discount = 0m,
                    Total = subtotal + shipping,

                    Items = verified.Select(line =>
                        new OrderItem
                        {
                            ProductId = line.ProductId,
                            ProductName = line.Name,
                            UnitPrice = line.Price,
                            Quantity = line.Quantity,
                            LineTotal = line.Subtotal
                        }).ToList()
                };

                db.Orders.Add(order);
                await db.SaveChangesAsync();

                // Remove database cart items
                await db.CartItems
                    .Where(c => c.UserId == userId)
                    .ExecuteDeleteAsync();

                await transaction.CommitAsync();

                // Clear session cart after successful commit
                cart.Clear();

                return View("Success", order);
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Stock changed. Please check your cart and try again.";

                return RedirectToAction("Index", "Cart");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Could not complete the demo order. Please try again.";

                return RedirectToAction("Index", "Cart");
            }
        }
    }
}
