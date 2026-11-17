using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InventorySalesManagementAPI.Models;
using InventorySalesManagementAPI.DataAccess;
using Microsoft.AspNetCore.Authorization;

namespace InventorySalesManagementAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SuppliersController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public SuppliersController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddSupplier(SupplierModel supplier)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"INSERT INTO Suppliers
                                (SupplierName, Phone, Email)
                                VALUES
                                (@SupplierName, @Phone, @Email)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@SupplierName", supplier.SupplierName);
                    cmd.Parameters.AddWithValue("@Phone", supplier.Phone);
                    cmd.Parameters.AddWithValue("@Email", supplier.Email);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Supplier Added Successfully");
                    }
                    else
                    {
                        return BadRequest("Supplier Not Added");
                    }
                }
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Shopkeeper")]
        public IActionResult GetSuppliers()
        {
            List<SupplierModel> suppliers = new List<SupplierModel>();

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "SELECT * FROM Suppliers";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SupplierModel supplier = new SupplierModel();

                            supplier.SupplierID = Convert.ToInt32(reader["SupplierID"]);
                            supplier.SupplierName = reader["SupplierName"].ToString() ?? "";
                            supplier.Phone = reader["Phone"].ToString() ?? "";
                            supplier.Email = reader["Email"].ToString() ?? "";

                            suppliers.Add(supplier);
                        }
                    }
                }
            }

            return Ok(suppliers);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Shopkeeper")]
        public IActionResult GetSupplierById(int id)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "SELECT * FROM Suppliers WHERE SupplierID = @SupplierID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@SupplierID", id);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            SupplierModel supplier = new SupplierModel();

                            supplier.SupplierID = Convert.ToInt32(reader["SupplierID"]);
                            supplier.SupplierName = reader["SupplierName"].ToString() ?? "";
                            supplier.Phone = reader["Phone"].ToString() ?? "";
                            supplier.Email = reader["Email"].ToString() ?? "";

                            return Ok(supplier);
                        }
                        else
                        {
                            return NotFound("Supplier Not Found");
                        }
                    }
                }
            }
        }


        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateSupplier(int id, SupplierModel supplier)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"UPDATE Suppliers
                         SET SupplierName = @SupplierName,
                             Phone = @Phone,
                             Email = @Email
                         WHERE SupplierID = @SupplierID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@SupplierID", id);
                    cmd.Parameters.AddWithValue("@SupplierName", supplier.SupplierName);
                    cmd.Parameters.AddWithValue("@Phone", supplier.Phone);
                    cmd.Parameters.AddWithValue("@Email", supplier.Email);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Supplier Updated Successfully");
                    }
                    else
                    {
                        return NotFound("Supplier Not Found");
                    }
                }
            }
        }


        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteSupplier(int id)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "DELETE FROM Suppliers WHERE SupplierID = @SupplierID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@SupplierID", id);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Supplier Deleted Successfully");
                    }
                    else
                    {
                        return NotFound("Supplier Not Found");
                    }
                }
            }
        }
    }
}