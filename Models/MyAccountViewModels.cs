using PawPuff.Models;

namespace PawPuff.Models;

public class MyAccountOverviewViewModel
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public int PaidCount { get; set; }
    public int CompletedCount { get; set; }
    public List<Order> RecentOrders { get; set; } = new();
    public List<string> SavedAddresses { get; set; } = new();
}

public class MyOrdersViewModel
{
    public string SelectedStatus { get; set; } = "all";
    public List<Order> Orders { get; set; } = new();
}
