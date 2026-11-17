namespace InventorySalesManagementAPI.DTOs
{
    public class AdjustStockDTO
    {
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public string AdjustmentType { get; set; } = string.Empty;
    }
}