using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawPuff.Models;
namespace PawPuff.Data;
public static class SeedData {
 public static async Task InitializeAsync(IServiceProvider services) {
  var db=services.GetRequiredService<ShopDbContext>(); await db.Database.EnsureCreatedAsync();
  if(!await db.Products.AnyAsync()) { db.Products.AddRange(
   new Product{Name="Salmon Cat Food",Category="Food",PetType="Cat",Description="Complete salmon recipe for adult cats.",Price=18.50m,Stock=30,ImageUrl="https://images.unsplash.com/photo-1589924691995-400dc9ecc119?auto=format&fit=crop&w=700&q=80"},
   new Product{Name="Chicken Dog Treats",Category="Treats",PetType="Dog",Description="Training treats made with chicken.",Price=6.99m,Stock=45},
   new Product{Name="Cat Feather Wand",Category="Toys",PetType="Cat",Description="Interactive feather toy for active cats.",Price=4.50m,Stock=20},
   new Product{Name="Soft Dog Bed",Category="Daily Essentials",PetType="Dog",Description="Washable, comfortable resting bed.",Price=29.99m,Stock=12},
   new Product{Name="Pet Raincoat",Category="Clothing",PetType="Dog",Description="Lightweight coat for rainy walks.",Price=14.00m,Stock=16},
   new Product{Name="Cat Litter Scoop",Category="Daily Essentials",PetType="Cat",Description="Durable scoop for daily litter cleaning.",Price=3.25m,Stock=50}); await db.SaveChangesAsync(); }
  var roles=services.GetRequiredService<RoleManager<IdentityRole>>(); foreach(var role in new[]{"Buyer","Seller","Admin"}) if(!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
  var users=services.GetRequiredService<UserManager<ApplicationUser>>();
  var adminName = services.GetRequiredService<IConfiguration>()["DemoAdmin:UserName"] ?? "ZMXAdmin";
  var adminEmail = services.GetRequiredService<IConfiguration>()["DemoAdmin:Email"] ?? "P2983732@my365.dmu.ac.uk";
  var adminPassword = services.GetRequiredService<IConfiguration>()["DemoAdmin:Password"] ?? "ZMXAdmin123!";
  var adminByName = await users.FindByNameAsync(adminName);
  var userUsingConfiguredEmail = await users.FindByEmailAsync(adminEmail);
  if (adminByName is null && userUsingConfiguredEmail is null) {
   // EmailConfirmed stays false because this starter does not yet send confirmation emails.
   var admin=new ApplicationUser{UserName=adminName,Email=adminEmail,EmailConfirmed=false,DisplayName="ZMX Demo Admin"};
   var result=await users.CreateAsync(admin,adminPassword);
   if(result.Succeeded) await users.AddToRoleAsync(admin,"Admin");
   else throw new InvalidOperationException("Could not create demo admin: " + string.Join("; ", result.Errors.Select(e=>e.Description)));
  }
  else if (adminByName is not null && (string.IsNullOrWhiteSpace(adminByName.Email) || !string.Equals(adminByName.Email, adminEmail, StringComparison.OrdinalIgnoreCase))) {
   // Keep an existing local ZMXAdmin account aligned with appsettings.json on startup.
   if (userUsingConfiguredEmail is not null && userUsingConfiguredEmail.Id != adminByName.Id)
    throw new InvalidOperationException($"The configured admin email '{adminEmail}' is already assigned to another account.");
   var emailResult = await users.SetEmailAsync(adminByName, adminEmail);
   if (!emailResult.Succeeded)
    throw new InvalidOperationException("Could not update the demo admin email: " + string.Join("; ", emailResult.Errors.Select(e=>e.Description)));
            adminByName.EmailConfirmed = false;

            var updateResult = await users.UpdateAsync(adminByName);

            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not update admin: " +
                    string.Join("; ",
                        updateResult.Errors.Select(e => e.Description))
                );
            }
        }
 }
}
