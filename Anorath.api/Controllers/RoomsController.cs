using Anorath.domain.Entities.Reception;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    [ApiController]
    [Route("rooms")]
    public class RoomsController : ControllerBase
    {
        private readonly TenantErpDbContext _db;
        public RoomsController(TenantErpDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rooms = await _db.Rooms.AsNoTracking().ToListAsync();
            return Ok(rooms);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Room room)
        {
            if (string.IsNullOrWhiteSpace(room.RoomNumber) || string.IsNullOrWhiteSpace(room.RoomType))
                return BadRequest("RoomNumber and RoomType are required.");

            try
            {
                _db.Rooms.Add(room);
                await _db.SaveChangesAsync();
                return Created($"/rooms/{room.RoomId}", room);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create room.");
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Room updated)
        {
            var room = await _db.Rooms.FindAsync(id);
            if (room is null) return NotFound();

            if (string.IsNullOrWhiteSpace(updated.RoomNumber) || string.IsNullOrWhiteSpace(updated.RoomType))
                return BadRequest("RoomNumber and RoomType are required.");

            try
            {
                room.RoomNumber = updated.RoomNumber;
                room.RoomType = updated.RoomType;
                room.Rate = updated.Rate;
                room.Status = updated.Status;
                room.IsActive = updated.IsActive;
                await _db.SaveChangesAsync();
                return Ok(room);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update room.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var room = await _db.Rooms.FindAsync(id);
            if (room is null) return NotFound();

            try
            {
                _db.Rooms.Remove(room);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to delete room.");
            }
        }
    }
}