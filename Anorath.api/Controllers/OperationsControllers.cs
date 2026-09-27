using Anorath.domain.Entities.Operations;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    public record PurchaseOrderRequest(int SupplierId, DateTime OrderDate, string ItemDescription, int Quantity, decimal UnitCost);
    public record StatusRequest(string Status);
    public record PayrollRequest(int EmployeeId, DateTime PeriodStart, DateTime PeriodEnd, decimal DaysWorked, decimal Deductions);

    // =====================================================
    // SUPPLIERS  (Supply Chain / Procurement)
    // =====================================================
    [ApiController]
    [Route("suppliers")]
    public class SuppliersController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public SuppliersController(TenantErpDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var suppliers = await _db.Suppliers.AsNoTracking().OrderBy(s => s.SupplierName).ToListAsync();
            return Ok(suppliers);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Supplier supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier.SupplierCode) || string.IsNullOrWhiteSpace(supplier.SupplierName))
                return BadRequest("SupplierCode and SupplierName are required.");

            try
            {
                supplier.SupplierId = 0;
                _db.Suppliers.Add(supplier);
                await _db.SaveChangesAsync();
                return Created($"/suppliers/{supplier.SupplierId}", supplier);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create supplier.");
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Supplier updated)
        {
            var supplier = await _db.Suppliers.FindAsync(id);
            if (supplier is null) return NotFound();

            if (string.IsNullOrWhiteSpace(updated.SupplierCode) || string.IsNullOrWhiteSpace(updated.SupplierName))
                return BadRequest("SupplierCode and SupplierName are required.");

            try
            {
                supplier.SupplierCode = updated.SupplierCode;
                supplier.SupplierName = updated.SupplierName;
                supplier.ContactNumber = updated.ContactNumber;
                supplier.EmailAddress = updated.EmailAddress;
                supplier.IsActive = updated.IsActive;
                await _db.SaveChangesAsync();
                return Ok(supplier);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update supplier.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _db.Suppliers.FindAsync(id);
            if (supplier is null) return NotFound();

            if (await _db.PurchaseOrders.AnyAsync(p => p.SupplierId == id))
                return BadRequest("This supplier has purchase orders. Set it to inactive instead of deleting.");

            try
            {
                _db.Suppliers.Remove(supplier);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete supplier.");
            }
        }
    }

    // =====================================================
    // PURCHASE ORDERS  (Supply Chain / Procurement)
    // =====================================================
    [ApiController]
    [Route("purchaseorders")]
    public class PurchaseOrdersController : ControllerBase
    {
        private static readonly string[] AllowedStatuses = { "Pending", "Received", "Cancelled" };

        private readonly TenantErpDbContext _db;
        public PurchaseOrdersController(TenantErpDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var suppliers = await _db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.SupplierId, s => s.SupplierName);
            var orders = await _db.PurchaseOrders.AsNoTracking().OrderByDescending(p => p.OrderDate).ToListAsync();

            var result = orders.Select(p => new
            {
                p.PurchaseOrderId,
                p.SupplierId,
                SupplierName = suppliers.TryGetValue(p.SupplierId, out var name) ? name : "(unknown)",
                p.OrderDate,
                p.ItemDescription,
                p.Quantity,
                p.UnitCost,
                p.TotalCost,
                p.Status,
                p.ReceivedDate
            });

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PurchaseOrderRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ItemDescription))
                return BadRequest("Item description is required.");
            if (req.Quantity <= 0)
                return BadRequest("Quantity must be greater than zero.");
            if (req.UnitCost < 0)
                return BadRequest("Unit cost cannot be negative.");

            var supplier = await _db.Suppliers.FindAsync(req.SupplierId);
            if (supplier is null || !supplier.IsActive)
                return BadRequest("A valid, active supplier is required.");

            try
            {
                var order = new PurchaseOrder
                {
                    SupplierId = req.SupplierId,
                    OrderDate = req.OrderDate == default ? DateTime.Today : req.OrderDate.Date,
                    ItemDescription = req.ItemDescription.Trim(),
                    Quantity = req.Quantity,
                    UnitCost = req.UnitCost,
                    TotalCost = Math.Round(req.Quantity * req.UnitCost, 2),
                    Status = "Pending"
                };

                _db.PurchaseOrders.Add(order);
                await _db.SaveChangesAsync();
                return Created($"/purchaseorders/{order.PurchaseOrderId}", order);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create purchase order.");
            }
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusRequest req)
        {
            if (!AllowedStatuses.Contains(req.Status))
                return BadRequest("Status must be Pending, Received, or Cancelled.");

            var order = await _db.PurchaseOrders.FindAsync(id);
            if (order is null) return NotFound();

            try
            {
                order.Status = req.Status;
                order.ReceivedDate = req.Status == "Received" ? DateTime.Today : null;
                await _db.SaveChangesAsync();
                return Ok(order);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update purchase order status.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _db.PurchaseOrders.FindAsync(id);
            if (order is null) return NotFound();

            if (order.Status == "Received")
                return BadRequest("Received orders are recorded as expenses and cannot be deleted.");

            try
            {
                _db.PurchaseOrders.Remove(order);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete purchase order.");
            }
        }
    }

    // =====================================================
    // EMPLOYEES  (Payroll)
    // =====================================================
    [ApiController]
    [Route("employees")]
    public class EmployeesController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public EmployeesController(TenantErpDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var employees = await _db.Employees.AsNoTracking().OrderBy(e => e.FullName).ToListAsync();
            return Ok(employees);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Employee employee)
        {
            if (string.IsNullOrWhiteSpace(employee.EmployeeCode) || string.IsNullOrWhiteSpace(employee.FullName))
                return BadRequest("EmployeeCode and FullName are required.");
            if (employee.DailyRate <= 0)
                return BadRequest("Daily rate must be greater than zero.");

            try
            {
                employee.EmployeeId = 0;
                if (employee.DateHired == default) employee.DateHired = DateTime.Today;
                _db.Employees.Add(employee);
                await _db.SaveChangesAsync();
                return Created($"/employees/{employee.EmployeeId}", employee);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create employee.");
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Employee updated)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee is null) return NotFound();

            if (string.IsNullOrWhiteSpace(updated.EmployeeCode) || string.IsNullOrWhiteSpace(updated.FullName))
                return BadRequest("EmployeeCode and FullName are required.");
            if (updated.DailyRate <= 0)
                return BadRequest("Daily rate must be greater than zero.");

            try
            {
                employee.EmployeeCode = updated.EmployeeCode;
                employee.FullName = updated.FullName;
                employee.Position = updated.Position;
                employee.DailyRate = updated.DailyRate;
                if (updated.DateHired != default) employee.DateHired = updated.DateHired;
                employee.IsActive = updated.IsActive;
                await _db.SaveChangesAsync();
                return Ok(employee);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update employee.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee is null) return NotFound();

            if (await _db.PayrollRecords.AnyAsync(p => p.EmployeeId == id))
                return BadRequest("This employee has payroll records. Set them to inactive instead of deleting.");

            try
            {
                _db.Employees.Remove(employee);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete employee.");
            }
        }
    }

    // =====================================================
    // PAYROLL RECORDS
    // =====================================================
    [ApiController]
    [Route("payroll")]
    public class PayrollController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public PayrollController(TenantErpDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var employees = await _db.Employees.AsNoTracking().ToDictionaryAsync(e => e.EmployeeId, e => e.FullName);
            var records = await _db.PayrollRecords.AsNoTracking().OrderByDescending(p => p.PeriodEnd).ToListAsync();

            var result = records.Select(p => new
            {
                p.PayrollRecordId,
                p.EmployeeId,
                EmployeeName = employees.TryGetValue(p.EmployeeId, out var name) ? name : "(unknown)",
                p.PeriodStart,
                p.PeriodEnd,
                p.DaysWorked,
                p.DailyRate,
                p.GrossPay,
                p.Deductions,
                p.NetPay
            });

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PayrollRequest req)
        {
            if (req.PeriodEnd.Date < req.PeriodStart.Date)
                return BadRequest("Period end must be on or after period start.");
            if (req.DaysWorked <= 0)
                return BadRequest("Days worked must be greater than zero.");

            var periodDays = (req.PeriodEnd.Date - req.PeriodStart.Date).Days + 1;
            if (req.DaysWorked > periodDays)
                return BadRequest($"Days worked cannot exceed the {periodDays} days in this period.");
            if (req.Deductions < 0)
                return BadRequest("Deductions cannot be negative.");

            var employee = await _db.Employees.FindAsync(req.EmployeeId);
            if (employee is null || !employee.IsActive)
                return BadRequest("A valid, active employee is required.");

            var gross = Math.Round(employee.DailyRate * req.DaysWorked, 2);
            if (req.Deductions > gross)
                return BadRequest("Deductions cannot be greater than gross pay.");

            try
            {
                var record = new PayrollRecord
                {
                    EmployeeId = employee.EmployeeId,
                    PeriodStart = req.PeriodStart.Date,
                    PeriodEnd = req.PeriodEnd.Date,
                    DaysWorked = req.DaysWorked,
                    DailyRate = employee.DailyRate,
                    GrossPay = gross,
                    Deductions = req.Deductions,
                    NetPay = gross - req.Deductions,
                    CreatedAt = DateTime.Now
                };

                _db.PayrollRecords.Add(record);
                await _db.SaveChangesAsync();
                return Created($"/payroll/{record.PayrollRecordId}", record);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create payroll record.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var record = await _db.PayrollRecords.FindAsync(id);
            if (record is null) return NotFound();

            try
            {
                _db.PayrollRecords.Remove(record);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete payroll record.");
            }
        }
    }
}