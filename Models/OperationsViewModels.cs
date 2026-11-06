using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PawPuff.Models;

public class OperationsDashboardViewModel
{
    public string? Search { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string? PetType { get; set; }
    public string? Category { get; set; }
    public bool LowStockOnly { get; set; }
    public int ProductCount { get; set; }
    public int LowStockCount { get; set; }
    public int OrderCount { get; set; }
    public int CustomerCount { get; set; }
    public List<Product> Products { get; set; } = new();
    public List<Product> LowStockProducts { get; set; } = new();
    public List<InventoryMovement> RecentInventoryMovements { get; set; } = new();
    public List<Order> RecentOrders { get; set; } = new();
}

public class OperationsNewProductViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Required, StringLength(100)]
    public string Category { get; set; } = "Food";

    [Required, StringLength(20)]
    public string PetType { get; set; } = "Dog";

    [Required, StringLength(1000)]
    public string Description { get; set; } = "";

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Price { get; set; }

    [Range(0, 100000)]
    public int Stock { get; set; }

    // Choose one image upload or image URL; no product image database migration needed.
    public IFormFile? ImageFile { get; set; }

    [StringLength(1500)]
    public string? ImageUrl { get; set; }
}
