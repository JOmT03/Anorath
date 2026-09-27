using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    public record ProductRequest(string ProductName, decimal Price, int StockQuantity, bool IsActive);

    // =====================================================
    // MENU ITEMS (Restaurant products)
    // View: Admin, Manager, BranchManager, Cashier
    // Add / Edit / Delete: Admin, Manager only
    // =====================================================
    [ApiController]
    [Route("products")]
    [Authorize(Roles = "Admin,Manager,BranchManager,Cashier")]
    public class ProductsController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public ProductsController(TenantErpDbContext db) => _db = db;

        // Creates a new entity of the same type as the DbSet (no need to know its namespace)
        private static T NewOf<T>(DbSet<T> _) where T : class, new() => new T();

        // GET products?search=burger
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search)
        {
            try
            {
                var q = _db.Products.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim();
                    q = q.Where(p => p.ProductName.Contains(s) || p.ProductCode.Contains(s));
                }
                return Ok(await q.OrderBy(p => p.ProductName).ToListAsync());
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to load menu items.");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create([FromBody] ProductRequest req)
        {
            var error = Validate(req);
            if (error != null) return BadRequest(error);

            var name = req.ProductName.Trim();
            if (await _db.Products.AnyAsync(p => p.ProductName == name))
                return BadRequest("A menu item with this name already exists.");

            // Auto-generate MENU-0001, MENU-0002, ...
            var next = await _db.Products.CountAsync() + 1;
            var code = $"MENU-{next:0000}";
            while (await _db.Products.AnyAsync(p => p.ProductCode == code))
            {
                next++;
                code = $"MENU-{next:0000}";
            }

            try
            {
                var product = NewOf(_db.Products);
                product.ProductCode = code;
                product.ProductName = name;
                product.Price = Math.Round(req.Price, 2);
                product.StockQuantity = req.StockQuantity;
                product.IsActive = req.IsActive;

                _db.Products.Add(product);
                await _db.SaveChangesAsync();
                return Created($"/products/{product.ProductId}", product);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to add menu item.");
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductRequest req)
        {
            var product = await _db.Products.FindAsync(id);
            if (product is null) return NotFound("Menu item not found.");

            var error = Validate(req);
            if (error != null) return BadRequest(error);

            var name = req.ProductName.Trim();
            if (await _db.Products.AnyAsync(p => p.ProductId != id && p.ProductName == name))
                return BadRequest("A menu item with this name already exists.");

            try
            {
                product.ProductName = name;
                product.Price = Math.Round(req.Price, 2);
                product.StockQuantity = req.StockQuantity;
                product.IsActive = req.IsActive;
                await _db.SaveChangesAsync();
                return Ok(product);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update menu item.");
            }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product is null) return NotFound("Menu item not found.");

            if (await _db.Sales.AnyAsync(s => s.ProductId == id))
                return BadRequest("This item already has sales. Set it to inactive instead of deleting.");

            try
            {
                _db.Products.Remove(product);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete menu item.");
            }
        }

        private static string? Validate(ProductRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.ProductName) || r.ProductName.Trim().Length < 2)
                return "Item name is required.";
            if (r.ProductName.Trim().Length > 150) return "Item name is too long (max 150).";
            if (r.Price <= 0) return "Price must be greater than zero.";
            if (r.Price > 100000) return "Price is too high.";
            if (r.StockQuantity < 0) return "Stock cannot be negative.";
            return null;
        }
    }
}