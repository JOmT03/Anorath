using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Anorath.domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Anorath.api.Services
{
    // Creates the signed login token. The API trusts only what is inside it.
    public class TokenService
    {
        private readonly IConfiguration _config;
        public TokenService(IConfiguration config) => _config = config;

        public string CreateToken(MasterUser user)
        {
            var claims = new List<Claim>
            {
                new("sub", user.MasterUserId.ToString()),
                new("name", user.Username),
                new("role", user.Role),
                new("plan", user.Company?.Plan ?? "Master"),
            };

            // Super Admin has no company
            if (user.Company != null)
                claims.Add(new("companyCode", user.Company.CompanyCode));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}