using Microsoft.AspNetCore.Mvc;

namespace PawPuff.Controllers;

// Legacy route retired: PawPuff is a single-brand shop, not a marketplace.
public class SellerController : Controller
{
    public IActionResult Index() => NotFound();
}
