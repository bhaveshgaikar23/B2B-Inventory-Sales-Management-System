using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InventorySalesManagementAPI.Models;
using InventorySalesManagementAPI.DataAccess;
using Microsoft.AspNetCore.Authorization;

namespace InventorySalesManagementAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public ProductsController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddProduct(ProductModel product)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"INSERT INTO Products
                    (CategoryID, SupplierID, ProductName, SKU,
                     WholesalePrice, StockQuantity,
                     MinOrderQuantity, MaxOrderQuantity)
                    VALUES
                    (@CategoryID, @SupplierID, @ProductName, @SKU,
                     @WholesalePrice, @StockQuantity,
                     @MinOrderQuantity, @MaxOrderQuantity)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryID", product.CategoryID);
                    cmd.Parameters.AddWithValue("@SupplierID", product.SupplierID);
                    cmd.Parameters.AddWithValue("@ProductName", product.ProductName);
                    cmd.Parameters.AddWithValue("@SKU", product.SKU);
                    cmd.Parameters.AddWithValue("@WholesalePrice", product.WholesalePrice);
                    cmd.Parameters.AddWithValue("@StockQuantity", 0);
                    cmd.Parameters.AddWithValue("@MinOrderQuantity", product.MinOrderQuantity);
                    cmd.Parameters.AddWithValue("@MaxOrderQuantity", product.MaxOrderQuantity);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Product Added Successfully");
                    }
                    else
                    {
                        return BadRequest("Product Not Added");
                    }
                }
            }
        }


        [HttpGet]
        [Authorize(Roles = "Admin,Shopkeeper")]
        public IActionResult GetProducts()
        {
            List<ProductModel> products = new List<ProductModel>();

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "SELECT * FROM Products";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ProductModel product = new ProductModel();

                            product.ProductID = Convert.ToInt32(reader["ProductID"]);
                            product.CategoryID = Convert.ToInt32(reader["CategoryID"]);
                            product.SupplierID = Convert.ToInt32(reader["SupplierID"]);
                            product.ProductName = reader["ProductName"].ToString() ?? "";
                            product.SKU = reader["SKU"].ToString() ?? "";
                            product.WholesalePrice = Convert.ToDecimal(reader["WholesalePrice"]);
                            product.StockQuantity = Convert.ToInt32(reader["StockQuantity"]);
                            product.MinOrderQuantity = Convert.ToInt32(reader["MinOrderQuantity"]);
                            product.MaxOrderQuantity = Convert.ToInt32(reader["MaxOrderQuantity"]);

                            products.Add(product);
                        }
                    }
                }
            }

            return Ok(products);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Shopkeeper")]
        public IActionResult GetProductById(int id)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "SELECT * FROM Products WHERE ProductID = @ProductID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@ProductID", id);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ProductModel product = new ProductModel();

                            product.ProductID = Convert.ToInt32(reader["ProductID"]);
                            product.CategoryID = Convert.ToInt32(reader["CategoryID"]);
                            product.SupplierID = Convert.ToInt32(reader["SupplierID"]);
                            product.ProductName = reader["ProductName"].ToString() ?? "";
                            product.SKU = reader["SKU"].ToString() ?? "";
                            product.WholesalePrice = Convert.ToDecimal(reader["WholesalePrice"]);
                            product.StockQuantity = Convert.ToInt32(reader["StockQuantity"]);
                            product.MinOrderQuantity = Convert.ToInt32(reader["MinOrderQuantity"]);
                            product.MaxOrderQuantity = Convert.ToInt32(reader["MaxOrderQuantity"]);

                            return Ok(product);
                        }
                        else
                        {
                            return NotFound("Product Not Found");
                        }
                    }
                }
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateProduct(int id, ProductModel product)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = @"UPDATE Products SET
                 CategoryID = @CategoryID,
                 SupplierID = @SupplierID,
                 ProductName = @ProductName,
                 SKU = @SKU,
                 WholesalePrice = @WholesalePrice,
                 MinOrderQuantity = @MinOrderQuantity,
                 MaxOrderQuantity = @MaxOrderQuantity
                 WHERE ProductID = @ProductID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@ProductID", id);
                    cmd.Parameters.AddWithValue("@CategoryID", product.CategoryID);
                    cmd.Parameters.AddWithValue("@SupplierID", product.SupplierID);
                    cmd.Parameters.AddWithValue("@ProductName", product.ProductName);
                    cmd.Parameters.AddWithValue("@SKU", product.SKU);
                    cmd.Parameters.AddWithValue("@WholesalePrice", product.WholesalePrice);
                    
                    cmd.Parameters.AddWithValue("@MinOrderQuantity", product.MinOrderQuantity);
                    cmd.Parameters.AddWithValue("@MaxOrderQuantity", product.MaxOrderQuantity);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Product Updated Successfully");
                    }
                    else
                    {
                        return NotFound("Product Not Found");
                    }
                }
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteProduct(int id)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "DELETE FROM Products WHERE ProductID = @ProductID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@ProductID", id);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Product Deleted Successfully");
                    }
                    else
                    {
                        return NotFound("Product Not Found");
                    }
                }
            }
        }
    }
}
