using System.Text.RegularExpressions;
using Anorath.domain.Entities.Operations;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    // ---------- Request DTOs ----------
    public record PoLineRequest(int SupplierItemId, int Quantity);
    public record PurchaseOrderRequest(int SupplierId, DateTime OrderDate, List<PoLineRequest> Lines);
    public record StatusRequest(string Status);
    public record PayrollRequest(int EmployeeId, DateTime PeriodStart, DateTime PeriodEnd, decimal DaysWorked, decimal Deductions);

    // =====================================================
    // SUPPLIERS + SUPPLIER ITEMS (price list)
    // =====================================================
    [ApiController]
    [Route("suppliers")]
    [Authorize(Roles = "Admin,Manager,BranchManager")]
    public class SuppliersController : ControllerBase
    {
        private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        private readonly TenantErpDbContext _db;
        public SuppliersController(TenantErpDbContext db) => _db = db;

        // GET suppliers?search=acdc
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search)
        {
            var q = _db.Suppliers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(x => x.SupplierName.Contains(s) || x.SupplierCode.Contains(s)
                              || (x.ContactPerson != null && x.ContactPerson.Contains(s)));
            }
            return Ok(await q.OrderBy(x => x.SupplierName).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Supplier supplier)
        {
            var error = Validate(supplier);
            if (error != null) return BadRequest(error);

            if (await _db.Suppliers.AnyAsync(s => s.SupplierName == supplier.SupplierName.Trim()))
                return BadRequest("A supplier with this name already exists.");

            // Auto-generate SUP-0001, SUP-0002, ...
            var next = await _db.Suppliers.CountAsync() + 1;
            var newCode = $"SUP-{next:0000}";
            while (await _db.Suppliers.AnyAsync(s => s.SupplierCode == newCode))
            {
                next++;
                newCode = $"SUP-{next:0000}";
            }

            try
            {
                supplier.SupplierId = 0;
                supplier.SupplierCode = newCode;
                Clean(supplier);
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
            if (supplier is null) return NotFound("Supplier not found.");

            var error = Validate(updated);
            if (error != null) return BadRequest(error);

            if (await _db.Suppliers.AnyAsync(s => s.SupplierId != id && s.SupplierName == updated.SupplierName.Trim()))
                return BadRequest("A supplier with this name already exists.");

            try
            {
                Clean(updated);
                supplier.SupplierName = updated.SupplierName;
                supplier.ContactPerson = updated.ContactPerson;
                supplier.ContactNumber = updated.ContactNumber;
                supplier.EmailAddress = updated.EmailAddress;
                supplier.Address = updated.Address;
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
            if (supplier is null) return NotFound("Supplier not found.");

            if (await _db.PurchaseOrders.AnyAsync(p => p.SupplierId == id))
                return BadRequest("This supplier has purchase orders. Set it to inactive instead of deleting.");

            try
            {
                var items = _db.SupplierItems.Where(i => i.SupplierId == id);
                _db.SupplierItems.RemoveRange(items);
                _db.Suppliers.Remove(supplier);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete supplier.");
            }
        }

        // ---------- ITEMS (price list) ----------

        // GET suppliers/5/items
        [HttpGet("{id:int}/items")]
        public async Task<IActionResult> GetItems(int id, [FromQuery] bool activeOnly = false)
        {
            if (!await _db.Suppliers.AnyAsync(s => s.SupplierId == id))
                return NotFound("Supplier not found.");

            var q = _db.SupplierItems.AsNoTracking().Where(i => i.SupplierId == id);
            if (activeOnly) q = q.Where(i => i.IsActive);
            return Ok(await q.OrderBy(i => i.ItemName).ToListAsync());
        }

        // POST suppliers/5/items
        [HttpPost("{id:int}/items")]
        public async Task<IActionResult> AddItem(int id, [FromBody] SupplierItem item)
        {
            if (!await _db.Suppliers.AnyAsync(s => s.SupplierId == id))
                return NotFound("Supplier not found.");

            var error = ValidateItem(item);
            if (error != null) return BadRequest(error);

            var name = item.ItemName.Trim();
            if (await _db.SupplierItems.AnyAsync(i => i.SupplierId == id && i.ItemName == name))
                return BadRequest("This supplier already has an item with that name.");

            try
            {
                item.SupplierItemId = 0;
                item.SupplierId = id;
                item.ItemName = name;
                item.Unit = string.IsNullOrWhiteSpace(item.Unit) ? "pcs" : item.Unit.Trim();
                item.UnitPrice = Math.Round(item.UnitPrice, 2);
                _db.SupplierItems.Add(item);
                await _db.SaveChangesAsync();
                return Created($"/suppliers/items/{item.SupplierItemId}", item);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to add item.");
            }
        }

        // PUT suppliers/items/7
        [HttpPut("items/{itemId:int}")]
        public async Task<IActionResult> UpdateItem(int itemId, [FromBody] SupplierItem updated)
        {
            var item = await _db.SupplierItems.FindAsync(itemId);
            if (item is null) return NotFound("Item not found.");

            var error = ValidateItem(updated);
            if (error != null) return BadRequest(error);

            var name = updated.ItemName.Trim();
            if (await _db.SupplierItems.AnyAsync(i => i.SupplierId == item.SupplierId && i.SupplierItemId != itemId && i.ItemName == name))
                return BadRequest("This supplier already has an item with that name.");

            try
            {
                item.ItemName = name;
                item.Unit = string.IsNullOrWhiteSpace(updated.Unit) ? "pcs" : updated.Unit.Trim();
                item.UnitPrice = Math.Round(updated.UnitPrice, 2);
                item.IsActive = updated.IsActive;
                await _db.SaveChangesAsync();
                return Ok(item);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update item.");
            }
        }

        // DELETE suppliers/items/7
        [HttpDelete("items/{itemId:int}")]
        public async Task<IActionResult> DeleteItem(int itemId)
        {
            var item = await _db.SupplierItems.FindAsync(itemId);
            if (item is null) return NotFound("Item not found.");

            if (await _db.PurchaseOrderLines.AnyAsync(l => l.SupplierItemId == itemId))
                return BadRequest("This item is already used in purchase orders. Set it to inactive instead of deleting.");

            try
            {
                _db.SupplierItems.Remove(item);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete item.");
            }
        }

        // ---------- helpers ----------
        private static string? Validate(Supplier s)
        {
            if (string.IsNullOrWhiteSpace(s.SupplierName)) return "Supplier name is required.";
            if (s.SupplierName.Trim().Length > 150) return "Supplier name is too long (max 150).";
            if (!string.IsNullOrWhiteSpace(s.EmailAddress) && !EmailRx.IsMatch(s.EmailAddress.Trim()))
                return "Email address is not valid.";
            if (!string.IsNullOrWhiteSpace(s.ContactNumber) && !Regex.IsMatch(s.ContactNumber.Trim(), @"^[0-9+\-\s()]{7,20}$"))
                return "Contact number is not valid.";
            return null;
        }

        private static void Clean(Supplier s)
        {
            s.SupplierName = s.SupplierName.Trim();
            s.ContactPerson = string.IsNullOrWhiteSpace(s.ContactPerson) ? null : s.ContactPerson.Trim();
            s.ContactNumber = string.IsNullOrWhiteSpace(s.ContactNumber) ? null : s.ContactNumber.Trim();
            s.EmailAddress = string.IsNullOrWhiteSpace(s.EmailAddress) ? null : s.EmailAddress.Trim();
            s.Address = string.IsNullOrWhiteSpace(s.Address) ? null : s.Address.Trim();
        }

        private static string? ValidateItem(SupplierItem i)
        {
            if (string.IsNullOrWhiteSpace(i.ItemName)) return "Item name is required.";
            if (i.ItemName.Trim().Length > 150) return "Item name is too long (max 150).";
            if (i.UnitPrice <= 0) return "Unit price must be greater than zero.";
            return null;
        }
    }

    // =====================================================
    // PURCHASE ORDERS (multi-item, per supplier)
    // =====================================================
    [ApiController]
    [Route("purchaseorders")]
    [Authorize(Roles = "Admin,Manager,BranchManager")]
    public class PurchaseOrdersController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public PurchaseOrdersController(TenantErpDbContext db) => _db = db;

        private static string PoNumber(int id) => $"PO-{id:00000}";

        // GET purchaseorders?supplierId=1&status=Pending
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? supplierId, [FromQuery] string? status)
        {
            var q = _db.PurchaseOrders.AsNoTracking().AsQueryable();
            if (supplierId is > 0) q = q.Where(p => p.SupplierId == supplierId);
            if (!string.IsNullOrWhiteSpace(status) && status != "All") q = q.Where(p => p.Status == status);

            var suppliers = await _db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.SupplierId, s => s.SupplierName);
            var orders = await q.OrderByDescending(p => p.OrderDate).ThenByDescending(p => p.PurchaseOrderId).ToListAsync();

            return Ok(orders.Select(p => new
            {
                p.PurchaseOrderId,
                PoNumber = PoNumber(p.PurchaseOrderId),
                p.SupplierId,
                SupplierName = suppliers.TryGetValue(p.SupplierId, out var n) ? n : "(unknown)",
                p.OrderDate,
                p.ItemDescription,
                p.Quantity,
                p.TotalCost,
                p.Status,
                p.ReceivedDate
            }));
        }

        // GET purchaseorders/5  (header + supplier + lines, used for View/Print)
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var po = await _db.PurchaseOrders.AsNoTracking().FirstOrDefaultAsync(p => p.PurchaseOrderId == id);
            if (po is null) return NotFound("Purchase order not found.");

            var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == po.SupplierId);
            var lines = await _db.PurchaseOrderLines.AsNoTracking()
                .Where(l => l.PurchaseOrderId == id).OrderBy(l => l.PurchaseOrderLineId).ToListAsync();

            return Ok(new
            {
                po.PurchaseOrderId,
                PoNumber = PoNumber(po.PurchaseOrderId),
                po.OrderDate,
                po.Status,
                po.ReceivedDate,
                po.TotalCost,
                Supplier = supplier,
                Lines = lines
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PurchaseOrderRequest req)
        {
            if (req.Lines is null || req.Lines.Count == 0)
                return BadRequest("Add at least one item to the purchase order.");
            if (req.Lines.Any(l => l.Quantity <= 0))
                return BadRequest("Every item must have a quantity greater than zero.");

            var supplier = await _db.Suppliers.FindAsync(req.SupplierId);
            if (supplier is null || !supplier.IsActive)
                return BadRequest("A valid, active supplier is required.");

            // Merge duplicate items (same item picked twice → add quantities)
            var merged = req.Lines.GroupBy(l => l.SupplierItemId)
                                  .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.Quantity) })
                                  .ToList();

            var ids = merged.Select(m => m.ItemId).ToList();
            var items = await _db.SupplierItems
                .Where(i => ids.Contains(i.SupplierItemId) && i.SupplierId == req.SupplierId && i.IsActive)
                .ToDictionaryAsync(i => i.SupplierItemId);

            if (items.Count != ids.Count)
                return BadRequest("One or more items are invalid, inactive, or not from this supplier.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Prices always come from the supplier's price list, never from the client
                var lines = merged.Select(m =>
                {
                    var it = items[m.ItemId];
                    return new PurchaseOrderLine
                    {
                        SupplierItemId = it.SupplierItemId,
                        ItemName = it.ItemName,
                        Unit = it.Unit,
                        Quantity = m.Qty,
                        UnitPrice = it.UnitPrice,
                        LineTotal = Math.Round(it.UnitPrice * m.Qty, 2)
                    };
                }).ToList();

                var first = lines[0].ItemName;
                var summary = lines.Count == 1 ? first : $"{first} + {lines.Count - 1} more";
                if (summary.Length > 200) summary = summary[..200];

                var order = new PurchaseOrder
                {
                    SupplierId = req.SupplierId,
                    OrderDate = req.OrderDate == default ? DateTime.Today : req.OrderDate.Date,
                    ItemDescription = summary,
                    Quantity = lines.Sum(l => l.Quantity),
                    UnitCost = 0,
                    TotalCost = lines.Sum(l => l.LineTotal),
                    Status = "Pending"
                };

                _db.PurchaseOrders.Add(order);
                await _db.SaveChangesAsync();

                foreach (var l in lines) l.PurchaseOrderId = order.PurchaseOrderId;
                _db.PurchaseOrderLines.AddRange(lines);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return Created($"/purchaseorders/{order.PurchaseOrderId}",
                    new { order.PurchaseOrderId, PoNumber = PoNumber(order.PurchaseOrderId), order.TotalCost });
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                return StatusCode(500, "Failed to create purchase order.");
            }
        }

        // PUT purchaseorders/5/status   Pending -> Received / Cancelled only
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusRequest req)
        {
            var order = await _db.PurchaseOrders.FindAsync(id);
            if (order is null) return NotFound("Purchase order not found.");

            if (order.Status != "Pending")
                return BadRequest($"This order is already {order.Status} and can no longer be changed.");
            if (req.Status != "Received" && req.Status != "Cancelled")
                return BadRequest("A pending order can only be marked Received or Cancelled.");

            try
            {
                order.Status = req.Status;
                order.ReceivedDate = req.Status == "Received" ? DateTime.Today : null;
                await _db.SaveChangesAsync();
                return Ok(new { order.PurchaseOrderId, order.Status, order.ReceivedDate });
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
            if (order is null) return NotFound("Purchase order not found.");

            if (order.Status == "Received")
                return BadRequest("Received orders are recorded as expenses and cannot be deleted.");

            try
            {
                var lines = _db.PurchaseOrderLines.Where(l => l.PurchaseOrderId == id);
                _db.PurchaseOrderLines.RemoveRange(lines);
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
    // EMPLOYEES (Payroll)
    // =====================================================
    [ApiController]
    [Route("employees")]
    [Authorize(Roles = "Admin,Manager,BranchManager")]
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
    [Authorize(Roles = "Admin,Manager,BranchManager")]
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