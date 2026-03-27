using FYPBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace FYPBackend.Controllers
{
    [RoutePrefix("api/Subscription")]
    public class SubscriptionController : ApiController
    {
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        // ─────────────────────────────────────────────
        // 1. CREATE SUBSCRIPTION
        // POST api/Subscription/Create
        // Customer sets up monthly medicine plan
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("Create")]
        public IHttpActionResult Create([FromBody] CreateSubscriptionDto dto)
        {
            if (dto == null || dto.medicines == null || dto.medicines.Count == 0)
                return BadRequest("Invalid data. medicines list required.");

            if (dto.dayOfMonth < 1 || dto.dayOfMonth > 28)
                return BadRequest("dayOfMonth must be between 1 and 28.");

            try
            {
                var customer = _db.customers.FirstOrDefault(c => c.c_id == dto.custId);
                if (customer == null)
                    return BadRequest("Customer not found");

                var store = _db.medicalstores.FirstOrDefault(s => s.store_id == dto.storeId);
                if (store == null)
                    return BadRequest("Store not found");

                // calculate next order date
                // if today's day has already passed this month, go to next month
                DateTime today = DateTime.Today;
                DateTime nextOrderDate;

                if (today.Day <= dto.dayOfMonth)
                    nextOrderDate = new DateTime(today.Year, today.Month, dto.dayOfMonth);
                else
                    nextOrderDate = new DateTime(today.Year, today.Month, dto.dayOfMonth).AddMonths(1);

                // create subscription row
                var sub = new subscriptions
                {
                    cust_id = dto.custId,
                    store_id = dto.storeId,
                    day_of_month = dto.dayOfMonth,
                    next_order_date = nextOrderDate,
                    status = "active",
                    created_at = DateTime.Now
                };

                _db.subscriptions.Add(sub);
                _db.SaveChanges();

                // add medicines to subscription
                foreach (var med in dto.medicines)
                {
                    _db.subscription_medicines.Add(new subscription_medicines
                    {
                        subscription_id = sub.id,
                        base_name = med.baseName,
                        quantity = med.quantity
                    });
                }

                _db.SaveChanges();

                return Ok(new
                {
                    message = "Subscription created successfully",
                    subscriptionId = sub.id,
                    nextOrderDate = sub.next_order_date.ToString("yyyy-MM-dd"),
                    status = sub.status,
                    medicineCount = dto.medicines.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // 2. GET SUBSCRIPTIONS FOR A CUSTOMER
        // GET api/Subscription/GetByCustomer?custId=11
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetByCustomer")]
        public IHttpActionResult GetByCustomer(int custId)
        {
            var subs = _db.subscriptions
                .Where(s => s.cust_id == custId)
                .ToList()
                .Select(s => new
                {
                    s.id,
                    s.cust_id,
                    s.store_id,
                    storeName = s.medicalstore != null ? s.medicalstore.name : "",
                    s.day_of_month,
                    nextOrderDate = s.next_order_date.ToString("yyyy-MM-dd"),
                    s.status,
                    s.created_at,
                    medicines = s.subscription_medicines.Select(m => new
                    {
                        m.base_name,
                        m.quantity
                    })
                })
                .ToList();

            return Ok(subs);
        }

        // ─────────────────────────────────────────────
        // 3. GET SINGLE SUBSCRIPTION DETAIL
        // GET api/Subscription/Detail?subId=1
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("Detail")]
        public IHttpActionResult Detail(int subId)
        {
            var s = _db.subscriptions.FirstOrDefault(x => x.id == subId);
            if (s == null)
                return NotFound();

            return Ok(new
            {
                s.id,
                s.cust_id,
                s.store_id,
                storeName = s.medicalstore != null ? s.medicalstore.name : "",
                s.day_of_month,
                nextOrderDate = s.next_order_date.ToString("yyyy-MM-dd"),
                s.status,
                s.created_at,
                medicines = s.subscription_medicines.Select(m => new
                {
                    m.base_name,
                    m.quantity
                })
            });
        }

        // ─────────────────────────────────────────────
        // 4. PAUSE OR CANCEL SUBSCRIPTION
        // PUT api/Subscription/UpdateStatus
        // ─────────────────────────────────────────────
        [HttpPut]
        [Route("UpdateStatus")]
        public IHttpActionResult UpdateStatus([FromBody] UpdateSubscriptionStatusDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            var sub = _db.subscriptions.FirstOrDefault(s => s.id == dto.subId);
            if (sub == null)
                return NotFound();

            var allowed = new[] { "active", "paused", "cancelled" };
            if (!allowed.Contains(dto.status))
                return BadRequest("Invalid status. Allowed: active, paused, cancelled");

            sub.status = dto.status;
            _db.SaveChanges();

            return Ok(new
            {
                message = $"Subscription updated to '{dto.status}'",
                subscriptionId = sub.id,
                status = sub.status
            });
        }

        // ─────────────────────────────────────────────
        // 5. PROCESS DUE SUBSCRIPTIONS
        // GET api/Subscription/ProcessDue
        // Call this every day — checks all active subs
        // where next_order_date = today and places order
        // In production this would be a scheduled job
        // For FYP call it manually or on app startup
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("ProcessDue")]
        public IHttpActionResult ProcessDue()
        {
            try
            {
                DateTime today = DateTime.Today;

                // find all active subscriptions due today or overdue
                var dueSubs = _db.subscriptions
                    .Where(s => s.status == "active"
                             && s.next_order_date <= today)
                    .ToList();

                if (!dueSubs.Any())
                    return Ok(new { message = "No subscriptions due today", processed = 0 });

                var results = new List<object>();

                foreach (var sub in dueSubs)
                {
                    var medicines = _db.subscription_medicines
                        .Where(m => m.subscription_id == sub.id)
                        .ToList();

                    if (!medicines.Any())
                        continue;

                    // get customer's latest prescription for location
                    // if none exists use a default location
                    var latestPrescription = _db.prescriptions
                        .Where(p => p.cust_id == sub.cust_id)
                        .OrderByDescending(p => p.id)
                        .FirstOrDefault();

                    string deliveryLocation = latestPrescription != null
                        ? latestPrescription.location
                        : "Default Location";

                    // create order
                    var newOrder = new order
                    {
                        cust_id = sub.cust_id,
                        store_id = sub.store_id,
                        presp_id = latestPrescription != null ? (int?)latestPrescription.id : null,
                        order_date = DateTime.Now,
                        status = "pending",
                        location = deliveryLocation
                    };

                    _db.orders.Add(newOrder);
                    _db.SaveChanges();

                    var warnings = new List<string>();

                    // add order items from subscription medicines
                    foreach (var med in medicines)
                    {
                        var medicine = _db.medicines.FirstOrDefault(m =>
                            m.store_id == sub.store_id &&
                            m.base_name == med.base_name);

                        if (medicine == null)
                        {
                            warnings.Add($"'{med.base_name}' not found in store");
                            continue;
                        }

                        decimal unitPrice = 0;
                        if (medicine.price.HasValue && medicine.pills_per_pack.HasValue && medicine.pills_per_pack.Value > 0)
                            unitPrice = (decimal)medicine.price.Value / medicine.pills_per_pack.Value;

                        _db.order_items.Add(new order_items
                        {
                            order_id = newOrder.order_id,
                            medicine_id = medicine.med_id,
                            quantity = med.quantity,
                            unit_price = unitPrice,
                            med_name = medicine.name,
                            created_at = DateTime.Now
                        });
                    }

                    _db.SaveChanges();

                    // calculate bill
                    decimal totalBill = _db.order_items
                        .Where(oi => oi.order_id == newOrder.order_id)
                        .ToList()
                        .Sum(oi => oi.unit_price * oi.quantity);

                    newOrder.total_bill = (int)Math.Round(totalBill);

                    // advance next_order_date by one month
                    sub.next_order_date = sub.next_order_date.AddMonths(1);

                    _db.SaveChanges();

                    results.Add(new
                    {
                        subscriptionId = sub.id,
                        custId = sub.cust_id,
                        orderId = newOrder.order_id,
                        totalBill = newOrder.total_bill,
                        nextOrderDate = sub.next_order_date.ToString("yyyy-MM-dd"),
                        warnings = warnings
                    });
                }

                return Ok(new
                {
                    message = $"{results.Count} subscription order(s) placed",
                    processed = results.Count,
                    orders = results
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }
        // ─────────────────────────────────────────────
        // SUBSCRIPTION DUE ALERTS FOR CUSTOMER
        // GET api/Subscription/Alerts?custId=11&daysAhead=5
        // Returns subscriptions firing in next X days
        // Customer sees reminder banner on home screen
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("Alerts")]
        public IHttpActionResult Alerts(int custId, int daysAhead = 5)
        {
            DateTime today = DateTime.Today;
            DateTime threshold = today.AddDays(daysAhead);

            var alerts = _db.subscriptions
                .Where(s => s.cust_id == custId
                         && s.status == "active"
                         && s.next_order_date >= today
                         && s.next_order_date <= threshold)
                .ToList()
                .Select(s => new
                {
                    s.id,
                    s.store_id,
                    storeName = s.medicalstore != null ? s.medicalstore.name : "",
                    s.day_of_month,
                    nextOrderDate = s.next_order_date.ToString("yyyy-MM-dd"),
                    daysUntilOrder = (s.next_order_date - today).Days,
                    s.status,
                    medicines = _db.subscription_medicines
                        .Where(m => m.subscription_id == s.id)
                        .Select(m => new { m.base_name, m.quantity })
                        .ToList()
                })
                .OrderBy(s => s.daysUntilOrder)
                .ToList();

            if (!alerts.Any())
                return Ok(new { message = "No upcoming subscriptions", alerts = alerts });

            return Ok(new
            {
                custId = custId,
                checkedOn = today.ToString("yyyy-MM-dd"),
                totalAlerts = alerts.Count,
                alerts = alerts
            });
        }
    }

    // ─────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────
    public class CreateSubscriptionDto
    {
        public int custId { get; set; }
        public int storeId { get; set; }
        public int dayOfMonth { get; set; }
        public List<SubscriptionMedicineDto> medicines { get; set; }
    }

    public class SubscriptionMedicineDto
    {
        public string baseName { get; set; }
        public int quantity { get; set; }
    }

    public class UpdateSubscriptionStatusDto
    {
        public int subId { get; set; }
        public string status { get; set; }
    }
}