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
    public class PaymentsController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public PaymentsController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpPost("create")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult CreatePayment(CreatePaymentDTO dto)
        {
            if (!int.TryParse(
    User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
    out int userId))
            {
                return Unauthorized();
            }

            if (dto.PaymentMethod != "COD" &&
                dto.PaymentMethod != "Online")
            {
                return BadRequest("Invalid payment method. Use COD or Online.");
            }

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        string orderQuery = @"
                    SELECT TotalAmount
                    FROM Orders
                    WHERE OrderID = @OrderID
                      AND UserID = @UserID
                      AND OrderStatus <> 'Cancelled'";

                        decimal amount;

                        using (SqlCommand cmd =
                            new SqlCommand(orderQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderID", dto.OrderID);
                            cmd.Parameters.AddWithValue("@UserID", userId);

                            object? result = cmd.ExecuteScalar();

                            if (result == null)
                            {
                                transaction.Rollback();
                                return NotFound("Order not found or cancelled.");
                            }

                            amount = Convert.ToDecimal(result);
                        }

                        string checkQuery = @"
                    SELECT COUNT(*)
                    FROM Payments
                    WHERE OrderID = @OrderID";

                        using (SqlCommand cmd =
                            new SqlCommand(checkQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderID", dto.OrderID);

                            int count = Convert.ToInt32(cmd.ExecuteScalar());

                            if (count > 0)
                            {
                                transaction.Rollback();
                                return BadRequest(
                                    "Payment already exists for this order.");
                            }
                        }

                        string insertQuery = @"
                    INSERT INTO Payments
                        (OrderID, PaymentMethod, PaymentStatus, Amount)
                    OUTPUT INSERTED.PaymentID
                    VALUES
                        (@OrderID, @PaymentMethod, 'Pending', @Amount)";

                        int paymentId;

                        using (SqlCommand cmd =
                            new SqlCommand(insertQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderID", dto.OrderID);
                            cmd.Parameters.AddWithValue("@PaymentMethod", dto.PaymentMethod);
                            cmd.Parameters.AddWithValue("@Amount", amount);

                            paymentId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        transaction.Commit();

                        return Ok(new
                        {
                            Message = "Payment created successfully.",
                            PaymentID = paymentId,
                            OrderID = dto.OrderID,
                            PaymentMethod = dto.PaymentMethod,
                            PaymentStatus = "Pending",
                            Amount = amount
                        });
                    }
                    catch (SqlException ex)
                        when (ex.Number == 2601 || ex.Number == 2627)
                    {
                        transaction.Rollback();
                        return Conflict("Payment already exists for this order.");
                    }
                    catch
                    {
                        transaction.Rollback();
                        return StatusCode(500, "Unable to create payment.");
                    }
                }
            }
        }


        [HttpGet("{orderId}")]
        [Authorize(Roles = "Shopkeeper")]
        public IActionResult GetPaymentDetails(int orderId)
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
            SELECT
                p.PaymentID,
                p.OrderID,
                o.OrderNumber,
                p.PaymentMethod,
                p.PaymentStatus,
                p.Amount
            FROM Payments p
            INNER JOIN Orders o ON p.OrderID = o.OrderID
            WHERE p.OrderID = @OrderID
              AND o.UserID = @UserID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@OrderID", orderId);
                    cmd.Parameters.AddWithValue("@UserID", userId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return NotFound("Payment details not found.");

                        return Ok(new
                        {
                            PaymentID = Convert.ToInt32(reader["PaymentID"]),
                            OrderID = Convert.ToInt32(reader["OrderID"]),
                            OrderNumber = reader["OrderNumber"].ToString(),
                            PaymentMethod = reader["PaymentMethod"].ToString(),
                            PaymentStatus = reader["PaymentStatus"].ToString(),
                            Amount = Convert.ToDecimal(reader["Amount"])
                        });
                    }
                }
            }
        }

        [HttpPut("status/{paymentId}")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdatePaymentStatus(
    int paymentId, UpdatePaymentStatusDTO dto)
        {
            string newStatus = dto.PaymentStatus?.Trim() ?? "";

            if (newStatus != "Paid" && newStatus != "Failed")
                return BadRequest("Payment status must be Paid or Failed.");

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        string checkQuery = @"
                    SELECT p.PaymentStatus, o.OrderStatus
                    FROM Payments p WITH (UPDLOCK, HOLDLOCK)
                    INNER JOIN Orders o ON p.OrderID = o.OrderID
                    WHERE p.PaymentID = @PaymentID";

                        string? currentStatus = null;
                        string? orderStatus = null;

                        using (SqlCommand cmd =
                            new SqlCommand(checkQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@PaymentID", paymentId);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    currentStatus =
                                        reader["PaymentStatus"].ToString();

                                    orderStatus =
                                        reader["OrderStatus"].ToString();
                                }
                            }
                        }

                        if (currentStatus == null)
                        {
                            transaction.Rollback();
                            return NotFound("Payment not found.");
                        }

                        if (orderStatus == "Cancelled")
                        {
                            transaction.Rollback();
                            return BadRequest(
                                "Cannot update payment for a cancelled order.");
                        }

                        if (currentStatus != "Pending")
                        {
                            transaction.Rollback();
                            return BadRequest(
                                "Only pending payments can be updated.");
                        }

                        string updateQuery = @"
                    UPDATE Payments
                    SET PaymentStatus = @PaymentStatus
                    WHERE PaymentID = @PaymentID
                      AND PaymentStatus = 'Pending'";

                        using (SqlCommand cmd =
                            new SqlCommand(updateQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue(
                                "@PaymentStatus", newStatus);

                            cmd.Parameters.AddWithValue(
                                "@PaymentID", paymentId);

                            if (cmd.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException(
                                    "Unable to update payment.");
                        }

                        transaction.Commit();

                        return Ok(new
                        {
                            Message = "Payment status updated successfully.",
                            PaymentID = paymentId,
                            PaymentStatus = newStatus
                        });
                    }
                    catch
                    {
                        transaction.Rollback();
                        return StatusCode(500,
                            "Unable to update payment status.");
                    }
                }
            }
        }
    }
}
