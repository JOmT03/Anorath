using System.Diagnostics;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Anorath.api.Controllers
{
    public record CompanyUpdateRequest(string CompanyName, string Plan);
    public record ExtendRequest(int Months);
    public record CompanyStatusRequest(bool IsActive);

    // =====================================================
    // SUPER ADMIN: manages tenant companies in the MASTER database only.
    // It never reads a tenant's business data.
    // =====================================================
    [ApiController]
    [Route("companies")]
    [Authorize(Roles = "SuperAdmin")]
    public class CompaniesController : ControllerBase
    {
        private static readonly string[] Plans = { "Micro", "Small", "Medium" };
        private static readonly int[] AllowedMonths = { 1, 3, 6, 12 };

        private readonly MasterErpDbContext _db;
        private readonly IConfiguration _config;

        public CompaniesController(MasterErpDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // GET companies?search=comp
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search)
        {
            try
            {
                var companies = await _db.Companies.AsNoTracking().OrderBy(c => c.CompanyCode).ToListAsync();
                var databases = await _db.CompanyDatabases.AsNoTracking().ToListAsync();
                var users = await _db.MasterUsers.AsNoTracking().Select(u => new { u.CompanyId }).ToListAsync();
                var today = DateTime.UtcNow.Date;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim();
                    companies = companies.Where(c =>
                        c.CompanyCode.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                        c.CompanyName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                        (c.Plan ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var result = companies.Select(c =>
                {
                    var db = databases.FirstOrDefault(d => d.CompanyId == c.CompanyId);
                    return new
                    {
                        c.CompanyId,
                        c.CompanyCode,
                        c.CompanyName,
                        c.Plan,
                        c.IsActive,
                        c.SubscriptionEnd,
                        daysLeft = c.SubscriptionEnd.HasValue ? (int?)(c.SubscriptionEnd.Value.Date - today).Days : null,
                        serverName = db?.ServerName ?? "(not configured)",
                        databaseName = db?.DatabaseName ?? "",
                        authMode = db == null ? "" : string.IsNullOrEmpty(db.CredentialKey) ? "Windows (Integrated)" : $"SQL Login ({db.CredentialKey})",
                        userCount = users.Count(u => Equals(u.CompanyId, c.CompanyId))
                    };
                });

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to load companies.");
            }
        }

        // PUT companies/5   (rename + change plan)
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CompanyUpdateRequest req)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
            if (company is null) return NotFound("Company not found.");

            if (string.IsNullOrWhiteSpace(req.CompanyName) || req.CompanyName.Trim().Length < 2)
                return BadRequest("Company name is required.");
            if (req.CompanyName.Trim().Length > 150)
                return BadRequest("Company name is too long.");
            if (!Plans.Contains(req.Plan))
                return BadRequest("Plan must be Micro, Small, or Medium.");

            try
            {
                company.CompanyName = req.CompanyName.Trim();
                company.Plan = req.Plan;
                await _db.SaveChangesAsync();
                return Ok(new { company.CompanyId, company.CompanyName, company.Plan });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update company.");
            }
        }

        // PUT companies/5/status   (suspend / activate)
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> SetStatus(int id, [FromBody] CompanyStatusRequest req)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
            if (company is null) return NotFound("Company not found.");

            try
            {
                company.IsActive = req.IsActive;
                await _db.SaveChangesAsync();
                return Ok(new { company.CompanyId, company.IsActive });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to change company status.");
            }
        }

        // POST companies/5/extend   { months: 1 | 3 | 6 | 12 }
        [HttpPost("{id:int}/extend")]
        public async Task<IActionResult> Extend(int id, [FromBody] ExtendRequest req)
        {
            if (!AllowedMonths.Contains(req.Months))
                return BadRequest("Extension must be 1, 3, 6, or 12 months.");

            var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
            if (company is null) return NotFound("Company not found.");

            try
            {
                // Extend from the current end date, or from today if already expired
                var now = DateTime.UtcNow;
                var from = company.SubscriptionEnd.HasValue && company.SubscriptionEnd.Value > now
                    ? company.SubscriptionEnd.Value
                    : now;
                company.SubscriptionEnd = from.AddMonths(req.Months);
                await _db.SaveChangesAsync();
                return Ok(new { company.CompanyId, company.SubscriptionEnd });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to extend subscription.");
            }
        }

        // GET companies/5/test-connection   (proves each tenant has its own reachable server)
        [HttpGet("{id:int}/test-connection")]
        public async Task<IActionResult> TestConnection(int id)
        {
            var db = await _db.CompanyDatabases.AsNoTracking().FirstOrDefaultAsync(d => d.CompanyId == id);
            if (db is null) return NotFound("This company has no database configured.");

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = db.ServerName,
                InitialCatalog = db.DatabaseName,
                TrustServerCertificate = true,
                ConnectTimeout = 8
            };

            if (string.IsNullOrEmpty(db.CredentialKey))
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                // Credentials live in appsettings, never in the database
                builder.UserID = _config[$"TenantCredentials:{db.CredentialKey}:UserId"] ?? "";
                builder.Password = _config[$"TenantCredentials:{db.CredentialKey}:Password"] ?? "";
            }

            var watch = Stopwatch.StartNew();
            try
            {
                await using var conn = new SqlConnection(builder.ConnectionString);
                await conn.OpenAsync();
                watch.Stop();
                return Ok(new
                {
                    ok = true,
                    server = db.ServerName,
                    database = db.DatabaseName,
                    serverVersion = conn.ServerVersion,
                    elapsedMs = watch.ElapsedMilliseconds,
                    message = "Connected successfully."
                });
            }
            catch (Exception ex)
            {
                watch.Stop();
                return Ok(new
                {
                    ok = false,
                    server = db.ServerName,
                    database = db.DatabaseName,
                    serverVersion = "",
                    elapsedMs = watch.ElapsedMilliseconds,
                    message = ex.Message
                });
            }
        }
    }
}