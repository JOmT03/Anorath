using Anorath.domain.Entities;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    [ApiController]
    [Route("products")]
    public class ProductsController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public ProductsController(TenantErpDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = await _db.Products.AsNoTracking().ToListAsync();
            return Ok(products);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            if (string.IsNullOrWhiteSpace(product.ProductCode) || string.IsNullOrWhiteSpace(product.ProductName))
                return BadRequest("ProductCode and ProductName are required.");

            try
            {
                _db.Products.Add(product);
                await _db.SaveChangesAsync();
                return Created($"/products/{product.ProductId}", product);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create product.");
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Product updated)
        {
            var product = await _db.Products.FindAsync(id);
            if (product is null) return NotFound();

            if (string.IsNullOrWhiteSpace(updated.ProductCode) || string.IsNullOrWhiteSpace(updated.ProductName))
                return BadRequest("ProductCode and ProductName are required.");

            try
            {
                product.ProductCode = updated.ProductCode;
                product.ProductName = updated.ProductName;
                product.Price = updated.Price;
                product.IsActive = updated.IsActive;
                await _db.SaveChangesAsync();
                return Ok(product);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update product.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product is null) return NotFound();

            try
            {
                _db.Products.Remove(product);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete product.");
            }
        }
    }
}