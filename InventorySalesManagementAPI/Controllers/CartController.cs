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
    [Authorize(Roles = "Shopkeeper")]
    public class CartController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public CartController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpPost("add")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult AddToCart(AddToCartDTO dto)
        {
            int userId = Convert.ToInt32(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );

            if (dto.Quantity <= 0)
                return BadRequest("Quantity must be greater than zero.");

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        string productQuery = @"SELECT StockQuantity,
                                              MinOrderQuantity,
                                              MaxOrderQuantity
                                       FROM Products
                                       WHERE ProductID = @ProductID";

                        int stock, minQty, maxQty;

                        using (SqlCommand cmd = new SqlCommand(productQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ProductID", dto.ProductID);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                    return NotFound("Product not found.");

                                stock = Convert.ToInt32(reader["StockQuantity"]);
                                minQty = Convert.ToInt32(reader["MinOrderQuantity"]);
                                maxQty = Convert.ToInt32(reader["MaxOrderQuantity"]);
                            }
                        }

                        string cartQuery = @"SELECT CartID FROM Cart
                                     WHERE UserID = @UserID";

                        int cartId;

                        using (SqlCommand cmd = new SqlCommand(cartQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserID", userId);
                            object? result = cmd.ExecuteScalar();

                            if (result == null)
                            {
                                string createCart = @"INSERT INTO Cart (UserID)
                                              OUTPUT INSERTED.CartID
                                              VALUES (@UserID)";

                                using (SqlCommand createCmd = new SqlCommand(createCart, con, transaction))
                                {
                                    createCmd.Parameters.AddWithValue("@UserID", userId);
                                    cartId = Convert.ToInt32(createCmd.ExecuteScalar());
                                }
                            }
                            else
                            {
                                cartId = Convert.ToInt32(result);
                            }
                        }

                        string existingQuery = @"SELECT Quantity FROM CartItems
                                         WHERE CartID = @CartID
                                         AND ProductID = @ProductID";

                        int existingQty = 0;

                        using (SqlCommand cmd = new SqlCommand(existingQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CartID", cartId);
                            cmd.Parameters.AddWithValue("@ProductID", dto.ProductID);

                            object? result = cmd.ExecuteScalar();

                            if (result != null)
                                existingQty = Convert.ToInt32(result);
                        }

                        int totalQty = existingQty + dto.Quantity;

                        if (totalQty < minQty || totalQty > maxQty || totalQty > stock)
                            return BadRequest("Quantity does not meet product order limits or available stock.");

                        string insertQuery = existingQty > 0
                            ? @"UPDATE CartItems SET Quantity = @Quantity
                        WHERE CartID = @CartID AND ProductID = @ProductID"
                            : @"INSERT INTO CartItems (CartID, ProductID, Quantity)
                        VALUES (@CartID, @ProductID, @Quantity)";

                        using (SqlCommand cmd = new SqlCommand(insertQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CartID", cartId);
                            cmd.Parameters.AddWithValue("@ProductID", dto.ProductID);
                            cmd.Parameters.AddWithValue("@Quantity", totalQty);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        return Ok("Product added to cart successfully.");
                    }
                    catch
                    {
                        transaction.Rollback();
                        return StatusCode(500, "Unable to add product to cart.");
                    }
                }
            }
        }

        [HttpGet]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult GetCart()
        {
            int userId = Convert.ToInt32(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            SELECT
                ci.CartItemID,
                p.ProductID,
                p.ProductName,
                p.SKU,
                p.WholesalePrice,
                ci.Quantity,
                (p.WholesalePrice * ci.Quantity) AS TotalPrice
            FROM Cart c
            INNER JOIN CartItems ci ON c.CartID = ci.CartID
            INNER JOIN Products p ON ci.ProductID = p.ProductID
            WHERE c.UserID = @UserID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<object> cartItems = new List<object>();

                        while (reader.Read())
                        {
                            cartItems.Add(new
                            {
                                CartItemID = Convert.ToInt32(reader["CartItemID"]),
                                ProductID = Convert.ToInt32(reader["ProductID"]),
                                ProductName = reader["ProductName"].ToString(),
                                SKU = reader["SKU"].ToString(),
                                WholesalePrice = Convert.ToDecimal(reader["WholesalePrice"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                TotalPrice = Convert.ToDecimal(reader["TotalPrice"])
                            });
                        }

                        return Ok(cartItems);
                    }
                }
            }
        }

        [HttpPut("update/{cartItemId}")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult UpdateCartQuantity(int cartItemId, AddToCartDTO dto)
        {
            if (dto.Quantity <= 0)
                return BadRequest("Quantity must be greater than zero.");

            int userId = Convert.ToInt32(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            UPDATE ci
            SET ci.Quantity = @Quantity
            FROM CartItems ci
            INNER JOIN Cart c ON ci.CartID = c.CartID
            INNER JOIN Products p ON ci.ProductID = p.ProductID
            WHERE ci.CartItemID = @CartItemID
              AND c.UserID = @UserID
              AND @Quantity BETWEEN p.MinOrderQuantity
                                AND p.MaxOrderQuantity
              AND @Quantity <= p.StockQuantity";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Quantity", dto.Quantity);
                    cmd.Parameters.AddWithValue("@CartItemID", cartItemId);
                    cmd.Parameters.AddWithValue("@UserID", userId);

                    int rows = cmd.ExecuteNonQuery();

                    if (rows == 0)
                        return BadRequest(
                            "Cart item not found or quantity exceeds product limits."
                        );

                    return Ok("Cart quantity updated successfully.");
                }
            }
        }



        [HttpDelete("remove/{cartItemId}")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult RemoveFromCart(int cartItemId)
        {
            int userId = Convert.ToInt32(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            DELETE ci
            FROM CartItems ci
            INNER JOIN Cart c ON ci.CartID = c.CartID
            WHERE ci.CartItemID = @CartItemID
              AND c.UserID = @UserID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CartItemID", cartItemId);
                    cmd.Parameters.AddWithValue("@UserID", userId);

                    int rows = cmd.ExecuteNonQuery();

                    if (rows == 0)
                        return NotFound("Cart item not found.");

                    return Ok("Product removed from cart successfully.");
                }
            }
        }




        [HttpDelete("clear")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult ClearCart()
        {
            int userId = Convert.ToInt32(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"
            DELETE ci
            FROM CartItems ci
            INNER JOIN Cart c ON ci.CartID = c.CartID
            WHERE c.UserID = @UserID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);

                    int rows = cmd.ExecuteNonQuery();

                    return Ok(new
                    {
                        Message = "Cart cleared successfully.",
                        RemovedItems = rows
                    });
                }
            }
        }




    }
}
