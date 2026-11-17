namespace InventorySalesManagementAPI.DTOs
{
    public class CreatePaymentDTO
    {
        public int OrderID { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
