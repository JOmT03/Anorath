using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.Billing
{
    internal class Billing
    {
        public int BillingId { get; set; }

        public int CustomerId { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal AmountPaid { get; set; }

        public decimal Balance { get; set; }

        public string Status { get; set; } = "Unpaid";

        public DateTime BillingDate { get; set; } = DateTime.UtcNow;
    }
}
