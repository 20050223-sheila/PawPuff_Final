using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawPuff.Data;
using PawPuff.Models;
using PawPuff.Services;
using Stripe;
using Stripe.Checkout;

namespace PawPuff.Controllers;

[Authorize]
public class CheckoutController(ShopDbContext db, CartService cart, IConfiguration configuration) : Controller
{
    private List<CartLine> GetLines() => cart.Get();
    private void SetTotals(List<CartLine> lines)
    {
        ViewBag.Lines = lines;
        ViewBag.Subtotal = lines.Sum(x => x.Subtotal);
        ViewBag.Shipping = lines.Sum(x => x.Subtotal) >= 50m ? 0m : 3.99m;
        ViewBag.Discount = 0m;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var lines = GetLines();
        if (lines.Count == 0) return RedirectToAction("Index", "Cart");
        SetTotals(lines);
        ViewBag.StripeConfigured = !string.IsNullOrWhiteSpace(configuration["Stripe:SecretKey"]);
        return View(new CheckoutViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutViewModel model)
    {
        var lines = GetLines();
        if (lines.Count == 0) return RedirectToAction("Index", "Cart");
        SetTotals(lines);
        ViewBag.StripeConfigured = !string.IsNullOrWhiteSpace(configuration["Stripe:SecretKey"]);
        if (!ModelState.IsValid) return View(model);

        // Only card payment is wired to Stripe in this starter. WeChat Pay/Alipay need
        // provider account eligibility and explicit dashboard configuration.
        if (model.PaymentMethod != "Card")
        {
            ModelState.AddModelError(nameof(model.PaymentMethod), "This payment method is not configured. Choose Credit/Debit Card (Stripe test mode) or contact the project administrator.");
            return View(model);
        }

        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            ModelState.AddModelError(string.Empty, "Stripe is not configured yet. Add a Stripe TEST secret key using User Secrets or an environment variable. No payment has been taken.");
            return View(model);
        }

        var productRows = new List<PawPuff.Models.Product>();
        foreach (var line in lines)
        {
            var product = await db.Products.FirstOrDefaultAsync(x => x.Id == line.ProductId && x.IsActive);
            if (product is null || product.Stock < line.Quantity)
            {
                ModelState.AddModelError(string.Empty, $"Stock is unavailable for {line.Name}. Please return to the cart.");
                return View(model);
            }
            productRows.Add(product);
        }

        var subtotal = lines.Sum(x => x.Subtotal);
        var shipping = subtotal >= 50m ? 0m : 3.99m;
        var order = new Order
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            PaymentMethod = "Card (Stripe)",
            ShippingAddress = model.ShippingAddress,
            Notes = model.Notes,
            Subtotal = subtotal,
            Shipping = shipping,
            Discount = 0m,
            Total = subtotal + shipping,
            Status = "PendingPayment",
            Items = lines.Select(x => new OrderItem { ProductId = x.ProductId, ProductName = x.Name, UnitPrice = x.Price, Quantity = x.Quantity, LineTotal = x.Subtotal }).ToList()
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        try
        {
            StripeConfiguration.ApiKey = secretKey;
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                PaymentMethodTypes = new List<string> { "card" },
                ClientReferenceId = order.Id.ToString(),
                SuccessUrl = Url.Action(nameof(PaymentSuccess), "Checkout", new { session_id = "{CHECKOUT_SESSION_ID}" }, Request.Scheme)!,
                CancelUrl = Url.Action(nameof(PaymentCancelled), "Checkout", new { orderId = order.Id }, Request.Scheme)!,
                Metadata = new Dictionary<string, string> { ["orderId"] = order.Id.ToString() },
                LineItems = lines.Select(line => new SessionLineItemOptions
                {
                    Quantity = line.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "gbp",
                        UnitAmount = decimal.ToInt64(decimal.Round(line.Price * 100m, 0, MidpointRounding.AwayFromZero)),
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = line.Name }
                    }
                }).ToList()
            };
            if (shipping > 0)
            {
                options.LineItems.Add(new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "gbp", UnitAmount = 399,
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Shipping" }
                    }
                });
            }
            var session = await new SessionService().CreateAsync(options);
            return Redirect(session.Url);
        }
        catch (StripeException)
        {
            order.Status = "PaymentSetupError";
            await db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "Could not start Stripe Checkout. Check your test API key and Stripe configuration. No payment has been taken.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> PaymentSuccess(string session_id)
    {
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(session_id)) return RedirectToAction(nameof(Index));
        StripeConfiguration.ApiKey = secretKey;
        Session session;
        try { session = await new SessionService().GetAsync(session_id); }
        catch (StripeException) { return BadRequest("Unable to verify payment session."); }
        if (session.PaymentStatus != "paid" || !int.TryParse(session.Metadata.GetValueOrDefault("orderId"), out var orderId))
            return RedirectToAction(nameof(PaymentCancelled), new { orderId = int.TryParse(session.ClientReferenceId, out var id) ? id : 0 });

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return NotFound();
        if (order.Status != "Paid")
        {
            // Recheck current inventory before decrementing. For production use, add a webhook
            // and transactional/idempotent stock reservation to handle concurrent orders.
            foreach (var item in order.Items)
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId && p.IsActive);
                if (product is null || product.Stock < item.Quantity)
                {
                    order.Status = "PaidNeedsReview";
                    await db.SaveChangesAsync();
                    return View("Success", order);
                }
                product.Stock -= item.Quantity;
            }
            order.Status = "Paid";
            await db.SaveChangesAsync();
            cart.Clear();
        }
        return View("Success", order);
    }

    [HttpGet]
    public async Task<IActionResult> PaymentCancelled(int orderId)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is not null && order.Status == "PendingPayment")
        {
            order.Status = "PaymentCancelled";
            await db.SaveChangesAsync();
        }
        TempData["CheckoutMessage"] = "Payment was cancelled. Your cart has not been cleared.";
        return RedirectToAction(nameof(Index));
    }
}
