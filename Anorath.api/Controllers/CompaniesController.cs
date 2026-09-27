using Anorath.domain.Entities;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace Anorath.api.Controllers
{
    [ApiController]
    [Route("companies")]
    public class CompaniesController : ControllerBase
    {
        private readonly MasterErpDbContext _db;
        public CompaniesController(MasterErpDbContext db) => _db = db;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Company company)
        {
            if (string.IsNullOrWhiteSpace(company.CompanyCode) || string.IsNullOrWhiteSpace(company.CompanyName))
                return BadRequest("CompanyCode and CompanyName are required.");

            try
            {
                _db.Companies.Add(company);
                await _db.SaveChangesAsync();
                return Created($"/companies/{company.CompanyId}", company);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create company.");
            }
        }
    }
}