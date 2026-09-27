using System;

namespace Anorath.domain.Entities
{
    public class Customer
    {
        public int CustomerId { get; set; }

        public string CustomerCode { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string? ContactNumber { get; set; }

        public string? EmailAddress { get; set; }

        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}