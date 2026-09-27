using Anorath.Infrastructure.Data;
using Anorath.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Anorath.api.Services;
using Microsoft.AspNetCore.Authorization;

namespace Anorath.api.Controllers
{
    public record LoginRequest(string Username, string Password);

    [ApiController]
    [Route("auth")]
    [AllowAnonymous]   // login must work without a token
    public class AuthController : ControllerBase
    {
        private readonly MasterErpDbContext _master;
        private readonly TokenService _tokens;

        public AuthController(MasterErpDbContext master, TokenService tokens)
        {
            _master = master;
            _tokens = tokens;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest login)
        {
            if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password))
                return BadRequest("Username and password are required.");

            try
            {
                var user = await _master.MasterUsers
                    .Include(u => u.Company)
                    .FirstOrDefaultAsync(u => u.Username == login.Username.Trim() && u.IsActive);

                // Same message for wrong username or wrong password (don't reveal which)
                if (user == null || !PasswordHasher.Verify(login.Password, user.PasswordHash))
                    return Unauthorized("Invalid username or password.");

                // Tenant users: company must be active and subscription not expired
                if (user.Company != null)
                {
                    if (!user.Company.IsActive)
                        return StatusCode(403, "This company account is suspended. Please contact the administrator.");

                    if (user.Company.SubscriptionEnd.HasValue && user.Company.SubscriptionEnd.Value < DateTime.UtcNow)
                        return StatusCode(403, "Subscription expired. Please contact the administrator to renew.");
                }

                return Ok(new
                {
                    token = _tokens.CreateToken(user),
                    userId = user.MasterUserId,
                    username = user.Username,
                    fullName = user.FullName,
                    role = user.Role,
                    companyCode = user.Company?.CompanyCode,   // null for Super Admin
                    companyName = user.Company?.CompanyName,
                    plan = user.Company?.Plan ?? "Master"      // Micro / Small / Medium / Master
                });
            }
            catch (Exception)
            {
                return StatusCode(500, "Login failed due to a server error.");
            }
        }
    }
}