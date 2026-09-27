using System.Text.RegularExpressions;
using Anorath.domain.Entities;
using Anorath.Infrastructure.Data;
using Anorath.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    public record CreateUserRequest(string Username, string FullName, string Password, string Role);
    public record UpdateUserRequest(string FullName, string Role, bool IsActive, string? Password);

    // Only a company's Admin can manage accounts, and only for their own company.
    [ApiController]
    [Route("users")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly MasterErpDbContext _master;
        public UsersController(MasterErpDbContext master) => _master = master;

        // Roles an Admin may create, based on the company's plan
        private static readonly Dictionary<string, string[]> RolesByPlan = new()
        {
            ["Micro"] = new[] { "Receptionist", "Cashier" },
            ["Small"] = new[] { "Manager", "Receptionist", "Cashier" },
            ["Medium"] = new[] { "BranchManager", "Manager", "Receptionist", "Cashier" },
        };

        // The company comes from the signed token, so an Admin can't touch another company
        private async Task<Company?> MyCompanyAsync()
        {
            var code = User.FindFirst("companyCode")?.Value;
            if (string.IsNullOrWhiteSpace(code)) return null;
            return await _master.Companies.FirstOrDefaultAsync(c => c.CompanyCode == code);
        }

        private static string[] AllowedRoles(Company company) =>
            RolesByPlan.TryGetValue(company.Plan, out var roles) ? roles : Array.Empty<string>();

        private static string? ValidatePassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return "Password must be at least 8 characters.";
            if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
                return "Password must contain letters and numbers.";
            return null;
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var company = await MyCompanyAsync();
            if (company == null) return Forbid();
            return Ok(AllowedRoles(company));
        }

        // GET users?search=ana  (search by username, name or role)
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search)
        {
            var company = await MyCompanyAsync();
            if (company == null) return Forbid();

            var query = _master.MasterUsers.Where(u => u.CompanyId == company.CompanyId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(u => u.Username.Contains(s) || u.FullName.Contains(s) || u.Role.Contains(s));
            }

            var users = await query
                .OrderBy(u => u.Role).ThenBy(u => u.Username)
                .Select(u => new
                {
                    id = u.MasterUserId,
                    username = u.Username,
                    fullName = u.FullName,
                    role = u.Role,
                    isActive = u.IsActive
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
        {
            var company = await MyCompanyAsync();
            if (company == null) return Forbid();

            // Validation
            if (string.IsNullOrWhiteSpace(req.Username) || !Regex.IsMatch(req.Username.Trim(), @"^[a-zA-Z0-9._-]{3,50}$"))
                return BadRequest("Username must be 3-50 characters: letters, numbers, dot, dash or underscore.");
            if (string.IsNullOrWhiteSpace(req.FullName))
                return BadRequest("Full name is required.");
            if (ValidatePassword(req.Password) is string pwError)
                return BadRequest(pwError);
            if (!AllowedRoles(company).Contains(req.Role))
                return BadRequest($"Role '{req.Role}' is not available on the {company.Plan} plan.");

            var username = req.Username.Trim().ToLower();
            if (await _master.MasterUsers.AnyAsync(u => u.Username == username))
                return Conflict("Username is already taken.");

            try
            {
                var user = new MasterUser
                {
                    CompanyId = company.CompanyId,
                    Username = username,
                    FullName = req.FullName.Trim(),
                    PasswordHash = PasswordHasher.Hash(req.Password),
                    Role = req.Role
                };
                _master.MasterUsers.Add(user);
                await _master.SaveChangesAsync();
                return Created($"/users/{user.MasterUserId}", new { id = user.MasterUserId });
            }
            catch (Exception)
            {
                return StatusCode(500, "Could not create the user.");
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
        {
            var company = await MyCompanyAsync();
            if (company == null) return Forbid();

            var user = await _master.MasterUsers
                .FirstOrDefaultAsync(u => u.MasterUserId == id && u.CompanyId == company.CompanyId);
            if (user == null) return NotFound("User not found.");
            if (user.Role == "Admin") return BadRequest("Admin accounts cannot be changed here.");

            if (string.IsNullOrWhiteSpace(req.FullName))
                return BadRequest("Full name is required.");
            if (!AllowedRoles(company).Contains(req.Role))
                return BadRequest($"Role '{req.Role}' is not available on the {company.Plan} plan.");

            // Password is optional on edit: only change it when a new one is typed
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                if (ValidatePassword(req.Password) is string pwError) return BadRequest(pwError);
                user.PasswordHash = PasswordHasher.Hash(req.Password);
            }

            user.FullName = req.FullName.Trim();
            user.Role = req.Role;
            user.IsActive = req.IsActive;

            await _master.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var company = await MyCompanyAsync();
            if (company == null) return Forbid();

            var user = await _master.MasterUsers
                .FirstOrDefaultAsync(u => u.MasterUserId == id && u.CompanyId == company.CompanyId);
            if (user == null) return NotFound("User not found.");
            if (user.Role == "Admin") return BadRequest("Admin accounts cannot be deleted.");

            _master.MasterUsers.Remove(user);
            await _master.SaveChangesAsync();
            return NoContent();
        }
    }
}