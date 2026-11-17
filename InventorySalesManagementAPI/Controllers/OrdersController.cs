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
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public OrdersController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }
        [HttpPost("place")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult PlaceOrder(PlaceOrderDTO dto)
        {
            

            if (string.IsNullOrWhiteSpace(dto.ShippingAddress))
                return BadRequest("Shipping address is required.");

            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value is not string userIdText
                || !int.TryParse(userIdText, out int userId))
                return Unauthorized();

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                using (SqlTransaction transaction =
                       con.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        string cartQuery = @"
                    SELECT ci.ProductID, p.ProductName,
                           p.WholesalePrice, ci.Quantity,
                           p.StockQuantity, p.MinOrderQuantity,
                           p.MaxOrderQuantity
                    FROM Cart c
                    INNER JOIN CartItems ci ON c.CartID = ci.CartID
                    INNER JOIN Products p ON ci.ProductID = p.ProductID
                    WHERE c.UserID = @UserID";

                        var items = new List<(int ProductID, string Name,
                            decimal Price, int Quantity)>();

                        decimal totalAmount = 0;

                        using (SqlCommand cmd =
                               new SqlCommand(cartQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserID", userId);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int quantity = Convert.ToInt32(reader["Quantity"]);
                                    int stock = Convert.ToInt32(reader["StockQuantity"]);
                                    int min = Convert.ToInt32(reader["MinOrderQuantity"]);
                                    int max = Convert.ToInt32(reader["MaxOrderQuantity"]);

                                    if (quantity < min || quantity > max || quantity > stock)
                                        return BadRequest(
                                            "A cart item exceeds stock or order limits.");

                                    int productId = Convert.ToInt32(reader["ProductID"]);
                                    string name = reader["ProductName"].ToString()!;
                                    decimal price = Convert.ToDecimal(reader["WholesalePrice"]);

                                    items.Add((productId, name, price, quantity));
                                    totalAmount += price * quantity;
                                }
                            }
                        }

                        if (items.Count == 0)
                            return BadRequest("Your cart is empty.");

                        string orderNumber = "ORD-" +
                            Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

                        string orderQuery = @"
                    INSERT INTO Orders
                        (UserID, OrderNumber, OrderStatus,
                         TotalAmount, ShippingAddress)
                    OUTPUT INSERTED.OrderID
                    VALUES
                        (@UserID, @OrderNumber, 'Pending',
                         @TotalAmount, @ShippingAddress)";

                        int orderId;

                        using (SqlCommand cmd =
                               new SqlCommand(orderQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserID", userId);
                            cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                            cmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                            cmd.Parameters.AddWithValue("@ShippingAddress",
                                                       dto.ShippingAddress);

                            orderId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        foreach (var item in items)
                        {
                            string itemQuery = @"
                        INSERT INTO OrderItems
                            (OrderID, ProductID, ProductName,
                             UnitPrice, Quantity, DiscountPercent)
                        VALUES
                            (@OrderID, @ProductID, @ProductName,
                             @UnitPrice, @Quantity, 0)";

                            using (SqlCommand cmd =
                                   new SqlCommand(itemQuery, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@OrderID", orderId);
                                cmd.Parameters.AddWithValue("@ProductID", item.ProductID);
                                cmd.Parameters.AddWithValue("@ProductName", item.Name);
                                cmd.Parameters.AddWithValue("@UnitPrice", item.Price);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                cmd.ExecuteNonQuery();
                            }

                            string stockQuery = @"
                        UPDATE Products
                        SET StockQuantity = StockQuantity - @Quantity
                        WHERE ProductID = @ProductID
                          AND StockQuantity >= @Quantity";

                            using (SqlCommand cmd =
                                   new SqlCommand(stockQuery, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ProductID", item.ProductID);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);

                                if (cmd.ExecuteNonQuery() != 1)
                                    throw new InvalidOperationException(
                                        "Insufficient stock for " + item.Name);
                            }

                            string transactionQuery = @"
                        INSERT INTO StockTransactions
                            (ProductID, OrderID, CreatedBy,
                             TransactionType, Quantity)
                        VALUES
                            (@ProductID, @OrderID, @CreatedBy,
                             'StockOut', @Quantity)";

                            using (SqlCommand cmd =
                                   new SqlCommand(transactionQuery, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ProductID", item.ProductID);
                                cmd.Parameters.AddWithValue("@OrderID", orderId);
                                cmd.Parameters.AddWithValue("@CreatedBy", userId);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        string clearCartQuery = @"
                    DELETE ci
                    FROM CartItems ci
                    INNER JOIN Cart c ON ci.CartID = c.CartID
                    WHERE c.UserID = @UserID";

                        using (SqlCommand cmd =
                               new SqlCommand(clearCartQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserID", userId);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        return Ok(new
                        {
                            Message = "Order placed successfully.",
                            OrderID = orderId,
                            OrderNumber = orderNumber,
                            TotalAmount = totalAmount
                        });
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        return StatusCode(500,
                            "Unable to place order. No changes were saved.");
                    }
                }
            }
        }


        [HttpGet]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult GetOrders()
        {
            if (!int.TryParse(
    User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
    out int userId))
            {
                return Unauthorized();
            }

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT OrderID, OrderNumber, OrderDate,
                   OrderStatus, TotalAmount, ShippingAddress
            FROM Orders
            WHERE UserID = @UserID
            ORDER BY OrderDate DESC, OrderID DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<object> orders = new List<object>();

                        while (reader.Read())
                        {
                            orders.Add(new
                            {
                                OrderID = Convert.ToInt32(reader["OrderID"]),
                                OrderNumber = reader["OrderNumber"].ToString(),
                                OrderDate = Convert.ToDateTime(reader["OrderDate"]),
                                OrderStatus = reader["OrderStatus"].ToString(),
                                TotalAmount = Convert.ToDecimal(reader["TotalAmount"]),
                                ShippingAddress = reader["ShippingAddress"].ToString()
                            });
                        }

                        return Ok(orders);
                    }
                }
            }
        }


        [HttpGet("{id}")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult GetOrderDetails(int id)
        {
            if (!int.TryParse(
     User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
     out int userId))
            {
                return Unauthorized();
            }

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string orderQuery = @"
            SELECT OrderID, OrderNumber, OrderDate,
                   OrderStatus, TotalAmount, ShippingAddress
            FROM Orders
            WHERE OrderID = @OrderID AND UserID = @UserID";

                object? order = null;

                using (SqlCommand cmd = new SqlCommand(orderQuery, con))
                {
                    cmd.Parameters.AddWithValue("@OrderID", id);
                    cmd.Parameters.AddWithValue("@UserID", userId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return NotFound("Order not found.");

                        order = new
                        {
                            OrderID = Convert.ToInt32(reader["OrderID"]),
                            OrderNumber = reader["OrderNumber"].ToString(),
                            OrderDate = Convert.ToDateTime(reader["OrderDate"]),
                            OrderStatus = reader["OrderStatus"].ToString(),
                            TotalAmount = Convert.ToDecimal(reader["TotalAmount"]),
                            ShippingAddress = reader["ShippingAddress"].ToString()
                        };
                    }
                }

                string itemQuery = @"
            SELECT OrderItemID, ProductID, ProductName,
                   UnitPrice, Quantity, DiscountPercent
            FROM OrderItems
            WHERE OrderID = @OrderID";

                List<object> items = new List<object>();

                using (SqlCommand cmd = new SqlCommand(itemQuery, con))
                {
                    cmd.Parameters.AddWithValue("@OrderID", id);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal price = Convert.ToDecimal(reader["UnitPrice"]);
                            int quantity = Convert.ToInt32(reader["Quantity"]);
                            decimal discount = Convert.ToDecimal(
                                reader["DiscountPercent"]
                            );

                            items.Add(new
                            {
                                OrderItemID = Convert.ToInt32(reader["OrderItemID"]),
                                ProductID = Convert.ToInt32(reader["ProductID"]),
                                ProductName = reader["ProductName"].ToString(),
                                UnitPrice = price,
                                Quantity = quantity,
                                DiscountPercent = discount,
                                TotalPrice = price * quantity * (1 - discount / 100)
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Order = order,
                    Items = items
                });
            }
        }


        [HttpPut("cancel/{id}")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult CancelOrder(int id)
        {
            if (!int.TryParse(
    User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
    out int userId))
            {
                return Unauthorized();
            }

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                using (SqlTransaction transaction =
                    con.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        string orderQuery = @"
                    SELECT OrderStatus
                    FROM Orders WITH (UPDLOCK, HOLDLOCK)
                    WHERE OrderID = @OrderID
                    AND UserID = @UserID";

                        string? status;

                        using (SqlCommand cmd =
                            new SqlCommand(orderQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderID", id);
                            cmd.Parameters.AddWithValue("@UserID", userId);

                            status = cmd.ExecuteScalar()?.ToString();
                        }

                        if (status == null)
                        {
                            transaction.Rollback();
                            return NotFound("Order not found.");
                        }

                        if (status != "Pending")
                        {
                            transaction.Rollback();
                            return BadRequest(
                                "Only pending orders can be cancelled.");
                        }

                        string itemsQuery = @"
                    SELECT ProductID, Quantity
                    FROM OrderItems
                    WHERE OrderID = @OrderID";

                        var items = new List<(int ProductID, int Quantity)>();

                        using (SqlCommand cmd =
                            new SqlCommand(itemsQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderID", id);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    items.Add((
                                        Convert.ToInt32(reader["ProductID"]),
                                        Convert.ToInt32(reader["Quantity"])
                                    ));
                                }
                            }
                        }

                        foreach (var item in items)
                        {
                            string stockQuery = @"
                        UPDATE Products
                        SET StockQuantity = StockQuantity + @Quantity
                        WHERE ProductID = @ProductID";

                            using (SqlCommand cmd =
                                new SqlCommand(stockQuery, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                cmd.Parameters.AddWithValue("@ProductID", item.ProductID);

                                if (cmd.ExecuteNonQuery() != 1)
                                    throw new InvalidOperationException(
                                        "Product not found while restoring stock.");
                            }

                            string transactionQuery = @"
                        INSERT INTO StockTransactions
                        (ProductID, OrderID, CreatedBy,
                         TransactionType, Quantity)
                        VALUES
                        (@ProductID, @OrderID, @CreatedBy,
                         'ReturnIn', @Quantity)";

                            using (SqlCommand cmd =
                                new SqlCommand(transactionQuery, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ProductID", item.ProductID);
                                cmd.Parameters.AddWithValue("@OrderID", id);
                                cmd.Parameters.AddWithValue("@CreatedBy", userId);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);

                                cmd.ExecuteNonQuery();
                            }
                        }

                        string updateQuery = @"
                    UPDATE Orders
                    SET OrderStatus = 'Cancelled'
                    WHERE OrderID = @OrderID
                    AND UserID = @UserID
                    AND OrderStatus = 'Pending'";

                        using (SqlCommand cmd =
                            new SqlCommand(updateQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderID", id);
                            cmd.Parameters.AddWithValue("@UserID", userId);

                            if (cmd.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException(
                                    "Unable to update order status.");
                        }

                        transaction.Commit();

                        return Ok("Order cancelled and stock restored successfully.");
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();

                        return StatusCode(500,
                            "Unable to cancel order. No changes were saved.");
                    }
                }
            }
        }


        [HttpPut("status/{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateOrderStatus(
    int id, UpdateOrderStatusDTO dto)
        {
            string newStatus = dto.OrderStatus?.Trim() ?? "";

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        string query = @"
                    SELECT OrderStatus
                    FROM Orders WITH (UPDLOCK, HOLDLOCK)
                    WHERE OrderID = @OrderID";

                        string? currentStatus;

                        using (SqlCommand cmd =
                            new SqlCommand(query, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderID", id);
                            currentStatus = cmd.ExecuteScalar()?.ToString();
                        }

                        if (currentStatus == null)
                        {
                            transaction.Rollback();
                            return NotFound("Order not found.");
                        }

                        bool validTransition =
                            (currentStatus == "Pending" &&
                             newStatus == "Confirmed") ||
                            (currentStatus == "Confirmed" &&
                             newStatus == "Processing") ||
                            (currentStatus == "Processing" &&
                             newStatus == "Shipped") ||
                            (currentStatus == "Shipped" &&
                             newStatus == "Delivered");

                        if (!validTransition)
                        {
                            transaction.Rollback();
                            return BadRequest(
                                $"Cannot change order status from {currentStatus} to {newStatus}.");
                        }

                        string updateQuery = @"
                    UPDATE Orders
                    SET OrderStatus = @OrderStatus
                    WHERE OrderID = @OrderID";

                        using (SqlCommand cmd =
                            new SqlCommand(updateQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderStatus", newStatus);
                            cmd.Parameters.AddWithValue("@OrderID", id);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        return Ok(new
                        {
                            Message = "Order status updated successfully.",
                            OrderID = id,
                            OrderStatus = newStatus
                        });
                    }
                    catch
                    {
                        transaction.Rollback();
                        return StatusCode(500, "Unable to update order status.");
                    }
                }
            }
        }
    }
}
