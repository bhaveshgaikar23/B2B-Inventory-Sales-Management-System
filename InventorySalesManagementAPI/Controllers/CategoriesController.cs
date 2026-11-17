using InventorySalesManagementAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InventorySalesManagementAPI.DataAccess;
using Microsoft.AspNetCore.Authorization;

namespace InventorySalesManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly DBConnection _dbConnection;

        public CategoriesController(DBConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddCategory(CategoryModel category)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "insert into Categories (CategoryName) values (@CategoryName)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryName", category.CategoryName);

                    int res = cmd.ExecuteNonQuery();

                    if (res > 0)
                    {
                        return Ok("Category Added Successfully");
                    }
                    else
                    {
                        return BadRequest("Category Not Added");
                    }
                }
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Shopkeeper")]
        public IActionResult GetCategories()
        {
            List<CategoryModel> categories = new List<CategoryModel>();

            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "SELECT * FROM Categories";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CategoryModel category = new CategoryModel();

                            category.CategoryID = Convert.ToInt32(reader["CategoryID"]);
                            category.CategoryName = reader["CategoryName"].ToString() ?? "";

                            categories.Add(category);
                        }
                    }
                }
            }

            return Ok(categories);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Shopkeeper")]
        public IActionResult GetCategoryById(int id)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "SELECT * FROM Categories WHERE CategoryID = @CategoryID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryID", id);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            CategoryModel category = new CategoryModel();

                            category.CategoryID = Convert.ToInt32(reader["CategoryID"]);
                            category.CategoryName = reader["CategoryName"].ToString() ?? "";

                            return Ok(category);
                        }
                        else
                        {
                            return NotFound("Category Not Found");
                        }
                    }
                }
            }
        }


        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateCategory(int id, CategoryModel category)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "UPDATE Categories SET CategoryName = @CategoryName WHERE CategoryID = @CategoryID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryID", id);
                    cmd.Parameters.AddWithValue("@CategoryName", category.CategoryName);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Category Updated Successfully");
                    }
                    else
                    {
                        return NotFound("Category Not Found");
                    }
                }
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteCategory(int id)
        {
            using (SqlConnection con = _dbConnection.GetConnection())
            {
                con.Open();

                string query = "DELETE FROM Categories WHERE CategoryID = @CategoryID";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryID", id);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Ok("Category Deleted Successfully");
                    }
                    else
                    {
                        return NotFound("Category Not Found");
                    }
                }
            }
        }

    }
}
