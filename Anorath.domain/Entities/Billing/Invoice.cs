using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.Billing
{
    internal class Invoice
    {
        public int InvoiceId { get; set; }

        public int BillingId { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        public decimal Amount { get; set; }

        public string Status { get; set; } = "Unpaid";
    }
}
