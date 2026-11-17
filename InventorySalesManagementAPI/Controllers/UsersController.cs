using Microsoft.AspNetCore.Authorization;
using InventorySalesManagementAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InventorySalesManagementAPI.Models;
using InventorySalesManagementAPI.DataAccess;
using System.Security.Cryptography;
using InventorySalesManagementAPI.DTOs;
using System.Security.Cryptography;
using InventorySalesManagementAPI.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace InventorySalesManagementAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly DBConnection _dbConnection;
        private readonly IConfiguration _configuration;

        public UsersController(DBConnection dbConnection, IConfiguration configuration)
        {
            _dbConnection = dbConnection;
            _configuration = configuration;
        }

        private string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100000,
                HashAlgorithmName.SHA256,
                32
            );

            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public IActionResult Register(UserModel user)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"INSERT INTO Users
                    (FullName, Email, PasswordHash, Phone, BusinessName, Role, ApprovalStatus)
                    VALUES
                    (@FullName, @Email, @PasswordHash, @Phone, @BusinessName, 'Shopkeeper', 'Pending')";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@FullName", user.FullName);
                    cmd.Parameters.AddWithValue("@Email", user.Email);
                    cmd.Parameters.AddWithValue("@PasswordHash", HashPassword(user.PasswordHash));
                    cmd.Parameters.AddWithValue("@Phone", user.Phone);
                    cmd.Parameters.AddWithValue("@BusinessName",
                        string.IsNullOrWhiteSpace(user.BusinessName)
                            ? DBNull.Value
                            : user.BusinessName);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Shopkeeper Registered Successfully. Waiting for Admin Approval.");
                    }

                    return BadRequest("Registration Failed.");
                }
            }
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            string[] parts = storedHash.Split('.');

            if (parts.Length != 2)
                return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] originalHash = Convert.FromBase64String(parts[1]);

            byte[] newHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100000,
                HashAlgorithmName.SHA256,
                32
            );

            return CryptographicOperations.FixedTimeEquals(
                originalHash, newHash);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login(LoginDTO login)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"SELECT UserID, FullName, Email,
                         PasswordHash, Role, ApprovalStatus
                         FROM Users
                         WHERE Email = @Email";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Email", login.Email);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return Unauthorized("Invalid Email or Password");
                        }

                        string storedHash = reader["PasswordHash"].ToString() ?? "";

                        if (!VerifyPassword(login.Password, storedHash))
                        {
                            return Unauthorized("Invalid Email or Password");
                        }

                        string role = reader["Role"].ToString() ?? "";
                        string approvalStatus =
                            reader["ApprovalStatus"].ToString() ?? "";

                        if (role == "Shopkeeper" && approvalStatus != "Approved")
                        {
                            return Unauthorized(
                                "Your account is waiting for Admin Approval.");
                        }

                        int userId = Convert.ToInt32(reader["UserID"]);
                        string email = reader["Email"].ToString() ?? "";
                        string fullName = reader["FullName"].ToString() ?? "";

                        string token = GenerateJwtToken(userId, email, fullName, role);

                        return Ok(new
                        {
                            UserID = userId,
                            FullName = fullName,
                            Email = email,
                            Role = role,
                            Token = token,
                            Message = "Login Successful"
                        });
                    }
                }
            }
        }

        private string GenerateJwtToken(int userId, string email, string fullName, string role)
        {
            var claims = new[]
            {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Email, email),
        new Claim(ClaimTypes.Name, fullName),
        new Claim(ClaimTypes.Role, role)
    };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)
            );

            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(_configuration["Jwt:ExpiryMinutes"])
                ),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetPendingShopkeepers()
        {
            List<PendingShopkeeperDTO> shopkeepers = new List<PendingShopkeeperDTO>();

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"SELECT UserID, FullName, Email, Phone,
                         BusinessName, ApprovalStatus
                         FROM Users
                         WHERE Role = 'Shopkeeper'
                         AND ApprovalStatus = 'Pending'";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        PendingShopkeeperDTO shopkeeper = new PendingShopkeeperDTO
                        {
                            UserID = Convert.ToInt32(reader["UserID"]),
                            FullName = reader["FullName"].ToString()!,
                            Email = reader["Email"].ToString()!,
                            Phone = reader["Phone"].ToString()!,
                            BusinessName = reader["BusinessName"] == DBNull.Value
                                ? ""
                                : reader["BusinessName"].ToString()!,
                            ApprovalStatus = reader["ApprovalStatus"].ToString()!
                        };

                        shopkeepers.Add(shopkeeper);
                    }
                }
            }

            return Ok(shopkeepers);
        }

        [HttpPut("approve/{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult ApproveShopkeeper(int id)
        {
            int adminId = Convert.ToInt32(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"UPDATE Users
                         SET ApprovalStatus = 'Approved',
                             ApprovedBy = @AdminID
                         WHERE UserID = @UserID
                         AND Role = 'Shopkeeper'
                         AND ApprovalStatus = 'Pending'";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@AdminID", adminId);
                    cmd.Parameters.AddWithValue("@UserID", id);

                    int rows = cmd.ExecuteNonQuery();

                    if (rows == 0)
                        return NotFound("Pending shopkeeper not found.");

                    return Ok("Shopkeeper approved successfully.");
                }
            }
        }

        [HttpPut("reject/{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult RejectShopkeeper(int id)
        {
            int adminId = Convert.ToInt32(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"UPDATE Users
                         SET ApprovalStatus = 'Rejected',
                             ApprovedBy = @AdminID
                         WHERE UserID = @UserID
                         AND Role = 'Shopkeeper'
                         AND ApprovalStatus = 'Pending'";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@AdminID", adminId);
                    cmd.Parameters.AddWithValue("@UserID", id);

                    int rows = cmd.ExecuteNonQuery();

                    if (rows == 0)
                        return NotFound("Pending shopkeeper not found.");

                    return Ok("Shopkeeper rejected successfully.");
                }
            }
        }


    }
}
