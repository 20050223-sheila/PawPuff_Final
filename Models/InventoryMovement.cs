using System.ComponentModel.DataAnnotations;

namespace PawPuff.Models;

// Immutable audit entry; changes to stock are stored alongside the order/adjustment.
public class InventoryMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    [StringLength(100)] public string ProductName { get; set; } = "";
    public int? OrderId { get; set; }
    public string? UserId { get; set; }
    public int QuantityChanged { get; set; }
    public int StockAfter { get; set; }
    [StringLength(30)] public string Reason { get; set; } = "Sale";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
