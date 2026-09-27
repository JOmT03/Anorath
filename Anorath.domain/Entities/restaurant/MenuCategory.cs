using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.restaurant
{
    internal class MenuCategory
    {
        public int MenuCategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MenuItem> MenuItems { get; set; }
            = new List<MenuItem>();
    }
}
