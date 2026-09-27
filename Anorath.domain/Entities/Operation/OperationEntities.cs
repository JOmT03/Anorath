using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Anorath.domain.Entities.Operations
{
    // =====================================================
    // SUPPLY CHAIN / PROCUREMENT
    // =====================================================
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [MaxLength(30)]
        public string SupplierCode { get; set; } = "";

        [MaxLength(150)]
        public string SupplierName { get; set; } = "";

        [MaxLength(150)]
        public string? ContactPerson { get; set; }

        [MaxLength(50)]
        public string? ContactNumber { get; set; }

        [MaxLength(150)]
        public string? EmailAddress { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;
    }

    // An item a supplier sells us, with the agreed price (e.g. ACDC Electro -> LED Bulb 12W, P85/pc)
    public class SupplierItem
    {
        [Key]
        public int SupplierItemId { get; set; }

        public int SupplierId { get; set; }

        [MaxLength(150)]
        public string ItemName { get; set; } = "";

        [MaxLength(20)]
        public string Unit { get; set; } = "pcs";

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        public bool IsActive { get; set; } = true;
    }

    // Purchase order header (one supplier per PO). TotalCost = sum of its lines.
    public class PurchaseOrder
    {
        [Key]
        public int PurchaseOrderId { get; set; }

        public int SupplierId { get; set; }

        public DateTime OrderDate { get; set; }

        // Short summary, e.g. "LED Bulb 12W + 2 more"
        [MaxLength(200)]
        public string ItemDescription { get; set; } = "";

        // Total number of units on the order
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        // Pending, Received, Cancelled
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime? ReceivedDate { get; set; }
    }

    // One line of a purchase order. Name and price are copied at order time.
    public class PurchaseOrderLine
    {
        [Key]
        public int PurchaseOrderLineId { get; set; }

        public int PurchaseOrderId { get; set; }

        public int SupplierItemId { get; set; }

        [MaxLength(150)]
        public string ItemName { get; set; } = "";

        [MaxLength(20)]
        public string Unit { get; set; } = "pcs";

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }
    }

    // =====================================================
    // PAYROLL
    // =====================================================
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }

        [MaxLength(30)]
        public string EmployeeCode { get; set; } = "";

        [MaxLength(150)]
        public string FullName { get; set; } = "";

        [MaxLength(100)]
        public string? Position { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRate { get; set; }

        public DateTime DateHired { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class PayrollRecord
    {
        [Key]
        public int PayrollRecordId { get; set; }

        public int EmployeeId { get; set; }

        public DateTime PeriodStart { get; set; }

        public DateTime PeriodEnd { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal DaysWorked { get; set; }

        // Snapshot of the employee's rate at the time payroll was computed
        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossPay { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Deductions { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetPay { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}