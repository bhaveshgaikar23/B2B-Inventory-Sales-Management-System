namespace InventorySalesManagementAPI.Models
{
    public class UserModel
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Phone { get; set; }
        public string BusinessName { get; set; }
        public string Role { get; set; }
        public string ApprovalStatus { get; set; }
        public int? ApprovedBy { get; set; }
    }
}
