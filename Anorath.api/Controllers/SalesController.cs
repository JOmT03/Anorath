using Anorath.domain.Entities.Operations;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    public record SaleRequest(int ProductId, int? CustomerId, int Quantity, string PaymentMethod);

    [ApiController]
    [Route("sales")]
    [Authorize(Roles = "Admin,Manager,BranchManager,Cashier")]
    public class SalesController : ControllerBase
    {
        private static readonly string[] PaymentMethods = { "Cash", "Card", "Charge to Room" };

        private readonly TenantErpDbContext _db;
        public SalesController(TenantErpDbContext db) => _db = db;

        // GET sales?from=2026-09-28&to=2026-09-28
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

        // Guests who can "Charge to Room" = currently checked in
        [HttpGet("guests")]
        public async Task<IActionResult> CheckedInGuests()
        {
            var guests = await (
                from r in _db.Reservations.AsNoTracking()
                join c in _db.Customers.AsNoTracking() on r.CustomerId equals c.CustomerId
                join rm in _db.Rooms.AsNoTracking() on r.RoomId equals rm.RoomId
                where r.Status == "CheckedIn"
                orderby rm.RoomNumber
                select new { customerId = c.CustomerId, customerName = c.CustomerName, roomNumber = rm.RoomNumber }
            ).ToListAsync();

            return Ok(guests);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaleRequest req)
        {
            if (req.Quantity <= 0 || req.Quantity > 100)
                return BadRequest("Quantity must be between 1 and 100.");
            if (!PaymentMethods.Contains(req.PaymentMethod))
                return BadRequest("Payment method must be Cash, Card, or Charge to Room.");

            var product = await _db.Products.FindAsync(req.ProductId);
            if (product is null || !product.IsActive)
                return BadRequest("Please choose an active menu item.");
            if (product.StockQuantity < req.Quantity)
                return BadRequest($"Only {product.StockQuantity} left of {product.ProductName}.");

            int? customerId = null;
            if (req.PaymentMethod == "Charge to Room")
            {
                if (req.CustomerId is null)
                    return BadRequest("Select a checked-in guest to charge to their room.");

                var checkedIn = await _db.Reservations.AnyAsync(r => r.CustomerId == req.CustomerId && r.Status == "CheckedIn");
                if (!checkedIn)
                    return BadRequest("Only checked-in guests can charge to their room.");

                customerId = req.CustomerId;
            }

            try
            {
                // Price always comes from the database, never from the client
                var sale = new Sale
                {
                    ProductId = product.ProductId,
                    CustomerId = customerId,
                    SaleDate = DateTime.Now,
                    Quantity = req.Quantity,
                    UnitPrice = product.Price,
                    Total = Math.Round(product.Price * req.Quantity, 2),
                    PaymentMethod = req.PaymentMethod
                };

                product.StockQuantity -= req.Quantity;   // inventory goes down

                _db.Sales.Add(sale);
                await _db.SaveChangesAsync();
                return Created($"/sales/{sale.SaleId}", new { id = sale.SaleId, total = sale.Total });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to record sale.");
            }
        }

        // Void a sale: Admin/Manager only, stock is returned
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var sale = await _db.Sales.FindAsync(id);
            if (sale is null) return NotFound("Sale not found.");

            try
            {
                var product = await _db.Products.FindAsync(sale.ProductId);
                if (product != null) product.StockQuantity += sale.Quantity;

                _db.Sales.Remove(sale);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to void sale.");
            }
        }
    }
}