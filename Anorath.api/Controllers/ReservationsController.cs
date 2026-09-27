using Anorath.domain.Entities.Reception;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    [ApiController]
    [Route("reservations")]
    public class ReservationsController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public ReservationsController(TenantErpDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var reservations = await _db.Reservations.AsNoTracking().ToListAsync();
            return Ok(reservations);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Reservations reservation)
        {
            if (reservation.CustomerId <= 0 || reservation.RoomId <= 0)
                return BadRequest("A valid CustomerId and RoomId are required.");
            if (reservation.CheckOut <= reservation.CheckIn)
                return BadRequest("CheckOut must be after CheckIn.");

            try
            {
                _db.Reservations.Add(reservation);
                await _db.SaveChangesAsync();
                return Created($"/reservations/{reservation.ReservationId}", reservation);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create reservation.");
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Reservations updated)
        {
            var reservation = await _db.Reservations.FindAsync(id);
            if (reservation is null) return NotFound();

            if (updated.CheckOut <= updated.CheckIn)
                return BadRequest("CheckOut must be after CheckIn.");

            try
            {
                reservation.CustomerId = updated.CustomerId;
                reservation.RoomId = updated.RoomId;
                reservation.ReservationDate = updated.ReservationDate;
                reservation.CheckIn = updated.CheckIn;
                reservation.CheckOut = updated.CheckOut;
                reservation.NumberOfGuests = updated.NumberOfGuests;
                reservation.Status = updated.Status;
                reservation.Remarks = updated.Remarks;
                await _db.SaveChangesAsync();
                return Ok(reservation);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update reservation.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var reservation = await _db.Reservations.FindAsync(id);
            if (reservation is null) return NotFound();

            try
            {
                _db.Reservations.Remove(reservation);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete reservation.");
            }
        }
    }
}