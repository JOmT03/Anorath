using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.Reception
{
    internal class Customer
    {

        public int CustomerId { get; set; }

        public string CustomerCode { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string ContactNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
