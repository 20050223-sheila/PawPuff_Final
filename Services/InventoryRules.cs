namespace PawPuff.Services;

// Pure rules for easy isolated verification; actual deductions are atomic in CheckoutController.
public static class InventoryRules
{
    public const int LowStockThreshold = 5;
    public static bool CanPurchase(int available, int requested) =>
        available >= 0 && requested > 0 && requested <= available;
    public static bool NeedsRestock(int available) => available <= LowStockThreshold;
}
