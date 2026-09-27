namespace Anorath.domain.Entities
{
    // Where each company's (tenant's) database lives. No passwords here:
    // credentials are looked up in appsettings by CredentialKey.
    public class CompanyDatabase
    {
        public int CompanyDatabaseId { get; set; }
        public int CompanyId { get; set; }

        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;

        // Empty = Windows login (local server). Otherwise e.g. "TenantA" in appsettings.
        public string CredentialKey { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public Company? Company { get; set; }
    }
}