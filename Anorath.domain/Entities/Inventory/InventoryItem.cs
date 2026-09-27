using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.Inventory
{
    internal class InventoryItem
    {
        public int InventoryItemId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public decimal ReorderLevel { get; set; }

        public decimal UnitCost { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int SupplierId { get; set; }
    }
}
