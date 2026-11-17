using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InventorySalesManagementAPI.DataAccess;
using Microsoft.Data.SqlClient;

namespace InventorySalesManagementAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public ReportsController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }
        [HttpGet("sales-summary")]
        public IActionResult GetSalesSummary()
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT
                COUNT(*) AS TotalOrders,

                COUNT(CASE WHEN OrderStatus = 'Delivered'
                      THEN 1 END) AS DeliveredOrders,

                COUNT(CASE WHEN OrderStatus = 'Cancelled'
                      THEN 1 END) AS CancelledOrders,

                COUNT(CASE WHEN OrderStatus = 'Pending'
                      THEN 1 END) AS PendingOrders,

                ISNULL(SUM(CASE
                    WHEN OrderStatus = 'Delivered'
                    THEN TotalAmount
                    ELSE 0
                END), 0) AS TotalSales

            FROM Orders";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return Ok(new
                        {
                            TotalOrders = Convert.ToInt32(reader["TotalOrders"]),
                            DeliveredOrders = Convert.ToInt32(reader["DeliveredOrders"]),
                            CancelledOrders = Convert.ToInt32(reader["CancelledOrders"]),
                            PendingOrders = Convert.ToInt32(reader["PendingOrders"]),
                            TotalSales = Convert.ToDecimal(reader["TotalSales"])
                        });
                    }
                }

                return Ok("No sales data available.");
            }
        }


        [HttpGet("monthly-sales")]
        public IActionResult GetMonthlySales()
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT
                YEAR(OrderDate) AS SalesYear,
                MONTH(OrderDate) AS SalesMonth,
                COUNT(*) AS TotalOrders,
                SUM(TotalAmount) AS TotalSales
            FROM Orders
            WHERE OrderStatus = 'Delivered'
            GROUP BY
                YEAR(OrderDate),
                MONTH(OrderDate)
            ORDER BY
                SalesYear DESC,
                SalesMonth DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    List<object> monthlySales = new List<object>();

                    while (reader.Read())
                    {
                        monthlySales.Add(new
                        {
                            SalesYear = Convert.ToInt32(reader["SalesYear"]),
                            SalesMonth = Convert.ToInt32(reader["SalesMonth"]),
                            TotalOrders = Convert.ToInt32(reader["TotalOrders"]),
                            TotalSales = Convert.ToDecimal(reader["TotalSales"])
                        });
                    }

                    return Ok(monthlySales);
                }
            }
        }

        [HttpGet("top-selling-products")]
        public IActionResult GetTopSellingProducts()
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT TOP 10
                oi.ProductID,
                oi.ProductName,
                SUM(oi.Quantity) AS TotalQuantitySold,
                SUM(
                    oi.UnitPrice * oi.Quantity *
                    (1 - oi.DiscountPercent / 100.0)
                ) AS TotalRevenue
            FROM OrderItems oi
            INNER JOIN Orders o
                ON oi.OrderID = o.OrderID
            WHERE o.OrderStatus = 'Delivered'
            GROUP BY
                oi.ProductID,
                oi.ProductName
            ORDER BY
                TotalQuantitySold DESC,
                TotalRevenue DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    List<object> topProducts = new List<object>();

                    while (reader.Read())
                    {
                        topProducts.Add(new
                        {
                            ProductID = Convert.ToInt32(reader["ProductID"]),
                            ProductName = reader["ProductName"].ToString(),
                            TotalQuantitySold = Convert.ToInt32(
                                reader["TotalQuantitySold"]),
                            TotalRevenue = Convert.ToDecimal(
                                reader["TotalRevenue"])
                        });
                    }

                    return Ok(topProducts);
                }
            }
        }


        [HttpGet("shopkeeper-purchases")]
        public IActionResult GetShopkeeperPurchases()
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT
                u.UserID,
                u.FullName,
                u.Email,
                u.BusinessName,

                COUNT(o.OrderID) AS TotalOrders,

                COUNT(CASE
                    WHEN o.OrderStatus = 'Delivered'
                    THEN 1
                END) AS DeliveredOrders,

                ISNULL(SUM(CASE
                    WHEN o.OrderStatus = 'Delivered'
                    THEN o.TotalAmount
                    ELSE 0
                END), 0) AS TotalPurchaseAmount

            FROM Users u
            LEFT JOIN Orders o
                ON u.UserID = o.UserID

            WHERE u.Role = 'Shopkeeper'

            GROUP BY
                u.UserID,
                u.FullName,
                u.Email,
                u.BusinessName

            ORDER BY TotalPurchaseAmount DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    List<object> reports = new List<object>();

                    while (reader.Read())
                    {
                        reports.Add(new
                        {
                            UserID = Convert.ToInt32(reader["UserID"]),
                            FullName = reader["FullName"].ToString(),
                            Email = reader["Email"].ToString(),

                            BusinessName =
                                reader["BusinessName"] == DBNull.Value
                                ? null
                                : reader["BusinessName"].ToString(),

                            TotalOrders = Convert.ToInt32(
                                reader["TotalOrders"]),

                            DeliveredOrders = Convert.ToInt32(
                                reader["DeliveredOrders"]),

                            TotalPurchaseAmount = Convert.ToDecimal(
                                reader["TotalPurchaseAmount"])
                        });
                    }

                    return Ok(reports);
                }
            }
        }
    }
}
