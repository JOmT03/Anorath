using System.Threading.Tasks;

namespace Anorath.Infrastructure.Services
{
    public interface ITenantService
    {
        string? GetTenantCode();

        Task<string> GetConnectionStringAsync();
    }
}