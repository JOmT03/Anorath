using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.restaurant
{
    internal class OrderItem
    {
        public int OrderItemId { get; set; }

        public int OrderId { get; set; }

        public int MenuItemId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal SubTotal { get; set; }

        public Order? Order { get; set; }

        public MenuItem? MenuItem { get; set; }
    }
}
