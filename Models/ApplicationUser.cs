using Microsoft.AspNetCore.Identity;
namespace PawPuff.Models;
public class ApplicationUser : IdentityUser { public string DisplayName { get; set; } = ""; }
