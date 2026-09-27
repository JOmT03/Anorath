using Anorath.domain.Entities.Operations;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    public record SaleRequest(int ProductId, int? CustomerId, int Quantity, string PaymentMethod);

    [ApiController]
    [Route("sales")]
    public class SalesController : ControllerBase
    {
        private static readonly string[] PaymentMethods = { "Cash", "Card", "Charge to Room" };

        private readonly TenantErpDbContext _db;
        public SalesController(TenantErpDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var (start, endExclusive) = RevenueRules.Range(from, to);

            try
            {
                var products = await _db.Products.AsNoTracking().ToDictionaryAsync(p => p.ProductId, p => p.ProductName);
                var customers = await _db.Customers.AsNoTracking().ToDictionaryAsync(c => c.CustomerId, c => c.CustomerName);
                var sales = await _db.Sales.AsNoTracking()
                    .Where(s => s.SaleDate >= start && s.SaleDate < endExclusive)
                    .OrderByDescending(s => s.SaleDate)
                    .ToListAsync();

                var result = sales.Select(s => new
                {
                    s.SaleId,
                    s.SaleDate,
                    ItemName = products.TryGetValue(s.ProductId, out var p) ? p : "(deleted item)",
                    s.Quantity,
                    s.UnitPrice,
                    s.Total,
                    Guest = s.CustomerId.HasValue && customers.TryGetValue(s.CustomerId.Value, out var c) ? c : "Walk-in",
                    s.PaymentMethod
                });

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to load sales.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaleRequest req)
        {
            if (req.Quantity <= 0)
                return BadRequest("Quantity must be greater than zero.");
            if (!PaymentMethods.Contains(req.PaymentMethod))
                return BadRequest("Payment method must be Cash, Card, or Charge to Room.");
            if (req.PaymentMethod == "Charge to Room" && req.CustomerId is null)
                return BadRequest("Select a guest to charge this to their room.");

            var product = await _db.Products.FindAsync(req.ProductId);
            if (product is null || !product.IsActive)
                return BadRequest("A valid, active item is required.");

            if (req.CustomerId.HasValue && !await _db.Customers.AnyAsync(c => c.CustomerId == req.CustomerId.Value))
                return BadRequest("Selected guest was not found.");

            try
            {
                // Price always comes from the database, never from the client
                var sale = new Sale
                {
                    ProductId = product.ProductId,
                    CustomerId = req.CustomerId,
                    SaleDate = DateTime.Now,
                    Quantity = req.Quantity,
                    UnitPrice = product.Price,
                    Total = Math.Round(product.Price * req.Quantity, 2),
                    PaymentMethod = req.PaymentMethod
                };

                _db.Sales.Add(sale);
                await _db.SaveChangesAsync();
                return Created($"/sales/{sale.SaleId}", sale);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to record sale.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var sale = await _db.Sales.FindAsync(id);
            if (sale is null) return NotFound();

            try
            {
                _db.Sales.Remove(sale);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete sale.");
            }
        }
    }
}