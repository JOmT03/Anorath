using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities
{
    internal class UserRole
    {
        public int UserRoleId { get; set; }

        public int UserId { get; set; }

        public int RoleId { get; set; }
    }
}
