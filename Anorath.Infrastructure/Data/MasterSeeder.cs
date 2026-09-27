using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Anorath.domain.Entities;
using Anorath.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Anorath.Infrastructure.Data
{
    public static class MasterSeeder
    {
        // Demo default password. Change after first login.
        private const string DefaultPassword = "Admin@123";

        // Which plan each company is on. Edit to match your tenants.
        // Micro = Tenant A, Small = Tenant B, Medium = Tenant C
        private static readonly Dictionary<string, string> Plans = new()
        {
            { "COMP-001", "Medium" },   // Test Enterprise      = Tenant C
            { "COMP-002", "Micro" },    // Second Test Company  = Tenant A
            { "COMP-003", "Small" },    // Cloud Test Company   = Tenant B (lahi nga server)
        };
        public static async Task SeedAsync(MasterErpDbContext db)
        {
            // 1) Super Admin (no company)
            if (!await db.MasterUsers.AnyAsync(u => u.Role == "SuperAdmin"))
            {
                db.MasterUsers.Add(new MasterUser
                {
                    CompanyId = null,
                    Username = "superadmin",
                    PasswordHash = PasswordHasher.Hash(DefaultPassword),
                    FullName = "Platform Super Admin",
                    Role = "SuperAdmin"
                });
            }

            var companies = await db.Companies.ToListAsync();

            foreach (var company in companies)
            {
                // 2) Plan + subscription dates (only fills in blanks)
                if (string.IsNullOrWhiteSpace(company.Plan))
                    company.Plan = Plans.TryGetValue(company.CompanyCode, out var plan) ? plan : "Micro";

                if (company.SubscriptionStart.Year < 2000)
                {
                    company.SubscriptionStart = DateTime.UtcNow;
                    company.SubscriptionEnd = DateTime.UtcNow.AddYears(1);
                }

                // 3) One Admin per company, e.g. comp001.admin
                var hasAdmin = await db.MasterUsers
                    .AnyAsync(u => u.CompanyId == company.CompanyId && u.Role == "Admin");

                if (!hasAdmin)
                {
                    db.MasterUsers.Add(new MasterUser
                    {
                        CompanyId = company.CompanyId,
                        Username = company.CompanyCode.ToLower().Replace("-", "") + ".admin",
                        PasswordHash = PasswordHasher.Hash(DefaultPassword),
                        FullName = company.CompanyName + " Admin",
                        Role = "Admin"
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}