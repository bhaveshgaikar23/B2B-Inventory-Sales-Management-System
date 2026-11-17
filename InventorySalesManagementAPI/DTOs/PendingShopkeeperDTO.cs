namespace InventorySalesManagementAPI.DTOs
{
    public class PendingShopkeeperDTO
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string BusinessName { get; set; }
        public string ApprovalStatus { get; set; }
    }
}
