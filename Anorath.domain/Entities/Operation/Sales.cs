using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Anorath.domain.Entities.Operations
{
    // Restaurant / outlet sale of a Product (menu item)
    public class Sale
    {
        [Key]
        public int SaleId { get; set; }

        public int ProductId { get; set; }

        // Optional: the guest who bought it (null = walk-in / cash customer)
        public int? CustomerId { get; set; }

        public DateTime SaleDate { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // Cash, Card, Charge to Room
        [MaxLength(30)]
        public string PaymentMethod { get; set; } = "Cash";
    }
}