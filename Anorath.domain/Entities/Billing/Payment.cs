using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.Billing
{
    internal class Payment
    {
        public int PaymentId { get; set; }

        public int BillingId { get; set; }

        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public string ReferenceNumber { get; set; } = string.Empty;
    }
}
