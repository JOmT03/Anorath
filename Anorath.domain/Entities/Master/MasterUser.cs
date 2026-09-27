using System;

namespace Anorath.domain.Entities
{
    // Lives in the MASTER database. Every login is checked here first.
    public class MasterUser
    {
        public int MasterUserId { get; set; }

        // null = Super Admin (not tied to any company)
        public int? CompanyId { get; set; }

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        // SuperAdmin, Admin, Receptionist, RestaurantStaff, HR, BranchManager
        public string Role { get; set; } = "Receptionist";

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Company? Company { get; set; }
    }
}