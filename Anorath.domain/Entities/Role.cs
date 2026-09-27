using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities
{
    internal class Role
    {

        public int RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
