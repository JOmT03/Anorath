using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.restaurant
{
    internal class Order
    {
        public int OrderId { get; set; }

        public int? CustomerId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";

        public string OrderType { get; set; } = "DineIn";

        public string Remarks { get; set; } = string.Empty;

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}
