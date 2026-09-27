using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    [ApiController]
    [Route("dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public DashboardController(TenantErpDbContext db) => _db = db;

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var rooms = await _db.Rooms.AsNoTracking().ToListAsync();
                var rates = rooms.ToDictionary(r => r.RoomId, r => r.Rate);
                var reservations = await _db.Reservations.AsNoTracking().ToListAsync();
                var customerCount = await _db.Customers.CountAsync();

                // Only Confirmed / CheckedIn / CheckedOut count as room revenue (see RevenueRules)
                var roomRevenue = RevenueRules.Total(reservations, rates);
                var restaurantRevenue = await _db.Sales.AsNoTracking().SumAsync(s => (decimal?)s.Total) ?? 0m;
                var totalRevenue = roomRevenue + restaurantRevenue;

                var purchases = await _db.PurchaseOrders.AsNoTracking()
                    .Where(p => p.Status == "Received")
                    .SumAsync(p => (decimal?)p.TotalCost) ?? 0m;
                var salaries = await _db.PayrollRecords.AsNoTracking()
                    .SumAsync(p => (decimal?)p.GrossPay) ?? 0m;
                var totalExpenses = purchases + salaries;

                var summary = new
                {
                    totalRooms = rooms.Count,
                    availableRooms = rooms.Count(r => r.Status == "Available"),
                    occupiedRooms = rooms.Count(r => r.Status == "Occupied"),
                    totalReservations = reservations.Count,
                    pendingReservations = reservations.Count(r => r.Status == "Pending"),
                    confirmedReservations = reservations.Count(r => r.Status == "Confirmed"),
                    checkedInReservations = reservations.Count(r => r.Status == "CheckedIn"),
                    checkedOutReservations = reservations.Count(r => r.Status == "CheckedOut"),
                    cancelledReservations = reservations.Count(r => r.Status == "Cancelled"),
                    totalCustomers = customerCount,
                    totalSuppliers = await _db.Suppliers.CountAsync(s => s.IsActive),
                    pendingPurchaseOrders = await _db.PurchaseOrders.CountAsync(p => p.Status == "Pending"),
                    totalEmployees = await _db.Employees.CountAsync(e => e.IsActive),
                    roomRevenue,
                    restaurantRevenue,
                    totalRevenue,
                    totalExpenses,
                    netIncome = totalRevenue - totalExpenses
                };

                return Ok(summary);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to load dashboard summary.");
            }
        }
    }
}