using System.ComponentModel.DataAnnotations;

namespace PawPuff.Models;

// Pending reviews are the admin's in-app notifications; products can remain listed until rejected.
public class ProductReviewRequest
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    [StringLength(100)] public string ProductName { get; set; } = "";
    public string? SubmittedByUserId { get; set; }
    [StringLength(30)] public string ActionType { get; set; } = "Published";
    [StringLength(1000)] public string Summary { get; set; } = "";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    [StringLength(20)] public string Status { get; set; } = "Pending";
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
