using FYPBackend.Models;
using System;
using System.Linq;
using System.Web.Http;

namespace FYPBackend.Controllers
{
    [RoutePrefix("api/Rider")]
    public class RiderController : ApiController
    {
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        // ─────────────────────────────────────────────
        // 1. RIDER UPDATES LIVE LOCATION
        // PUT api/Rider/UpdateLocation
        // Rider app calls every 10 seconds while on delivery
        // ─────────────────────────────────────────────
        [HttpPut]
        [Route("UpdateLocation")]
        public IHttpActionResult UpdateLocation([FromBody] UpdateLocationDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            var rider = _db.Riders.FirstOrDefault(r => r.rider_id == dto.riderId);
            if (rider == null)
                return NotFound();

            rider.current_lat = (decimal)dto.lat;
            rider.current_lng = (decimal)dto.lng;
            _db.SaveChanges();

            return Ok(new { message = "Location updated" });
        }

        // ─────────────────────────────────────────────
        // 2. RIDER UPDATES OWN AVAILABILITY
        // PUT api/Rider/UpdateStatus
        // available / busy / offline
        // ─────────────────────────────────────────────
        [HttpPut]
        [Route("UpdateStatus")]
        public IHttpActionResult UpdateStatus([FromBody] UpdateRiderStatusDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            var rider = _db.Riders.FirstOrDefault(r => r.rider_id == dto.riderId);
            if (rider == null)
                return NotFound();

            var allowed = new[] { "available", "busy", "offline" };
            if (!allowed.Contains(dto.status))
                return BadRequest("Invalid status. Allowed: available, busy, offline");

            rider.status = dto.status;
            _db.SaveChanges();

            return Ok(new
            {
                message = $"Rider status updated to '{dto.status}'",
                riderId = rider.rider_id,
                status = rider.status
            });
        }

        // ─────────────────────────────────────────────
        // 3. CUSTOMER RATES RIDER
        // POST api/Rider/Rate
        // Only allowed after order is delivered
        // Updates running average rating on rider
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("Rate")]
        public IHttpActionResult RateRider([FromBody] RateRiderDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            if (dto.stars < 1 || dto.stars > 5)
                return BadRequest("Rating must be between 1 and 5");

            try
            {
                var order = _db.orders.FirstOrDefault(o =>
                    o.order_id == dto.orderId &&
                    o.cust_id == dto.custId);

                if (order == null)
                    return NotFound();

                if (order.status != "delivered")
                    return BadRequest("Can only rate after order is delivered");

                if (!order.rider_id.HasValue)
                    return BadRequest("No rider assigned to this order");

                var rider = _db.Riders.FirstOrDefault(r => r.rider_id == order.rider_id.Value);
                if (rider == null)
                    return NotFound();

                // running average: (oldAvg * oldCount + newStars) / (oldCount + 1)
                int prevCount = rider.total_orders ?? 0;
                decimal prevRating = rider.rating ?? 0;

                decimal newRating = prevCount == 0
                    ? dto.stars
                    : (prevRating * prevCount + dto.stars) / (prevCount + 1);

                rider.rating = Math.Round(newRating, 2);
                rider.total_orders = prevCount + 1;
                order.status = "rated";

                _db.SaveChanges();

                return Ok(new
                {
                    message = "Rating submitted successfully",
                    riderId = rider.rider_id,
                    newRating = rider.rating,
                    totalOrders = rider.total_orders
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // 4. GET RIDERS FOR A STORE
        // GET api/Rider/GetByStore?storeId=1
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetByStore")]
        public IHttpActionResult GetByStore(int storeId)
        {
            var riders = _db.Riders
                .Where(r => r.med_id == storeId)
                .Select(r => new
                {
                    r.rider_id,
                    r.name,
                    r.contact,
                    r.status,
                    r.rating,
                    r.total_orders,
                    r.current_lat,
                    r.current_lng
                })
                .ToList();

            return Ok(riders);
        }
    }

    // ─────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────
    public class UpdateLocationDto
    {
        public int riderId { get; set; }
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class UpdateRiderStatusDto
    {
        public int riderId { get; set; }
        public string status { get; set; }
    }

    public class RateRiderDto
    {
        public int orderId { get; set; }
        public int custId { get; set; }
        public decimal stars { get; set; }
    }
}