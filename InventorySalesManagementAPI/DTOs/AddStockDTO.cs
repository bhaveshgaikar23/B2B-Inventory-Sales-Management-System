namespace InventorySalesManagementAPI.DTOs
{
    public class AddStockDTO
    {
        public int ProductID { get; set; }
        public int SupplierID { get; set; }
        public int Quantity { get; set; }
    }
}
