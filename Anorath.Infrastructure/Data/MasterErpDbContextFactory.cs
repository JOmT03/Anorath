using Anorath.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Anorath.Infrastructure.Data
{
    public class MasterErpDbContextFactory : IDesignTimeDbContextFactory<MasterErpDbContext>
    {
        public MasterErpDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<MasterErpDbContext>();


            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AnorathMasterDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;");

            return new MasterErpDbContext(optionsBuilder.Options);
        }
    }
}