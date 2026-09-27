using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Anorath.Infrastructure.Data
{

    public class TenantErpDbContextFactory
        : IDesignTimeDbContextFactory<TenantErpDbContext>
    {
        public TenantErpDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder =
                new DbContextOptionsBuilder<TenantErpDbContext>();

            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;" +
                "Database=AnorathTenantTemplateDb;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True;");

            return new TenantErpDbContext(optionsBuilder.Options);
        }
    }
}