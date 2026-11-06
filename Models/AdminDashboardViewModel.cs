using System.ComponentModel.DataAnnotations;

namespace PawPuff.Models;

public class AdminDashboardViewModel
{
    public int OrdersPage { get; set; }
    public int TotalOrderPages { get; set; }
    public int AllOrderCount { get; set; }
    public int ActiveProductCount { get; set; }
    public int PaidOrderCount { get; set; }
    public int UserCount { get; set; }
    public int SoldUnits { get; set; }
    public decimal DemoSalesTotal { get; set; }
    public int PendingReviewCount { get; set; }
    public int LowStockCount { get; set; }
    public List<Order> RecentOrders { get; set; } = new();
    public List<ProductReviewRequest> PendingReviews { get; set; } = new();
    public List<ProductReviewRequest> ReviewHistory { get; set; } = new();
    public List<OperatorAccountRow> OperatorAccounts { get; set; } = new();
    public List<TopSellingProductRow> TopSellingProducts { get; set; } = new();
}

public class OperatorAccountRow
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string UserName { get; set; } = "";
    public bool IsDisabled { get; set; }
}

public class TopSellingProductRow
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal DemoRevenue { get; set; }
}

public class CreateOperatorViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 3)] public string UserName { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 10), DataType(DataType.Password)]
    public string Password { get; set; } = "";
}
