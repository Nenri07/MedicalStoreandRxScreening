using FYPBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace FYPBackend.Controllers
{
    [RoutePrefix("api/Order")]
    public class OrderController : ApiController
    {
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        // ─────────────────────────────────────────────
        // 1. PLACE FROM PRESCRIPTION
        // POST api/Order/PlaceFromPrescription
        // Customer picks store from NearbyWithMedicines
        // and places order — stock deducted immediately
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("PlaceFromPrescription")]
        public IHttpActionResult PlaceFromPrescription([FromBody] OrderFromPrescriptionDto dto)
        {
            if (dto == null || dto.medicines == null || dto.medicines.Count == 0)
                return BadRequest("Invalid data. medicines list is required.");

            try
            {
                var customer = _db.customers.FirstOrDefault(c => c.c_id == dto.custId);
                var store = _db.medicalstores.FirstOrDefault(s => s.store_id == dto.storeId);
                var prescription = _db.prescriptions.FirstOrDefault(p => p.id == dto.prescriptionId);

                if (customer == null) return BadRequest("Customer not found");
                if (store == null) return BadRequest("Store not found");
                if (prescription == null) return BadRequest("Prescription not found");

                // create one order row
                var newOrder = new order
                {
                    cust_id = dto.custId,
                    store_id = dto.storeId,
                    presp_id = dto.prescriptionId,
                    order_date = DateTime.Now,
                    status = "pending",
                    location = prescription.location
                };

                _db.orders.Add(newOrder);
                _db.SaveChanges();

                var warnings = new List<string>();

                foreach (var item in dto.medicines)
                {
                    var medicine = _db.medicines.FirstOrDefault(m =>
                        m.store_id == dto.storeId &&
                        m.base_name == item.baseName);

                    if (medicine == null)
                    {
                        warnings.Add($"'{item.baseName}' not found in this store — skipped");
                        continue;
                    }

                    // deduct stock FIFO oldest expiry first
                    int needed = item.quantity;
                    var batches = _db.medicine_batches
                        .Where(b => b.med_id == medicine.med_id && b.remaining_pills > 0)
                        .OrderBy(b => b.expiry_date)
                        .ToList();

                    foreach (var batch in batches)
                    {
                        if (needed <= 0) break;
                        int deduct = Math.Min(needed, batch.remaining_pills);
                        batch.remaining_pills -= deduct;
                        needed -= deduct;
                    }

                    if (needed > 0)
                        warnings.Add($"Low stock for '{item.baseName}'. Short by {needed} pills.");

                    decimal unitPrice = 0;
                    if (medicine.price.HasValue && medicine.pills_per_pack.HasValue && medicine.pills_per_pack.Value > 0)
                        unitPrice = (decimal)medicine.price.Value / medicine.pills_per_pack.Value;

                    _db.order_items.Add(new order_items
                    {
                        order_id = newOrder.order_id,
                        medicine_id = medicine.med_id,
                        quantity = item.quantity,
                        unit_price = unitPrice,
                        med_name = medicine.name,
                        created_at = DateTime.Now
                    });
                }

                _db.SaveChanges();

                decimal totalBill = _db.order_items
                    .Where(oi => oi.order_id == newOrder.order_id)
                    .ToList()
                    .Sum(oi => oi.unit_price * oi.quantity);

                newOrder.total_bill = (int)Math.Round(totalBill);
                _db.SaveChanges();

                return Ok(new
                {
                    message = warnings.Count == 0 ? "Order placed successfully" : "Order placed with warnings",
                    orderId = newOrder.order_id,
                    totalBill = newOrder.total_bill,
                    itemCount = dto.medicines.Count - warnings.Count,
                    warnings = warnings
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // 2. STORE VIEWS INCOMING ORDERS
        // GET api/Order/GetByStore?storeId=1
        // Store sees all orders placed to them
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetByStore")]
        public IHttpActionResult GetByStore(int storeId)
        {
            var orders = _db.orders
                .Where(o => o.store_id == storeId)
                .ToList()
                .Select(o => new
                {
                    o.order_id,
                    o.cust_id,
                    customerName = o.customer != null ? o.customer.name : "",
                    o.order_date,
                    o.total_bill,
                    o.status,
                    o.location,
                    o.rider_id,
                    riderName = o.Rider != null ? o.Rider.name : "",
                    items = o.order_items.Select(i => new
                    {
                        i.med_name,
                        i.quantity,
                        i.unit_price,
                        lineTotal = i.unit_price * i.quantity
                    })
                })
                .OrderByDescending(o => o.order_date)
                .ToList();

            return Ok(orders);
        }

        // ─────────────────────────────────────────────
        // 3. STORE CONFIRMS ORDER
        // POST api/Order/Confirm
        // Accepts order + auto assigns available rider
        // from same store with least active deliveries
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("Confirm")]
        public IHttpActionResult ConfirmOrder([FromBody] ConfirmOrderDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            try
            {
                var order = _db.orders.FirstOrDefault(o => o.order_id == dto.orderId);
                if (order == null)
                    return NotFound();

                if (order.status != "pending")
                    return BadRequest($"Order is already '{order.status}', cannot confirm");

                // auto assign rider from same store
                // pick rider with status = available
                // among those pick one with fewest active orders
                var activeStatuses = new[] { "confirmed", "rider_picked", "on_the_way" };

                var rider = _db.Riders
                    .Where(r => r.med_id == order.store_id && r.status == "available")
                    .ToList()
                    .OrderBy(r => _db.orders.Count(o =>
                        o.rider_id == r.rider_id &&
                        activeStatuses.Contains(o.status)))
                    .FirstOrDefault();

                if (rider == null)
                    return BadRequest("No available rider found for this store right now");

                order.rider_id = rider.rider_id;
                order.status = "confirmed";
                _db.SaveChanges();

                return Ok(new
                {
                    message = "Order confirmed and rider assigned",
                    orderId = order.order_id,
                    status = order.status,
                    riderId = rider.rider_id,
                    riderName = rider.name,
                    riderContact = rider.contact
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // 4. STORE REJECTS ORDER
        // POST api/Order/Reject
        // Status = cancelled, stock already deducted
        // on place so no reversal needed here
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("Reject")]
        public IHttpActionResult RejectOrder([FromBody] RejectOrderDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            try
            {
                var order = _db.orders.FirstOrDefault(o => o.order_id == dto.orderId);
                if (order == null)
                    return NotFound();

                if (order.status != "pending")
                    return BadRequest($"Order is already '{order.status}', cannot reject");

                order.status = "cancelled";
                _db.SaveChanges();

                return Ok(new
                {
                    message = "Order rejected",
                    orderId = order.order_id,
                    status = order.status,
                    reason = dto.reason ?? "No reason provided"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // 5. RIDER VIEWS ASSIGNED ORDERS
        // GET api/Order/GetByRider?riderId=1
        // Rider sees orders assigned to them
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetByRider")]
        public IHttpActionResult GetByRider(int riderId)
        {
            var orders = _db.orders
                .Where(o => o.rider_id == riderId)
                .ToList()
                .Select(o => new
                {
                    o.order_id,
                    o.cust_id,
                    customerName = o.customer != null ? o.customer.name : "",
                    o.store_id,
                    storeName = o.medicalstore != null ? o.medicalstore.name : "",
                    o.order_date,
                    o.total_bill,
                    o.status,
                    o.location,
                    items = o.order_items.Select(i => new
                    {
                        i.med_name,
                        i.quantity
                    })
                })
                .OrderByDescending(o => o.order_date)
                .ToList();

            return Ok(orders);
        }

        // ─────────────────────────────────────────────
        // 6. UPDATE ORDER STATUS (rider moves pipeline)
        // PUT api/Order/UpdateStatus
        // rider_picked → on_the_way → delivered
        // ─────────────────────────────────────────────
        [HttpPut]
        [Route("UpdateStatus")]
        public IHttpActionResult UpdateStatus([FromBody] UpdateOrderStatusDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            var order = _db.orders.FirstOrDefault(o => o.order_id == dto.orderId);
            if (order == null)
                return NotFound();

            var allowed = new[]
            {
                "pending", "confirmed", "rider_picked",
                "on_the_way", "delivered", "cancelled", "rated"
            };

            if (!allowed.Contains(dto.status))
                return BadRequest("Invalid status. Allowed: pending, confirmed, rider_picked, on_the_way, delivered, cancelled, rated");

            order.status = dto.status;
            _db.SaveChanges();

            return Ok(new
            {
                message = $"Order {dto.orderId} updated to '{dto.status}'",
                orderId = order.order_id,
                status = order.status
            });
        }

        // ─────────────────────────────────────────────
        // 7. CUSTOMER TRACKS LIVE ORDER
        // GET api/Order/TrackOrder?orderId=1
        // Returns status + rider live location for map
        // Customer polls this every 10 seconds
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("TrackOrder")]
        public IHttpActionResult TrackOrder(int orderId)
        {
            var order = _db.orders.FirstOrDefault(o => o.order_id == orderId);
            if (order == null)
                return NotFound();

            object riderInfo = null;

            if (order.rider_id.HasValue)
            {
                var rider = _db.Riders.FirstOrDefault(r => r.rider_id == order.rider_id.Value);
                if (rider != null)
                {
                    riderInfo = new
                    {
                        rider.rider_id,
                        rider.name,
                        rider.contact,
                        rider.rating,
                        currentLat = rider.current_lat,
                        currentLng = rider.current_lng
                    };
                }
            }

            return Ok(new
            {
                orderId = order.order_id,
                status = order.status,
                location = order.location,
                totalBill = order.total_bill,
                rider = riderInfo
            });
        }

        // ─────────────────────────────────────────────
        // 8. GET ORDERS FOR A CUSTOMER
        // GET api/Order/GetByCustomer?custId=1
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetByCustomer")]
        public IHttpActionResult GetByCustomer(int custId)
        {
            var orders = _db.orders
                .Where(o => o.cust_id == custId)
                .ToList()
                .Select(o => new
                {
                    o.order_id,
                    o.store_id,
                    storeName = o.medicalstore != null ? o.medicalstore.name : "",
                    o.order_date,
                    o.total_bill,
                    o.status,
                    o.location,
                    items = o.order_items.Select(i => new
                    {
                        i.med_name,
                        i.quantity,
                        i.unit_price,
                        lineTotal = i.unit_price * i.quantity
                    })
                })
                .OrderByDescending(o => o.order_date)
                .ToList();

            return Ok(orders);
        }

        // ─────────────────────────────────────────────
        // 9. GET SINGLE ORDER DETAIL
        // GET api/Order/Detail?orderId=1
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("Detail")]
        public IHttpActionResult Detail(int orderId)
        {
            var o = _db.orders.FirstOrDefault(x => x.order_id == orderId);
            if (o == null)
                return NotFound();

            return Ok(new
            {
                o.order_id,
                o.cust_id,
                o.store_id,
                storeName = o.medicalstore != null ? o.medicalstore.name : "",
                o.presp_id,
                o.rider_id,
                riderName = o.Rider != null ? o.Rider.name : "",
                o.order_date,
                o.total_bill,
                o.status,
                o.location,
                items = o.order_items.Select(i => new
                {
                    i.item_id,
                    i.med_name,
                    i.medicine_id,
                    i.quantity,
                    i.unit_price,
                    lineTotal = i.unit_price * i.quantity
                })
            });
        }

        // ─────────────────────────────────────────────
        // PRIVATE HELPER
        // ─────────────────────────────────────────────
        private int CalculateBill(int orderId)
        {
            decimal total = _db.order_items
                .Where(o => o.order_id == orderId)
                .ToList()
                .Sum(o => o.unit_price * o.quantity);
            return Convert.ToInt32(total);
        }
    }

    // ─────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────
    public class OrderFromPrescriptionDto
    {
        public int custId { get; set; }
        public int storeId { get; set; }
        public int prescriptionId { get; set; }
        public List<PrescriptionMedicineOrderDto> medicines { get; set; }
    }

    public class PrescriptionMedicineOrderDto
    {
        public string baseName { get; set; }
        public int quantity { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        public int orderId { get; set; }
        public string status { get; set; }
    }

    public class ConfirmOrderDto
    {
        public int orderId { get; set; }
    }

    public class RejectOrderDto
    {
        public int orderId { get; set; }
        public string reason { get; set; }
    }
}