using System;
using System.Collections.Generic;

namespace Anorath.domain.Entities
{
    public class Company
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;

        // Subscription tier: Micro (Tenant A), Small (Tenant B), Medium (Tenant C)
        public string Plan { get; set; } = "Micro";
        public DateTime SubscriptionStart { get; set; } = DateTime.UtcNow;
        public DateTime? SubscriptionEnd { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}