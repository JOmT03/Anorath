using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Anorath.domain.Entities.Operations
{
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [MaxLength(30)]
        public string SupplierCode { get; set; } = "";

        [MaxLength(150)]
        public string SupplierName { get; set; } = "";

        [MaxLength(50)]
        public string? ContactNumber { get; set; }

        [MaxLength(150)]
        public string? EmailAddress { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class PurchaseOrder
    {
        [Key]
        public int PurchaseOrderId { get; set; }

        public int SupplierId { get; set; }

        public DateTime OrderDate { get; set; }

        [MaxLength(200)]
        public string ItemDescription { get; set; } = "";

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime? ReceivedDate { get; set; }
    }

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