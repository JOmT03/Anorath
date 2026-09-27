using Anorath.domain.Entities.Reception;
using Anorath.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anorath.api.Controllers
{
    public record ReservationRequest(int CustomerId, int RoomId, DateTime CheckIn, DateTime CheckOut, int NumberOfGuests, string? Remarks);
    public record ReservationStatusRequest(string Status);

    [ApiController]
    [Route("reservations")]
    [Authorize(Roles = "Admin,Receptionist")]
    public class ReservationsController : ControllerBase
    {
        // Statuses that hold the room (block overlapping bookings)
        private static readonly string[] ActiveStatuses = { "Pending", "Confirmed", "CheckedIn" };

        // Allowed status changes (the booking life cycle)
        private static readonly Dictionary<string, string[]> NextStatuses = new()
        {
            ["Pending"] = new[] { "Confirmed", "Cancelled" },
            ["Confirmed"] = new[] { "CheckedIn", "Cancelled" },
            ["CheckedIn"] = new[] { "CheckedOut" },
            ["CheckedOut"] = Array.Empty<string>(),
            ["Cancelled"] = Array.Empty<string>(),
        };

        private readonly TenantErpDbContext _db;
        public ReservationsController(TenantErpDbContext db) => _db = db;

        private static int Nights(DateTime checkIn, DateTime checkOut) =>
            Math.Max(1, (checkOut.Date - checkIn.Date).Days);

        // GET reservations?search=juan&status=Confirmed
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? status)
        {
            var query =
                from r in _db.Reservations.AsNoTracking()
                join c in _db.Customers.AsNoTracking() on r.CustomerId equals c.CustomerId
                join rm in _db.Rooms.AsNoTracking() on r.RoomId equals rm.RoomId
                select new { r, c, rm };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(x => x.c.CustomerName.Contains(s) || x.c.CustomerCode.Contains(s) || x.rm.RoomNumber.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
                query = query.Where(x => x.r.Status == status);

            var rows = await query.OrderByDescending(x => x.r.CheckIn).ToListAsync();

            return Ok(rows.Select(x => new
            {
                reservationId = x.r.ReservationId,
                customerId = x.c.CustomerId,
                customerName = x.c.CustomerName,
                roomId = x.rm.RoomId,
                roomNumber = x.rm.RoomNumber,
                roomType = x.rm.RoomType,
                checkIn = x.r.CheckIn,
                checkOut = x.r.CheckOut,
                numberOfGuests = x.r.NumberOfGuests,
                nights = Nights(x.r.CheckIn, x.r.CheckOut),
                amount = Nights(x.r.CheckIn, x.r.CheckOut) * x.rm.Rate,
                status = x.r.Status,
                remarks = x.r.Remarks
            }));
        }

        private async Task<string?> ValidateAsync(ReservationRequest req, int? currentId)
        {
            var customer = await _db.Customers.FindAsync(req.CustomerId);
            if (customer == null || !customer.IsActive) return "Please choose an active customer.";

            var room = await _db.Rooms.FindAsync(req.RoomId);
            if (room == null || !room.IsActive) return "Please choose an active room.";
            if (room.Status == "Maintenance") return $"Room {room.RoomNumber} is under maintenance.";

            var checkIn = req.CheckIn.Date;
            var checkOut = req.CheckOut.Date;
            if (checkOut <= checkIn) return "Check-out must be after check-in.";
            if (currentId == null && checkIn < DateTime.Today) return "Check-in date cannot be in the past.";
            if ((checkOut - checkIn).Days > 60) return "A stay cannot be longer than 60 nights.";
            if (req.NumberOfGuests < 1 || req.NumberOfGuests > 20) return "Number of guests must be between 1 and 20.";

            // No double booking: same room, overlapping dates, still active
            var overlap = await _db.Reservations.AnyAsync(r =>
                r.RoomId == req.RoomId &&
                r.ReservationId != (currentId ?? 0) &&
                ActiveStatuses.Contains(r.Status) &&
                r.CheckIn < checkOut && checkIn < r.CheckOut);
            if (overlap) return $"Room {room.RoomNumber} is already booked for those dates.";

            return null;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ReservationRequest req)
        {
            if (await ValidateAsync(req, null) is string error) return BadRequest(error);

            try
            {
                var reservation = new Reservations
                {
                    CustomerId = req.CustomerId,
                    RoomId = req.RoomId,
                    ReservationDate = DateTime.Now,
                    CheckIn = req.CheckIn.Date,
                    CheckOut = req.CheckOut.Date,
                    NumberOfGuests = req.NumberOfGuests,
                    Status = "Pending",
                    Remarks = req.Remarks?.Trim() ?? ""
                };
                _db.Reservations.Add(reservation);
                await _db.SaveChangesAsync();
                return Created($"/reservations/{reservation.ReservationId}", new { id = reservation.ReservationId });
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to create reservation.");
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ReservationRequest req)
        {
            var reservation = await _db.Reservations.FindAsync(id);
            if (reservation is null) return NotFound("Reservation not found.");
            if (reservation.Status != "Pending" && reservation.Status != "Confirmed")
                return BadRequest($"A {reservation.Status} reservation can no longer be edited.");

            if (await ValidateAsync(req, id) is string error) return BadRequest(error);

            try
            {
                reservation.CustomerId = req.CustomerId;
                reservation.RoomId = req.RoomId;
                reservation.CheckIn = req.CheckIn.Date;
                reservation.CheckOut = req.CheckOut.Date;
                reservation.NumberOfGuests = req.NumberOfGuests;
                reservation.Remarks = req.Remarks?.Trim() ?? "";
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update reservation.");
            }
        }

        // PUT reservations/5/status  { "status": "CheckedIn" }
        // Also keeps the room status in sync (Occupied on check-in, Available on check-out)
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ReservationStatusRequest req)
        {
            var reservation = await _db.Reservations.FindAsync(id);
            if (reservation is null) return NotFound("Reservation not found.");

            var allowed = NextStatuses.TryGetValue(reservation.Status, out var next) ? next : Array.Empty<string>();
            if (!allowed.Contains(req.Status))
                return BadRequest($"Cannot change a {reservation.Status} reservation to {req.Status}.");

            var room = await _db.Rooms.FindAsync(reservation.RoomId);

            try
            {
                reservation.Status = req.Status;

                if (room != null)
                {
                    if (req.Status == "CheckedIn") room.Status = "Occupied";
                    if (req.Status == "CheckedOut") room.Status = "Available";
                }

                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to update the status.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var reservation = await _db.Reservations.FindAsync(id);
            if (reservation is null) return NotFound("Reservation not found.");

            // Only bookings that never earned money can be removed
            if (reservation.Status != "Pending" && reservation.Status != "Cancelled")
                return Conflict($"A {reservation.Status} reservation is part of the revenue records and cannot be deleted.");

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