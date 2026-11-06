using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PawPuff.Models;

namespace PawPuff.Controllers;

public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn) : Controller
{
    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string email, string password, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("", "Please fill in every field.");
            return View();
        }

        // Public registration always creates a Buyer, even if a forged form sends a role.
        var user = new ApplicationUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            DisplayName = displayName.Trim()
        };

        var result = await users.CreateAsync(user, password);
        if (result.Succeeded)
        {
            var roleResult = await users.AddToRoleAsync(user, "Buyer");
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError("", error.Description);
                return View();
            }
            await signIn.SignInAsync(user, false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);
        return View();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        var user = await users.FindByEmailAsync(email?.Trim() ?? "");
        var result = user == null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signIn.PasswordSignInAsync(user.UserName!, password, false, lockoutOnFailure: true);

        if (result.Succeeded && user != null)
        {
            if (await users.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("Index", "Admin");

            if (await users.IsInRoleAsync(user, "Operator"))
                return RedirectToAction("Index", "Operations");

            // Respect local return URLs only for ordinary customer navigation.
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError("", result.IsLockedOut
            ? "Too many failed login attempts. Try again later."
            : "Invalid email or password.");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Denied() =>
        Content("Access denied. Your account does not have permission to view this page.");
}
