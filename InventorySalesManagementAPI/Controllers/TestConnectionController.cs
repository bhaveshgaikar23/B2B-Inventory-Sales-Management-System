
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InventorySalesManagementAPI.DataAccess;

namespace InventorySalesManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestConnectionController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public TestConnectionController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpGet]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                using (SqlConnection con = _dbConnection.GetConnection())
                {
                    await con.OpenAsync();
                    return Ok("Database Connected Successfully!");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}

