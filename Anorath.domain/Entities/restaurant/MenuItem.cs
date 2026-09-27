using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.restaurant
{
    internal class MenuItem
    {

        public int MenuItemId { get; set; }

        public int MenuCategoryId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public bool IsAvailable { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public MenuCategory? MenuCategory { get; set; }
    }
}
