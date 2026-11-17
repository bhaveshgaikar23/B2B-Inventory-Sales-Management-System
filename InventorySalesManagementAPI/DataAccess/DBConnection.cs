using Microsoft.Data.SqlClient;


namespace InventorySalesManagementAPI.DataAccess
{
    public class DBConnection
    {
        private readonly IConfiguration _configuration;

        public DBConnection(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public SqlConnection GetConnection()
        {
            string conString =
            _configuration.GetConnectionString("DBConnection")
            ?? throw new InvalidOperationException("Connection string not found.");

            return new SqlConnection(conString);
        }
    }
}
