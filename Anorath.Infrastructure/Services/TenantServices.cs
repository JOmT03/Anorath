using System;
using System.Linq;
using System.Threading.Tasks;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Anorath.Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly MasterErpDbContext _masterDbContext;
        private readonly IConfiguration _configuration;

        public TenantService(
            IHttpContextAccessor httpContextAccessor,
            MasterErpDbContext masterDbContext,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _masterDbContext = masterDbContext;
            _configuration = configuration;
        }

        public string? GetTenantCode()
        {
            var user = _httpContextAccessor.HttpContext?.User;

            // The company comes ONLY from the signed token, never from a header
            if (user?.Identity?.IsAuthenticated == true)
                return user.FindFirst("companyCode")?.Value;

            return null;
        }

        // Builds the tenant connection string:
        //   server + database  -> from the Master DB (CompanyDatabases)
        //   user + password    -> from appsettings "TenantCredentials:<CredentialKey>"
        public async Task<string> GetConnectionStringAsync()
        {
            var tenantCode = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenantCode))
                throw new InvalidOperationException("No company found in the login token.");

            var db = await (
                from company in _masterDbContext.Companies
                join database in _masterDbContext.CompanyDatabases
                    on company.CompanyId equals database.CompanyId
                where company.CompanyCode == tenantCode && company.IsActive && database.IsActive
                select database
            ).AsNoTracking().FirstOrDefaultAsync();

            if (db == null)
                throw new InvalidOperationException($"No active database found for tenant {tenantCode}.");

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = db.ServerName,
                InitialCatalog = db.DatabaseName,
                TrustServerCertificate = true,
                MultipleActiveResultSets = true
            };

            if (string.IsNullOrWhiteSpace(db.CredentialKey))
            {
                // Local server: use the Windows login
                builder.IntegratedSecurity = true;
            }
            else
            {
                // Hosted server: credentials stay in appsettings, never in the database
                var userId = _configuration[$"TenantCredentials:{db.CredentialKey}:UserId"];
                var password = _configuration[$"TenantCredentials:{db.CredentialKey}:Password"];

                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException($"Credentials not found for key '{db.CredentialKey}'.");

                builder.UserID = userId;
                builder.Password = password;
                builder.Encrypt = true;
            }

            return builder.ConnectionString;
        }
    }
}