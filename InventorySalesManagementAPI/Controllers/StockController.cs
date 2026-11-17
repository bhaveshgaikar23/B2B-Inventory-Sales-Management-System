using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using InventorySalesManagementAPI.DataAccess;
using InventorySalesManagementAPI.DTOs;

namespace InventorySalesManagementAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class StockController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public StockController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpPost("add")]
        [Authorize(Roles = "Admin")]
        public IActionResult AddStock(AddStockDTO dto)
        {
            if (dto.ProductID <= 0 || dto.SupplierID <= 0 || dto.Quantity <= 0)
                return BadRequest("ProductID, SupplierID and Quantity must be greater than zero.");

            if (!int.TryParse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out int adminId))
                return Unauthorized();

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        string productQuery = @"
                    SELECT SupplierID
                    FROM Products WITH (UPDLOCK, HOLDLOCK)
                    WHERE ProductID = @ProductID";

                        int? supplierId = null;

                        using (SqlCommand cmd =
                            new SqlCommand(productQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ProductID", dto.ProductID);

                            object? result = cmd.ExecuteScalar();

                            if (result != null && result != DBNull.Value)
                                supplierId = Convert.ToInt32(result);
                        }

                        if (supplierId == null)
                        {
                            transaction.Rollback();
                            return NotFound("Product not found.");
                        }

                        if (supplierId != dto.SupplierID)
                        {
                            transaction.Rollback();
                            return BadRequest(
                                "Supplier does not match the product's supplier.");
                        }

                        string updateQuery = @"
                    UPDATE Products
                    SET StockQuantity = StockQuantity + @Quantity
                    WHERE ProductID = @ProductID
                      AND StockQuantity <= 2147483647 - @Quantity";

                        using (SqlCommand cmd =
                            new SqlCommand(updateQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ProductID", dto.ProductID);
                            cmd.Parameters.AddWithValue("@Quantity", dto.Quantity);

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                transaction.Rollback();
                                return BadRequest("Unable to add stock. Check quantity limits.");
                            }
                        }

                        string stockTransactionQuery = @"
                    INSERT INTO StockTransactions
                        (ProductID, SupplierID, CreatedBy,
                         TransactionType, Quantity)
                    VALUES
                        (@ProductID, @SupplierID, @CreatedBy,
                         'StockIn', @Quantity)";

                        using (SqlCommand cmd =
                            new SqlCommand(stockTransactionQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ProductID", dto.ProductID);
                            cmd.Parameters.AddWithValue("@SupplierID", dto.SupplierID);
                            cmd.Parameters.AddWithValue("@CreatedBy", adminId);
                            cmd.Parameters.AddWithValue("@Quantity", dto.Quantity);

                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        return Ok("Stock added successfully.");
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();

                        return StatusCode(500, "Unable to add stock.");
                    }
                }
            }
        }


        [HttpPost("adjust")]
        [Authorize(Roles = "Admin")]
        public IActionResult AdjustStock(AdjustStockDTO dto)
        {
            if (dto.ProductID <= 0 || dto.Quantity <= 0)
                return BadRequest("ProductID and Quantity must be greater than zero.");

            if (dto.AdjustmentType != "AdjustmentIn" &&
                dto.AdjustmentType != "AdjustmentOut")
                return BadRequest("Use AdjustmentIn or AdjustmentOut.");

            if (!int.TryParse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out int adminId))
                return Unauthorized();

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        string updateQuery = dto.AdjustmentType == "AdjustmentIn"
                            ? @"UPDATE Products
                        SET StockQuantity = StockQuantity + @Quantity
                        WHERE ProductID = @ProductID
                          AND StockQuantity <= 2147483647 - @Quantity"
                            : @"UPDATE Products
                        SET StockQuantity = StockQuantity - @Quantity
                        WHERE ProductID = @ProductID
                          AND StockQuantity >= @Quantity";

                        using (SqlCommand cmd =
                            new SqlCommand(updateQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ProductID", dto.ProductID);
                            cmd.Parameters.AddWithValue("@Quantity", dto.Quantity);

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                transaction.Rollback();
                                return BadRequest(
                                    "Product not found or insufficient stock.");
                            }
                        }

                        string transactionQuery = @"
                    INSERT INTO StockTransactions
                        (ProductID, CreatedBy, TransactionType, Quantity)
                    VALUES
                        (@ProductID, @CreatedBy, @TransactionType, @Quantity)";

                        using (SqlCommand cmd =
                            new SqlCommand(transactionQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ProductID", dto.ProductID);
                            cmd.Parameters.AddWithValue("@CreatedBy", adminId);
                            cmd.Parameters.AddWithValue("@TransactionType",
                                                        dto.AdjustmentType);
                            cmd.Parameters.AddWithValue("@Quantity", dto.Quantity);

                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        return Ok("Stock adjusted successfully.");
                    }
                    catch
                    {
                        transaction.Rollback();
                        return StatusCode(500, "Unable to adjust stock.");
                    }
                }
            }
        }


        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult GetStock()
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT
                p.ProductID,
                p.ProductName,
                p.SKU,
                c.CategoryName,
                s.SupplierName,
                p.StockQuantity,
                p.MinOrderQuantity,
                p.MaxOrderQuantity
            FROM Products p
            INNER JOIN Categories c
                ON p.CategoryID = c.CategoryID
            INNER JOIN Suppliers s
                ON p.SupplierID = s.SupplierID
            ORDER BY p.ProductName";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    List<object> stocks = new List<object>();

                    while (reader.Read())
                    {
                        stocks.Add(new
                        {
                            ProductID = Convert.ToInt32(reader["ProductID"]),
                            ProductName = reader["ProductName"].ToString(),
                            SKU = reader["SKU"].ToString(),
                            CategoryName = reader["CategoryName"].ToString(),
                            SupplierName = reader["SupplierName"].ToString(),
                            StockQuantity = Convert.ToInt32(reader["StockQuantity"]),
                            MinOrderQuantity = Convert.ToInt32(reader["MinOrderQuantity"]),
                            MaxOrderQuantity = Convert.ToInt32(reader["MaxOrderQuantity"])
                        });
                    }

                    return Ok(stocks);
                }
            }
        }

        [HttpGet("transactions")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetStockTransactions()
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT
                st.TransactionID,
                st.ProductID,
                p.ProductName,
                st.OrderID,
                st.SupplierID,
                s.SupplierName,
                st.CreatedBy,
                u.FullName AS CreatedByName,
                st.TransactionType,
                st.Quantity,
                st.TransactionDate
            FROM StockTransactions st
            INNER JOIN Products p
                ON st.ProductID = p.ProductID
            INNER JOIN Users u
                ON st.CreatedBy = u.UserID
            LEFT JOIN Suppliers s
                ON st.SupplierID = s.SupplierID
            ORDER BY st.TransactionDate DESC,
                     st.TransactionID DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    List<object> transactions = new List<object>();

                    while (reader.Read())
                    {
                        transactions.Add(new
                        {
                            TransactionID = Convert.ToInt32(reader["TransactionID"]),
                            ProductID = Convert.ToInt32(reader["ProductID"]),
                            ProductName = reader["ProductName"].ToString(),

                            OrderID = reader["OrderID"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["OrderID"]),

                            SupplierID = reader["SupplierID"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["SupplierID"]),

                            SupplierName = reader["SupplierName"] == DBNull.Value
                                ? null
                                : reader["SupplierName"].ToString(),

                            CreatedBy = Convert.ToInt32(reader["CreatedBy"]),
                            CreatedByName = reader["CreatedByName"].ToString(),

                            TransactionType = reader["TransactionType"].ToString(),
                            Quantity = Convert.ToInt32(reader["Quantity"]),
                            TransactionDate = Convert.ToDateTime(
                                reader["TransactionDate"])
                        });
                    }

                    return Ok(transactions);
                }
            }
        }




        [HttpGet("low-stock")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetLowStockProducts()
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT
                p.ProductID,
                p.ProductName,
                p.SKU,
                c.CategoryName,
                s.SupplierName,
                p.StockQuantity,
                p.MinOrderQuantity
            FROM Products p
            INNER JOIN Categories c
                ON p.CategoryID = c.CategoryID
            INNER JOIN Suppliers s
                ON p.SupplierID = s.SupplierID
            WHERE p.StockQuantity < p.MinOrderQuantity
            ORDER BY p.StockQuantity ASC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    List<object> lowStockProducts = new List<object>();

                    while (reader.Read())
                    {
                        lowStockProducts.Add(new
                        {
                            ProductID = Convert.ToInt32(reader["ProductID"]),
                            ProductName = reader["ProductName"].ToString(),
                            SKU = reader["SKU"].ToString(),
                            CategoryName = reader["CategoryName"].ToString(),
                            SupplierName = reader["SupplierName"].ToString(),
                            StockQuantity = Convert.ToInt32(reader["StockQuantity"]),
                            MinOrderQuantity = Convert.ToInt32(reader["MinOrderQuantity"])
                        });
                    }

                    return Ok(lowStockProducts);
                }
            }
        }
    }
}
