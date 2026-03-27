//using FYPBackend.Models;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Web.Http;

//namespace FYPBackend.Controllers
//{
//    [RoutePrefix("api/Order")]
//    public class OrderController : ApiController
//    {
//        private readonly fyp1Entities1 _db = new fyp1Entities1();

//        // ─────────────────────────────────────────────
//        // 1. PLACE FROM PRESCRIPTION
//        // POST api/Order/PlaceFromPrescription
//        // Customer picks store from NearbyWithMedicines
//        // and places order — stock deducted immediately
//        // ─────────────────────────────────────────────
//        [HttpPost]
//        [Route("PlaceFromPrescription")]
//        public IHttpActionResult PlaceFromPrescription([FromBody] OrderFromPrescriptionDto dto)
//        {
//            if (dto == null || dto.medicines == null || dto.medicines.Count == 0)
//                return BadRequest("Invalid data. medicines list is required.");

//            try
//            {
//                var customer = _db.customers.FirstOrDefault(c => c.c_id == dto.custId);
//                var store = _db.medicalstores.FirstOrDefault(s => s.store_id == dto.storeId);
//                var prescription = _db.prescriptions.FirstOrDefault(p => p.id == dto.prescriptionId);

//                if (customer == null) return BadRequest("Customer not found");
//                if (store == null) return BadRequest("Store not found");
//                if (prescription == null) return BadRequest("Prescription not found");

//                // create one order row
//                var newOrder = new order
//                {
//                    cust_id = dto.custId,
//                    store_id = dto.storeId,
//                    presp_id = dto.prescriptionId,
//                    order_date = DateTime.Now,
//                    status = "pending",
//                    location = prescription.location
//                };

//                _db.orders.Add(newOrder);
//                _db.SaveChanges();

//                var warnings = new List<string>();

//                foreach (var item in dto.medicines)
//                {
//                    var medicine = _db.medicines.FirstOrDefault(m =>
//                        m.store_id == dto.storeId &&
//                        m.base_name == item.baseName);

//                    if (medicine == null)
//                    {
//                        warnings.Add($"'{item.baseName}' not found in this store — skipped");
//                        continue;
//                    }

//                    // deduct stock FIFO oldest expiry first
//                    int needed = item.quantity;
//                    var batches = _db.medicine_batches
//                        .Where(b => b.med_id == medicine.med_id && b.remaining_pills > 0)
//                        .OrderBy(b => b.expiry_date)
//                        .ToList();

//                    foreach (var batch in batches)
//                    {
//                        if (needed <= 0) break;
//                        int deduct = Math.Min(needed, batch.remaining_pills);
//                        batch.remaining_pills -= deduct;
//                        needed -= deduct;
//                    }

//                    if (needed > 0)
//                        warnings.Add($"Low stock for '{item.baseName}'. Short by {needed} pills.");

//                    decimal unitPrice = 0;
//                    if (medicine.price.HasValue && medicine.pills_per_pack.HasValue && medicine.pills_per_pack.Value > 0)
//                        unitPrice = (decimal)medicine.price.Value / medicine.pills_per_pack.Value;

//                    _db.order_items.Add(new order_items
//                    {
//                        order_id = newOrder.order_id,
//                        medicine_id = medicine.med_id,
//                        quantity = item.quantity,
//                        unit_price = unitPrice,
//                        med_name = medicine.name,
//                        created_at = DateTime.Now
//                    });
//                }

//                _db.SaveChanges();

//                decimal totalBill = _db.order_items
//                    .Where(oi => oi.order_id == newOrder.order_id)
//                    .ToList()
//                    .Sum(oi => oi.unit_price * oi.quantity);

//                newOrder.total_bill = (int)Math.Round(totalBill);
//                _db.SaveChanges();

//                return Ok(new
//                {
//                    message = warnings.Count == 0 ? "Order placed successfully" : "Order placed with warnings",
//                    orderId = newOrder.order_id,
//                    totalBill = newOrder.total_bill,
//                    itemCount = dto.medicines.Count - warnings.Count,
//                    warnings = warnings
//                });
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ex.InnerException?.Message ?? ex.Message);
//            }
//        }

//        // ─────────────────────────────────────────────
//        // 2. STORE VIEWS INCOMING ORDERS
//        // GET api/Order/GetByStore?storeId=1
//        // Store sees all orders placed to them
//        // ─────────────────────────────────────────────
//        [HttpGet]
//        [Route("GetByStore")]
//        public IHttpActionResult GetByStore(int storeId)
//        {
//            var orders = _db.orders
//                .Where(o => o.store_id == storeId)
//                .ToList()
//                .Select(o => new
//                {
//                    o.order_id,
//                    o.cust_id,
//                    customerName = o.customer != null ? o.customer.name : "",
//                    o.order_date,
//                    o.total_bill,
//                    o.status,
//                    o.location,
//                    o.rider_id,
//                    riderName = o.Rider != null ? o.Rider.name : "",
//                    items = o.order_items.Select(i => new
//                    {
//                        i.med_name,
//                        i.quantity,
//                        i.unit_price,
//                        lineTotal = i.unit_price * i.quantity
//                    })
//                })
//                .OrderByDescending(o => o.order_date)
//                .ToList();

//            return Ok(orders);
//        }

//        // ─────────────────────────────────────────────
//        // 3. STORE CONFIRMS ORDER
//        // POST api/Order/Confirm
//        // Accepts order + auto assigns available rider
//        // from same store with least active deliveries
//        // ─────────────────────────────────────────────
//        [HttpPost]
//        [Route("Confirm")]
//        public IHttpActionResult ConfirmOrder([FromBody] ConfirmOrderDto dto)
//        {
//            if (dto == null)
//                return BadRequest("Invalid data");

//            try
//            {
//                var order = _db.orders.FirstOrDefault(o => o.order_id == dto.orderId);
//                if (order == null)
//                    return NotFound();

//                if (order.status != "pending")
//                    return BadRequest($"Order is already '{order.status}', cannot confirm");

//                // auto assign rider from same store
//                // pick rider with status = available
//                // among those pick one with fewest active orders
//                var activeStatuses = new[] { "confirmed", "rider_picked", "on_the_way" };

//                var rider = _db.Riders
//                    .Where(r => r.med_id == order.store_id && r.status == "available")
//                    .ToList()
//                    .OrderBy(r => _db.orders.Count(o =>
//                        o.rider_id == r.rider_id &&
//                        activeStatuses.Contains(o.status)))
//                    .FirstOrDefault();

//                if (rider == null)
//                    return BadRequest("No available rider found for this store right now");

//                order.rider_id = rider.rider_id;
//                order.status = "confirmed";
//                _db.SaveChanges();

//                return Ok(new
//                {
//                    message = "Order confirmed and rider assigned",
//                    orderId = order.order_id,
//                    status = order.status,
//                    riderId = rider.rider_id,
//                    riderName = rider.name,
//                    riderContact = rider.contact
//                });
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ex.InnerException?.Message ?? ex.Message);
//            }
//        }

//        // ─────────────────────────────────────────────
//        // 4. STORE REJECTS ORDER
//        // POST api/Order/Reject
//        // Status = cancelled, stock already deducted
//        // on place so no reversal needed here
//        // ─────────────────────────────────────────────
//        [HttpPost]
//        [Route("Reject")]
//        public IHttpActionResult RejectOrder([FromBody] RejectOrderDto dto)
//        {
//            if (dto == null)
//                return BadRequest("Invalid data");

//            try
//            {
//                var order = _db.orders.FirstOrDefault(o => o.order_id == dto.orderId);
//                if (order == null)
//                    return NotFound();

//                if (order.status != "pending")
//                    return BadRequest($"Order is already '{order.status}', cannot reject");

//                order.status = "cancelled";
//                _db.SaveChanges();

//                return Ok(new
//                {
//                    message = "Order rejected",
//                    orderId = order.order_id,
//                    status = order.status,
//                    reason = dto.reason ?? "No reason provided"
//                });
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ex.InnerException?.Message ?? ex.Message);
//            }
//        }

//        // ─────────────────────────────────────────────
//        // 5. RIDER VIEWS ASSIGNED ORDERS
//        // GET api/Order/GetByRider?riderId=1
//        // Rider sees orders assigned to them
//        // ─────────────────────────────────────────────
//        [HttpGet]
//        [Route("GetByRider")]
//        public IHttpActionResult GetByRider(int riderId)
//        {
//            var orders = _db.orders
//                .Where(o => o.rider_id == riderId)
//                .ToList()
//                .Select(o => new
//                {
//                    o.order_id,
//                    o.cust_id,
//                    customerName = o.customer != null ? o.customer.name : "",
//                    o.store_id,
//                    storeName = o.medicalstore != null ? o.medicalstore.name : "",
//                    o.order_date,
//                    o.total_bill,
//                    o.status,
//                    o.location,
//                    items = o.order_items.Select(i => new
//                    {
//                        i.med_name,
//                        i.quantity
//                    })
//                })
//                .OrderByDescending(o => o.order_date)
//                .ToList();

//            return Ok(orders);
//        }

//        // ─────────────────────────────────────────────
//        // 6. UPDATE ORDER STATUS (rider moves pipeline)
//        // PUT api/Order/UpdateStatus
//        // rider_picked → on_the_way → delivered
//        // ─────────────────────────────────────────────
//        [HttpPut]
//        [Route("UpdateStatus")]
//        public IHttpActionResult UpdateStatus([FromBody] UpdateOrderStatusDto dto)
//        {
//            if (dto == null)
//                return BadRequest("Invalid data");

//            var order = _db.orders.FirstOrDefault(o => o.order_id == dto.orderId);
//            if (order == null)
//                return NotFound();

//            var allowed = new[]
//            {
//                "pending", "confirmed", "rider_picked",
//                "on_the_way", "delivered", "cancelled", "rated"
//            };

//            if (!allowed.Contains(dto.status))
//                return BadRequest("Invalid status. Allowed: pending, confirmed, rider_picked, on_the_way, delivered, cancelled, rated");

//            order.status = dto.status;
//            _db.SaveChanges();

//            return Ok(new
//            {
//                message = $"Order {dto.orderId} updated to '{dto.status}'",
//                orderId = order.order_id,
//                status = order.status
//            });
//        }

//        // ─────────────────────────────────────────────
//        // 7. CUSTOMER TRACKS LIVE ORDER
//        // GET api/Order/TrackOrder?orderId=1
//        // Returns status + rider live location for map
//        // Customer polls this every 10 seconds
//        // ─────────────────────────────────────────────
//        [HttpGet]
//        [Route("TrackOrder")]
//        public IHttpActionResult TrackOrder(int orderId)
//        {
//            var order = _db.orders.FirstOrDefault(o => o.order_id == orderId);
//            if (order == null)
//                return NotFound();

//            object riderInfo = null;

//            if (order.rider_id.HasValue)
//            {
//                var rider = _db.Riders.FirstOrDefault(r => r.rider_id == order.rider_id.Value);
//                if (rider != null)
//                {
//                    riderInfo = new
//                    {
//                        rider.rider_id,
//                        rider.name,
//                        rider.contact,
//                        rider.rating,
//                        currentLat = rider.current_lat,
//                        currentLng = rider.current_lng
//                    };
//                }
//            }

//            return Ok(new
//            {
//                orderId = order.order_id,
//                status = order.status,
//                location = order.location,
//                totalBill = order.total_bill,
//                rider = riderInfo
//            });
//        }

//        // ─────────────────────────────────────────────
//        // 8. GET ORDERS FOR A CUSTOMER
//        // GET api/Order/GetByCustomer?custId=1
//        // ─────────────────────────────────────────────
//        [HttpGet]
//        [Route("GetByCustomer")]
//        public IHttpActionResult GetByCustomer(int custId)
//        {
//            var orders = _db.orders
//                .Where(o => o.cust_id == custId)
//                .ToList()
//                .Select(o => new
//                {
//                    o.order_id,
//                    o.store_id,
//                    storeName = o.medicalstore != null ? o.medicalstore.name : "",
//                    o.order_date,
//                    o.total_bill,
//                    o.status,
//                    o.location,
//                    items = o.order_items.Select(i => new
//                    {
//                        i.med_name,
//                        i.quantity,
//                        i.unit_price,
//                        lineTotal = i.unit_price * i.quantity
//                    })
//                })
//                .OrderByDescending(o => o.order_date)
//                .ToList();

//            return Ok(orders);
//        }

//        // ─────────────────────────────────────────────
//        // 9. GET SINGLE ORDER DETAIL
//        // GET api/Order/Detail?orderId=1
//        // ─────────────────────────────────────────────
//        [HttpGet]
//        [Route("Detail")]
//        public IHttpActionResult Detail(int orderId)
//        {
//            var o = _db.orders.FirstOrDefault(x => x.order_id == orderId);
//            if (o == null)
//                return NotFound();

//            return Ok(new
//            {
//                o.order_id,
//                o.cust_id,
//                o.store_id,
//                storeName = o.medicalstore != null ? o.medicalstore.name : "",
//                o.presp_id,
//                o.rider_id,
//                riderName = o.Rider != null ? o.Rider.name : "",
//                o.order_date,
//                o.total_bill,
//                o.status,
//                o.location,
//                items = o.order_items.Select(i => new
//                {
//                    i.item_id,
//                    i.med_name,
//                    i.medicine_id,
//                    i.quantity,
//                    i.unit_price,
//                    lineTotal = i.unit_price * i.quantity
//                })
//            });
//        }

//        // ─────────────────────────────────────────────
//        // PRIVATE HELPER
//        // ─────────────────────────────────────────────
//        private int CalculateBill(int orderId)
//        {
//            decimal total = _db.order_items
//                .Where(o => o.order_id == orderId)
//                .ToList()
//                .Sum(o => o.unit_price * o.quantity);
//            return Convert.ToInt32(total);
//        }
//    }

//    // ─────────────────────────────────────────────
//    // DTOs
//    // ─────────────────────────────────────────────
//    public class OrderFromPrescriptionDto
//    {
//        public int custId { get; set; }
//        public int storeId { get; set; }
//        public int prescriptionId { get; set; }
//        public List<PrescriptionMedicineOrderDto> medicines { get; set; }
//    }

//    public class PrescriptionMedicineOrderDto
//    {
//        public string baseName { get; set; }
//        public int quantity { get; set; }
//    }

//    public class UpdateOrderStatusDto
//    {
//        public int orderId { get; set; }
//        public string status { get; set; }
//    }

//    public class ConfirmOrderDto
//    {
//        public int orderId { get; set; }
//    }

//    public class RejectOrderDto
//    {
//        public int orderId { get; set; }
//        public string reason { get; set; }
//    }
//}



using FYPBackend.DTOs.Order;
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
        // 1. PLACE ORDER FROM PRESCRIPTION
        // POST api/Order/PlaceFromPrescriptioaan
        // Customer places order after contraindication check
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("PlaceFromPrescription")]
        public IHttpActionResult PlaceFromPrescription([FromBody] PlaceFromPrescriptionDto dto)
        {
            if (dto == null || dto.medicines == null || dto.medicines.Count == 0)
                return BadRequest("Invalid data. medicines list required.");

            try
            {
                var prescription = _db.prescriptions.FirstOrDefault(p => p.id == dto.prescriptionId);
                if (prescription == null)
                    return BadRequest("Prescription not found");

                var store = _db.medicalstores.FirstOrDefault(s => s.store_id == dto.storeId);
                if (store == null)
                    return BadRequest("Store not found");

                var customer = _db.customers.FirstOrDefault(c => c.c_id == dto.custId);
                if (customer == null)
                    return BadRequest("Customer not found");

                var newOrder = new order
                {
                    cust_id = dto.custId,
                    store_id = dto.storeId,
                    presp_id = dto.prescriptionId,
                    order_date = DateTime.Now,
                    status = "pending",
                    location = dto.deliveryAddress
                };

                _db.orders.Add(newOrder);
                _db.SaveChanges();

                var warnings = new List<string>();
                decimal totalBill = 0;

                foreach (var med in dto.medicines)
                {
                    var medicine = _db.medicines.FirstOrDefault(m =>
                        m.store_id == dto.storeId &&
                        m.base_name == med.baseName);

                    if (medicine == null)
                    {
                        warnings.Add($"'{med.baseName}' not found in store — skipped");
                        continue;
                    }

                    int totalPills = med.quantity;
                    int availableStock = _db.medicine_batches
                        .Where(b => b.med_id == medicine.med_id && b.remaining_pills > 0)
                        .Sum(b => (int?)b.remaining_pills) ?? 0;

                    if (availableStock < totalPills)
                    {
                        warnings.Add($"'{med.baseName}' has only {availableStock} pills, requested {totalPills}");
                        continue;
                    }

                    decimal unitPrice = 0;
                    if (medicine.price.HasValue && medicine.pills_per_pack.HasValue && medicine.pills_per_pack.Value > 0)
                        unitPrice = (decimal)medicine.price.Value / medicine.pills_per_pack.Value;

                    decimal itemTotal = unitPrice * totalPills;
                    totalBill += itemTotal;

                    _db.order_items.Add(new order_items
                    {
                        order_id = newOrder.order_id,
                        medicine_id = medicine.med_id,
                        quantity = totalPills,
                        unit_price = unitPrice,
                        med_name = medicine.name,
                        created_at = DateTime.Now
                    });
                }

                newOrder.total_bill = (int)Math.Round(totalBill);
                _db.SaveChanges();

                return Ok(new
                {
                    message = "Order placed successfully",
                    orderId = newOrder.order_id,
                    status = newOrder.status,
                    totalBill = newOrder.total_bill,
                    warnings = warnings
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // 2. PLACE DIRECT ORDER (Buy Again / Cart)
        // POST api/Order/PlaceDirect
        // Customer buys specific medicines directly
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("PlaceDirect")]
        public IHttpActionResult PlaceDirect([FromBody] PlaceDirectOrderDto dto)
        {
            if (dto == null || dto.medicines == null || dto.medicines.Count == 0)
                return BadRequest("Invalid data. medicines list required.");

            try
            {
                var customer = _db.customers.FirstOrDefault(c => c.c_id == dto.custId);
                if (customer == null)
                    return BadRequest("Customer not found");

                var store = _db.medicalstores.FirstOrDefault(s => s.store_id == dto.storeId);
                if (store == null)
                    return BadRequest("Store not found");

                var newOrder = new order
                {
                    cust_id = dto.custId,
                    store_id = dto.storeId,
                    order_date = DateTime.Now,
                    status = "pending",
                    location = dto.deliveryAddress
                };

                _db.orders.Add(newOrder);
                _db.SaveChanges();

                decimal totalBill = 0;
                var warnings = new List<string>();

                foreach (var item in dto.medicines)
                {
                    var medicine = _db.medicines.FirstOrDefault(m =>
                        m.med_id == item.medId && m.store_id == dto.storeId);

                    if (medicine == null)
                    {
                        warnings.Add($"Medicine ID {item.medId} not found");
                        continue;
                    }

                    decimal unitPrice = 0;
                    if (medicine.price.HasValue && medicine.pills_per_pack.HasValue && medicine.pills_per_pack.Value > 0)
                        unitPrice = (decimal)medicine.price.Value / medicine.pills_per_pack.Value;

                    totalBill += unitPrice * item.quantity;

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

                newOrder.total_bill = (int)Math.Round(totalBill);
                _db.SaveChanges();

                return Ok(new
                {
                    message = "Order placed successfully",
                    orderId = newOrder.order_id,
                    status = newOrder.status,
                    totalBill = newOrder.total_bill,
                    warnings = warnings
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // 3. GET ORDERS FOR CUSTOMER
        // GET api/Order/GetByCustomer?custId=11
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetByCustomer")]
        public IHttpActionResult GetByCustomer(int custId)
        {
            var orders = _db.orders
                .Where(o => o.cust_id == custId)
                .OrderByDescending(o => o.order_date)
                .ToList()
                .Select(o => new
                {
                    o.order_id,
                    o.status,
                    o.total_bill,
                    // Replace all instances of:
                    // orderDate = o.order_date.ToString("yyyy-MM-dd HH:mm"),
                    // with:

                    // And in the Detail method:
                    // orderDate = o.order_date.ToString("yyyy-MM-dd HH:mm"),
                    // with:
                    orderDate = o.order_date.HasValue ? o.order_date.Value.ToString("yyyy-MM-dd HH:mm") : "",
                    o.location,
                    storeName = _db.medicalstores
                        .Where(s => s.store_id == o.store_id)
                        .Select(s => s.name)
                        .FirstOrDefault(),
                    itemCount = _db.order_items.Count(i => i.order_id == o.order_id)
                })
                .ToList();

            return Ok(orders);
        }

        // ─────────────────────────────────────────────
        // 4. GET ORDER DETAIL (with rider location)
        // GET api/Order/Detail?orderId=5
        // Used by customer for tracking screen
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("Detail")]
        public IHttpActionResult Detail(int orderId)
        {
            var o = _db.orders.FirstOrDefault(x => x.order_id == orderId);
            if (o == null)
                return NotFound();

            var items = _db.order_items
                .Where(i => i.order_id == orderId)
                .Select(i => new
                {
                    i.med_name,
                    i.quantity,
                    i.unit_price,
                    subtotal = i.unit_price * i.quantity
                })
                .ToList();

            object riderInfo = null;
            if (o.rider_id.HasValue)
            {
                var rider = _db.Riders.FirstOrDefault(r => r.rider_id == o.rider_id.Value);
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
                o.order_id,
                o.status,
                o.total_bill,
                orderDate = o.order_date.HasValue ? o.order_date.Value.ToString("yyyy-MM-dd HH:mm") : "",
                o.location,
                storeName = _db.medicalstores
                    .Where(s => s.store_id == o.store_id)
                    .Select(s => s.name)
                    .FirstOrDefault(),
                items,
                rider = riderInfo
            });
        }

        // ─────────────────────────────────────────────
        // 5. GET ORDERS FOR STORE
        // GET api/Order/GetByStore?storeId=1
        // Store dashboard — all incoming orders
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetByStore")]
        public IHttpActionResult GetByStore(int storeId)
        {
            var orders = _db.orders
                .Where(o => o.store_id == storeId)
                .OrderByDescending(o => o.order_date)
                .ToList()
                .Select(o => new
                {
                    o.order_id,
                    o.cust_id,
                    customerName = _db.customers
                        .Where(c => c.c_id == o.cust_id)
                        .Select(c => c.name)
                        .FirstOrDefault(),
                    o.status,
                    o.total_bill,
                    orderDate = o.order_date.HasValue ? o.order_date.Value.ToString("yyyy-MM-dd HH:mm") : "",
                    o.location,
                    o.rider_id,
                    items = _db.order_items
                        .Where(i => i.order_id == o.order_id)
                        .Select(i => new
                        {
                            i.med_name,
                            i.quantity,
                            i.unit_price
                        }).ToList()
                })
                .ToList();

            return Ok(orders);
        }

        // ─────────────────────────────────────────────
        // 6. UPDATE ORDER STATUS
        // PUT api/Order/UpdateStatus
        // Used by store (confirm/ready/dispatched)
        // and by rider (delivered)
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

            var allowed = new[] { "pending", "confirmed", "ready", "dispatched", "delivered", "cancelled" };
            if (!allowed.Contains(dto.status))
                return BadRequest("Invalid status. Allowed: pending, confirmed, ready, dispatched, delivered, cancelled");

            order.status = dto.status;
            _db.SaveChanges();

            return Ok(new
            {
                message = $"Order status updated to '{dto.status}'",
                orderId = order.order_id,
                status = order.status
            });
        }

        // ─────────────────────────────────────────────
        // 7. ASSIGN RIDER TO ORDER
        // PUT api/Order/AssignRider
        // Store assigns an available rider
        // ─────────────────────────────────────────────
        [HttpPut]
        [Route("AssignRider")]
        public IHttpActionResult AssignRider([FromBody] AssignRiderDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            var order = _db.orders.FirstOrDefault(o => o.order_id == dto.orderId);
            if (order == null)
                return NotFound();

            var rider = _db.Riders.FirstOrDefault(r => r.rider_id == dto.riderId);
            if (rider == null)
                return BadRequest("Rider not found");

            if (rider.status == "busy")
                return BadRequest("Rider is already busy");

            order.rider_id = dto.riderId;
            order.status = "dispatched";

            rider.status = "busy";

            _db.SaveChanges();

            return Ok(new
            {
                message = "Rider assigned successfully",
                orderId = order.order_id,
                riderId = rider.rider_id,
                riderName = rider.name,
                orderStatus = order.status
            });
        }

        // ─────────────────────────────────────────────
        // 8. GET CURRENT ORDER FOR RIDER
        // GET api/Order/GetCurrentForRider?riderId=3
        // Rider home screen — active delivery
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetCurrentForRider")]
        public IHttpActionResult GetCurrentForRider(int riderId)
        {
            var order = _db.orders
                .Where(o => o.rider_id == riderId && o.status == "dispatched")
                .OrderByDescending(o => o.order_date)
                .FirstOrDefault();

            if (order == null)
                return Ok(new { message = "No active delivery", order = (object)null });

            var items = _db.order_items
                .Where(i => i.order_id == order.order_id)
                .Select(i => new { i.med_name, i.quantity })
                .ToList();

            var customer = _db.customers.FirstOrDefault(c => c.c_id == order.cust_id);

            return Ok(new
            {
                order = new
                {
                    order.order_id,
                    order.status,
                    order.location,
                    order.total_bill,
                    customerName = customer != null ? customer.name : "",
                    customerContact = customer != null ? customer.contact : "",
                    items
                }
            });
        }

        // ─────────────────────────────────────────────
        // 9. GET ORDER HISTORY FOR RIDER
        // GET api/Order/GetByRider?riderId=3
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetByRider")]
        public IHttpActionResult GetByRider(int riderId)
        {
            var orders = _db.orders
                .Where(o => o.rider_id == riderId && (o.status == "delivered" || o.status == "rated"))
                .OrderByDescending(o => o.order_date)
                .ToList()
                .Select(o => new
                {
                    o.order_id,
                    o.status,
                    o.total_bill,
                    orderDate = o.order_date.HasValue ? o.order_date.Value.ToString("yyyy-MM-dd HH:mm") : "",
                    o.location,
                    customerName = _db.customers
                        .Where(c => c.c_id == o.cust_id)
                        .Select(c => c.name)
                        .FirstOrDefault()
                })
                .ToList();

            return Ok(orders);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
