using Anorath.domain.Entities.Reception;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Anorath.api.Controllers
{
    // =====================================================
    // Shared revenue rules — one place so Dashboard, Reports,
    // Financial Statements and BI all compute revenue the same way.
    // =====================================================
    internal static class RevenueRules
    {
        // Pending and Cancelled reservations do NOT count as revenue.
        public static readonly string[] EarningStatuses = { "Confirmed", "CheckedIn", "CheckedOut" };

        public static bool IsEarning(string? status) => status != null && EarningStatuses.Contains(status);

        public static int Nights(DateTime checkIn, DateTime checkOut)
        {
            var nights = (checkOut.Date - checkIn.Date).Days;
            return nights < 1 ? 1 : nights;
        }

        public static decimal Amount(Reservations r, Dictionary<int, decimal> roomRates)
            => roomRates.TryGetValue(r.RoomId, out var rate) ? rate * Nights(r.CheckIn, r.CheckOut) : 0m;

        public static decimal Total(IEnumerable<Reservations> list, Dictionary<int, decimal> roomRates)
            => list.Where(r => IsEarning(r.Status)).Sum(r => Amount(r, roomRates));

        public static (DateTime start, DateTime endExclusive) Range(DateTime? from, DateTime? to)
        {
            var start = (from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            var end = (to ?? start.AddMonths(1).AddDays(-1)).Date;
            return (start, end.AddDays(1));
        }
    }

    // =====================================================
    // FINANCIAL STATEMENTS  (Income Statement + Income Tax)
    // Owner (Admin) only, Small and Medium plans only.
    // =====================================================
    [ApiController]
    [Route("financials")]
    [Authorize(Roles = "Admin")]
    public class FinancialsController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        private readonly IConfiguration _config;

        public FinancialsController(TenantErpDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // GET financials/income-statement?from=2026-09-01&to=2026-09-30&taxRate=0.25
        [HttpGet("income-statement")]
        public async Task<IActionResult> IncomeStatement([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] decimal? taxRate)
        {
            // Plan check comes from the signed token, not from the client
            var plan = User.FindFirst("plan")?.Value;
            if (plan == "Micro")
                return StatusCode(403, "Financial Statements are available on the Small and Medium plans only.");

            var (start, endExclusive) = RevenueRules.Range(from, to);
            if (endExclusive <= start) return BadRequest("'To' date must be on or after 'From' date.");

            // CREATE Act (RA 11534): 25% regular, 20% small corporations. Default comes from appsettings.
            var rate = taxRate ?? _config.GetValue<decimal?>("Financials:IncomeTaxRate") ?? 0.25m;
            if (rate < 0m || rate > 0.5m) return BadRequest("Tax rate must be between 0% and 50%.");

            try
            {
                var rooms = await _db.Rooms.AsNoTracking().ToListAsync();
                var rates = rooms.ToDictionary(r => r.RoomId, r => r.Rate);
                var roomTypes = rooms.ToDictionary(r => r.RoomId, r => r.RoomType);

                var reservations = (await _db.Reservations.AsNoTracking()
                        .Where(r => r.CheckIn >= start && r.CheckIn < endExclusive)
                        .ToListAsync())
                    .Where(r => RevenueRules.IsEarning(r.Status))
                    .ToList();

                var revenueLines = reservations
                    .GroupBy(r => roomTypes.TryGetValue(r.RoomId, out var t) ? t : "Other")
                    .Select(g => new { account = $"Room Revenue - {g.Key}", amount = g.Sum(r => RevenueRules.Amount(r, rates)) })
                    .OrderByDescending(x => x.amount)
                    .ToList();

                var restaurant = await _db.Sales.AsNoTracking()
                    .Where(s => s.SaleDate >= start && s.SaleDate < endExclusive)
                    .SumAsync(s => (decimal?)s.Total) ?? 0m;
                revenueLines.Add(new { account = "Restaurant Sales", amount = restaurant });

                // Only RECEIVED purchase orders are expenses
                var purchases = await _db.PurchaseOrders.AsNoTracking()
                    .Where(p => p.Status == "Received" && (p.ReceivedDate ?? p.OrderDate) >= start && (p.ReceivedDate ?? p.OrderDate) < endExclusive)
                    .SumAsync(p => (decimal?)p.TotalCost) ?? 0m;

                var salaries = await _db.PayrollRecords.AsNoTracking()
                    .Where(p => p.PeriodEnd >= start && p.PeriodEnd < endExclusive)
                    .SumAsync(p => (decimal?)p.GrossPay) ?? 0m;

                var totalRevenue = revenueLines.Sum(x => x.amount);
                var totalExpenses = purchases + salaries;
                var incomeBeforeTax = totalRevenue - totalExpenses;

                // No income tax on a loss
                var incomeTax = incomeBeforeTax > 0 ? Math.Round(incomeBeforeTax * rate, 2) : 0m;

                return Ok(new
                {
                    periodStart = start,
                    periodEnd = endExclusive.AddDays(-1),
                    revenueLines,
                    totalRevenue,
                    expenseLines = new[]
                    {
                        new { account = "Supplies & Purchases", amount = purchases },
                        new { account = "Salaries & Wages", amount = salaries }
                    },
                    totalExpenses,
                    incomeBeforeTax,
                    taxRate = rate,
                    incomeTax,
                    netIncome = incomeBeforeTax - incomeTax
                });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to generate income statement.");
            }
        }
    }

    // =====================================================
    // REPORTS  (Accommodation + Restaurant)
    // =====================================================
    [ApiController]
    [Route("reports")]
    [Authorize(Roles = "Admin,Manager,BranchManager")]
    public class ReportsController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public ReportsController(TenantErpDbContext db) => _db = db;

        [HttpGet("reservations")]
        public async Task<IActionResult> ReservationReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var (start, endExclusive) = RevenueRules.Range(from, to);
            if (endExclusive <= start) return BadRequest("'to' date must be on or after 'from' date.");

            try
            {
                var rooms = await _db.Rooms.AsNoTracking().ToListAsync();
                var rates = rooms.ToDictionary(r => r.RoomId, r => r.Rate);
                var roomById = rooms.ToDictionary(r => r.RoomId);
                var customers = await _db.Customers.AsNoTracking().ToDictionaryAsync(c => c.CustomerId, c => c.CustomerName);

                var reservations = await _db.Reservations.AsNoTracking()
                    .Where(r => r.CheckIn >= start && r.CheckIn < endExclusive)
                    .OrderBy(r => r.CheckIn)
                    .ToListAsync();

                var rows = reservations.Select(r => new
                {
                    r.ReservationId,
                    customerName = customers.TryGetValue(r.CustomerId, out var c) ? c : "(unknown)",
                    roomNumber = roomById.TryGetValue(r.RoomId, out var room) ? room.RoomNumber : "(unknown)",
                    roomType = room?.RoomType ?? "",
                    checkIn = r.CheckIn,
                    checkOut = r.CheckOut,
                    nights = RevenueRules.Nights(r.CheckIn, r.CheckOut),
                    amount = RevenueRules.Amount(r, rates),
                    status = r.Status
                }).ToList();

                return Ok(new
                {
                    periodStart = start,
                    periodEnd = endExclusive.AddDays(-1),
                    rows,
                    totalReservations = rows.Count,
                    earningReservations = reservations.Count(r => RevenueRules.IsEarning(r.Status)),
                    totalRevenue = RevenueRules.Total(reservations, rates)
                });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to generate reservation report.");
            }
        }

        [HttpGet("restaurant")]
        public async Task<IActionResult> RestaurantReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var (start, endExclusive) = RevenueRules.Range(from, to);
            if (endExclusive <= start) return BadRequest("'to' date must be on or after 'from' date.");

            try
            {
                var products = await _db.Products.AsNoTracking().ToDictionaryAsync(p => p.ProductId, p => p.ProductName);
                var sales = await _db.Sales.AsNoTracking()
                    .Where(s => s.SaleDate >= start && s.SaleDate < endExclusive)
                    .ToListAsync();

                var rows = sales
                    .GroupBy(s => s.ProductId)
                    .Select(g => new
                    {
                        itemName = products.TryGetValue(g.Key, out var name) ? name : "(deleted item)",
                        quantitySold = g.Sum(s => s.Quantity),
                        transactions = g.Count(),
                        totalSales = g.Sum(s => s.Total)
                    })
                    .OrderByDescending(x => x.totalSales)
                    .ToList();

                var byPayment = sales
                    .GroupBy(s => s.PaymentMethod)
                    .Select(g => new { paymentMethod = g.Key, amount = g.Sum(s => s.Total) })
                    .ToList();

                return Ok(new
                {
                    periodStart = start,
                    periodEnd = endExclusive.AddDays(-1),
                    rows,
                    byPayment,
                    totalTransactions = sales.Count,
                    totalItemsSold = sales.Sum(s => s.Quantity),
                    totalSales = sales.Sum(s => s.Total)
                });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to generate restaurant report.");
            }
        }
    }

    // =====================================================
    // BUSINESS INTELLIGENCE  (KPIs + graph data)
    // =====================================================
    [ApiController]
    [Route("analytics")]
    [Authorize(Roles = "Admin,Manager,BranchManager")]
    public class AnalyticsController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public AnalyticsController(TenantErpDbContext db) => _db = db;

        [HttpGet("kpis")]
        public async Task<IActionResult> Kpis()
        {
            try
            {
                var rooms = await _db.Rooms.AsNoTracking().ToListAsync();
                var rates = rooms.ToDictionary(r => r.RoomId, r => r.Rate);
                var reservations = await _db.Reservations.AsNoTracking().ToListAsync();

                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                var earning = reservations.Where(r => RevenueRules.IsEarning(r.Status)).ToList();
                var roomNightsSold = earning.Sum(r => RevenueRules.Nights(r.CheckIn, r.CheckOut));
                var roomRevenue = RevenueRules.Total(earning, rates);

                var thisMonth = reservations.Where(r => r.CheckIn >= monthStart && r.CheckIn < monthEnd).ToList();
                var restaurantThisMonth = await _db.Sales.AsNoTracking()
                    .Where(s => s.SaleDate >= monthStart && s.SaleDate < monthEnd)
                    .SumAsync(s => (decimal?)s.Total) ?? 0m;

                var byCustomer = reservations.Where(r => r.Status != "Cancelled").GroupBy(r => r.CustomerId).ToList();
                var repeatCustomers = byCustomer.Count(g => g.Count() >= 2);

                var occupied = rooms.Count(r => r.Status == "Occupied");
                var cancelled = reservations.Count(r => r.Status == "Cancelled");

                return Ok(new
                {
                    occupancyRate = rooms.Count == 0 ? 0m : Math.Round(occupied * 100m / rooms.Count, 1),
                    revenueThisMonth = RevenueRules.Total(thisMonth, rates) + restaurantThisMonth,
                    restaurantThisMonth,
                    reservationsThisMonth = thisMonth.Count,
                    averageDailyRate = roomNightsSold == 0 ? 0m : Math.Round(roomRevenue / roomNightsSold, 2),
                    averageStayNights = earning.Count == 0 ? 0m : Math.Round((decimal)roomNightsSold / earning.Count, 1),
                    cancellationRate = reservations.Count == 0 ? 0m : Math.Round(cancelled * 100m / reservations.Count, 1),
                    repeatCustomerRate = byCustomer.Count == 0 ? 0m : Math.Round(repeatCustomers * 100m / byCustomer.Count, 1),
                    repeatCustomers,
                    totalRevenue = roomRevenue
                });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to load KPIs.");
            }
        }

        [HttpGet("monthly-revenue")]
        public async Task<IActionResult> MonthlyRevenue([FromQuery] int months = 6)
        {
            if (months < 1 || months > 24) months = 6;

            try
            {
                var rates = await _db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.RoomId, r => r.Rate);
                var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(months - 1));
                var reservations = await _db.Reservations.AsNoTracking()
                    .Where(r => r.CheckIn >= firstMonth)
                    .ToListAsync();
                var sales = await _db.Sales.AsNoTracking()
                    .Where(s => s.SaleDate >= firstMonth)
                    .ToListAsync();

                var result = Enumerable.Range(0, months).Select(i =>
                {
                    var start = firstMonth.AddMonths(i);
                    var end = start.AddMonths(1);
                    var inMonth = reservations.Where(r => r.CheckIn >= start && r.CheckIn < end).ToList();
                    var roomRevenue = RevenueRules.Total(inMonth, rates);
                    var restaurantRevenue = sales.Where(s => s.SaleDate >= start && s.SaleDate < end).Sum(s => s.Total);
                    return new
                    {
                        month = start.ToString("MMM yyyy"),
                        revenue = roomRevenue + restaurantRevenue,
                        roomRevenue,
                        restaurantRevenue,
                        reservations = inMonth.Count,
                        visits = inMonth.Count(r => RevenueRules.IsEarning(r.Status))

                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to load monthly revenue.");
            }
        }

        [HttpGet("reservations-by-status")]
        public async Task<IActionResult> ReservationsByStatus()
        {
            try
            {
                var result = await _db.Reservations.AsNoTracking()
                    .GroupBy(r => r.Status)
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .ToListAsync();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to load reservation status breakdown.");
            }
        }
    }
}