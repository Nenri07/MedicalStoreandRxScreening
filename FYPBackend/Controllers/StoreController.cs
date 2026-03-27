using FYPBackend.DTOs.Store;
using FYPBackend.Models;
using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace FYPBackend.Controllers
{
    [RoutePrefix("api/Store")]
    public class StoreController : ApiController
    {
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        // ─────────────────────────────────────────────
        // 1. STORE SIGNUP
        // POST api/Store/StoreSignup
        // 
        [HttpPost]
        [Route("StoreSignup")]
        public IHttpActionResult StoreSignup()
        {
            try
            {
                var request = HttpContext.Current.Request;

                string email = request["email"];
                string password = request["password"];
                string name = request["name"];
                string location = request["location"];

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                    return BadRequest("Email and Password are required");

                bool emailExists = _db.users.Any(u => u.email == email);
                if (emailExists)
                    return BadRequest("Email already exists");

                // ✅ Create user
                var newUser = new user
                {
                    email = email,
                    password = password,
                    Role = "Store"
                };

                _db.users.Add(newUser);
                _db.SaveChanges();

                string imagePath = "";

                // ✅ Handle Image Upload
                if (request.Files.Count > 0)
                {
                    var file = request.Files[0];

                    // Clean store name for folder
                    string safeStoreName = name.Replace(" ", "_");

                    // Root folder
                    string rootFolder = HttpContext.Current.Server.MapPath("~/Uploads/Stores/");
                    if (!Directory.Exists(rootFolder))
                        Directory.CreateDirectory(rootFolder);

                    // Store-specific folder
                    string storeFolder = Path.Combine(rootFolder, safeStoreName);
                    if (!Directory.Exists(storeFolder))
                        Directory.CreateDirectory(storeFolder);

                    // Unique file name
                    string fileName = "STORE_" + DateTime.Now.Ticks + Path.GetExtension(file.FileName);
                    string fullPath = Path.Combine(storeFolder, fileName);

                    file.SaveAs(fullPath);

                    // Save URL
                    imagePath = "/Uploads/Stores/" + safeStoreName + "/" + fileName;
                }

                // ✅ Create store
                var newStore = new medicalstore
                {
                    store_id = newUser.Id,
                    name = name,
                    email = email,
                    location = location,
                    password = password,
                    images = imagePath
                };

                _db.medicalstores.Add(newStore);
                _db.SaveChanges();

                return Ok(new
                {
                    message = "Medical store registered successfully",
                    storeId = newStore.store_id,
                    imageUrl = imagePath
                });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errors = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.ErrorMessage);

                string errorMessage = string.Join("; ", errors);

                return BadRequest(errorMessage);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // 2. ADD MEDICINE
        // POST api/Store/AddMedicine
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("AddMedicine")]
        public IHttpActionResult AddMedicine([FromBody] AddMedicineDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            if (dto.storeId <= 0)
                return BadRequest("Invalid Store ID");

            if (string.IsNullOrWhiteSpace(dto.name) ||
                string.IsNullOrWhiteSpace(dto.baseName) ||
                string.IsNullOrWhiteSpace(dto.category) ||
                string.IsNullOrWhiteSpace(dto.strength))
                return BadRequest("Name, BaseName, Category and Strength are required");

            var store = _db.medicalstores.FirstOrDefault(s => s.store_id == dto.storeId);
            if (store == null)
                return NotFound();

            bool exists = _db.medicines.Any(m =>
                m.name == dto.name &&
                m.store_id == dto.storeId);

            if (exists)
                return BadRequest("Medicine with same name already exists in this store");

            var medicine = new medicine
            {
                store_id = dto.storeId,
                name = dto.name,
                base_name = dto.baseName,
                price = dto.price,
                category = dto.category,
                strength = dto.strength,
                pills_per_pack = dto.pillsPerPack
            };

            _db.medicines.Add(medicine);
            _db.SaveChanges();

            if (dto.quantity <= 0)
                return BadRequest("Quantity must be greater than zero");

            int totalPills = dto.quantity * dto.pillsPerPack;

            var batch = new medicine_batches
            {
                med_id = medicine.med_id,
                batch_number = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                total_pills = totalPills,
                remaining_pills = totalPills,
                expiry_date = dto.expiryDate,
                purchase_price_per_pack = dto.price
            };

            _db.medicine_batches.Add(batch);
            _db.SaveChanges();

            return Ok(new
            {
                message = "Medicine added successfully",
                medId = medicine.med_id,
                batchId = batch.batch_id
            });
        }

        // ─────────────────────────────────────────────
        // 3. UPDATE STOCK (add new batch)
        // PUT api/Store/UpdateStock
        // ─────────────────────────────────────────────
        [HttpPut]
        [Route("UpdateStock")]
        public IHttpActionResult UpdateStock([FromBody] AddBatchDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            if (dto.medId <= 0)
                return BadRequest("Invalid Medicine ID");

            var medicine = _db.medicines.FirstOrDefault(m => m.med_id == dto.medId);
            if (medicine == null)
                return NotFound();

            if (dto.quantity <= 0)
                return BadRequest("Quantity must be greater than zero");

            int totalPills = dto.quantity * (medicine.pills_per_pack ?? 0);

            var batch = new medicine_batches
            {
                med_id = dto.medId,
                batch_number = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                total_pills = totalPills,
                remaining_pills = totalPills,
                expiry_date = dto.expiryDate,
                purchase_price_per_pack = dto.price
            };

            _db.medicine_batches.Add(batch);
            _db.SaveChanges();

            return Ok(new
            {
                message = "Stock updated successfully",
                batchId = batch.batch_id,
                totalPills = totalPills
            });
        }

        // ─────────────────────────────────────────────
        // 4. GET STORE MEDICINES WITH STOCK
        // GET api/Store/GetMedicines?storeId=1
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("GetMedicines")]
        public IHttpActionResult GetMedicines(int storeId)
        {
            var medicines = _db.medicines
                .Where(m => m.store_id == storeId)
                .ToList()
                .Select(m => new
                {
                    m.med_id,
                    m.name,
                    m.base_name,
                    m.price,
                    m.category,
                    m.strength,
                    m.pills_per_pack,
                    totalStock = _db.medicine_batches
                        .Where(b => b.med_id == m.med_id && b.remaining_pills > 0)
                        .Sum(b => (int?)b.remaining_pills) ?? 0
                })
                .ToList();

            return Ok(medicines);
        }
        [HttpGet]
        [Route("GetProfile")]
        public IHttpActionResult GetProfile(int storeId)
        {
            var store = _db.medicalstores.FirstOrDefault(s => s.store_id == storeId);
            if (store == null)
                return NotFound();

            return Ok(new
            {
                store.store_id,
                store.name,
                store.email,
                store.location,
                store.images,
                store.latitude,
                store.longitude
            });
        }

        // ✅ UPDATE STORE PROFILE
        // PUT api/Store/UpdateProfile
        // Used by: Store edit profile screen
        
        [HttpPut]
        [Route("UpdateProfile")]
        public IHttpActionResult UpdateProfile()
        {
            try
            {
                var request = HttpContext.Current.Request;

                int storeId = Convert.ToInt32(request["storeId"]);
                string name = request["name"];
                string location = request["location"];
                string latitudeStr = request["latitude"];
                string longitudeStr = request["longitude"];

                var store = _db.medicalstores.FirstOrDefault(s => s.store_id == storeId);
                if (store == null)
                    return NotFound();

                // ✅ Update basic fields
                store.name = name;
                store.location = location;

                if (!string.IsNullOrEmpty(latitudeStr))
                    store.latitude = Convert.ToDecimal(latitudeStr);

                if (!string.IsNullOrEmpty(longitudeStr))
                    store.longitude = Convert.ToDecimal(longitudeStr);

                // ✅ Handle Image Upload
                if (request.Files.Count > 0)
                {
                    var file = request.Files[0];

                    string safeStoreName = name.Replace(" ", "_");

                    string rootFolder = HttpContext.Current.Server.MapPath("~/Uploads/Stores/");
                    if (!Directory.Exists(rootFolder))
                        Directory.CreateDirectory(rootFolder);

                    string storeFolder = Path.Combine(rootFolder, safeStoreName);
                    if (!Directory.Exists(storeFolder))
                        Directory.CreateDirectory(storeFolder);

                    string fileName = "STORE_" + DateTime.Now.Ticks + Path.GetExtension(file.FileName);
                    string fullPath = Path.Combine(storeFolder, fileName);

                    file.SaveAs(fullPath);

                    // ✅ Update image path
                    store.images = "/Uploads/Stores/" + safeStoreName + "/" + fileName;
                }

                _db.SaveChanges();

                return Ok(new
                {
                    message = "Store profile updated successfully",
                    imageUrl = store.images
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }
        // ✅ ADD RIDER TO STORE
        // POST api/Store/AddRider
        // Used by: Store rider management screen
        [HttpPost]
        [Route("AddRider")]
        public IHttpActionResult AddRider([FromBody] AddRiderDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            if (string.IsNullOrWhiteSpace(dto.email) || string.IsNullOrWhiteSpace(dto.password))
                return BadRequest("Email and Password are required");

            bool emailExists = _db.users.Any(u => u.email == dto.email);
            if (emailExists)
                return BadRequest("Email already exists");

            var store = _db.medicalstores.FirstOrDefault(s => s.store_id == dto.storeId);
            if (store == null)
                return BadRequest("Store not found");

            // Create user row first
            var newUser = new user
            {
                email = dto.email,
                password = dto.password,
                Role = "Rider"
            };

            _db.users.Add(newUser);
            _db.SaveChanges();

            // Create rider row
            var newRider = new Rider
            {
                rider_id = newUser.Id,
                name = dto.name,
                email = dto.email,
                password = dto.password,
                contact = dto.contact,
                med_id = dto.storeId,
                status = "offline",
                rating = 0,
                total_orders = 0
            };

            _db.Riders.Add(newRider);
            _db.SaveChanges();

            return Ok(new
            {
                message = "Rider added successfully",
                riderId = newRider.rider_id,
                name = newRider.name
            });
        }

        // ✅ DELETE MEDICINE
        // DELETE api/Store/DeleteMedicine?medId=1
        // Used by: Store medicine list screen
        [HttpDelete]
        [Route("DeleteMedicine")]
        public IHttpActionResult DeleteMedicine(int medId)
        {
            var medicine = _db.medicines.FirstOrDefault(m => m.med_id == medId);
            if (medicine == null)
                return NotFound();

            // Remove batches first (FK constraint)
            var batches = _db.medicine_batches.Where(b => b.med_id == medId).ToList();
            foreach (var batch in batches)
                _db.medicine_batches.Remove(batch);

            _db.medicines.Remove(medicine);
            _db.SaveChanges();

            return Ok("Medicine deleted successfully");
        }
        // ─────────────────────────────────────────────
        // GET EXPIRY ALERTS FOR A STORE
        // GET api/Store/ExpiryAlerts?storeId=1&daysAhead=30
        // Returns medicines expiring within next X days
        // Store sees this on their dashboard
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("ExpiryAlerts")]
        public IHttpActionResult ExpiryAlerts(int storeId, int daysAhead = 30)
        {
            var store = _db.medicalstores.FirstOrDefault(s => s.store_id == storeId);
            if (store == null)
                return NotFound();

            // today and threshold date as strings yyyy-MM-dd for string comparison
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            string threshold = DateTime.Today.AddDays(daysAhead).ToString("yyyy-MM-dd");

            // get all batches for this store that expire within the window
            var alerts = _db.medicine_batches
                .Where(b => b.remaining_pills > 0
                         && string.Compare(b.expiry_date, today) >= 0
                         && string.Compare(b.expiry_date, threshold) <= 0)
                .ToList()
                .Select(b =>
                {
                    var med = _db.medicines.FirstOrDefault(m =>
                        m.med_id == b.med_id &&
                        m.store_id == storeId);

                    if (med == null) return null;

                    // days remaining calculation
                    DateTime expiry;
                    bool parsed = DateTime.TryParse(b.expiry_date, out expiry);
                    int daysLeft = parsed ? (expiry - DateTime.Today).Days : 0;

                    // urgency level
                    string urgency = daysLeft <= 7 ? "critical" :
                                     daysLeft <= 15 ? "high" :
                                     daysLeft <= 30 ? "medium" : "low";

                    return new
                    {
                        b.batch_id,
                        b.batch_number,
                        b.expiry_date,
                        daysLeft,
                        urgency,
                        remainingPills = b.remaining_pills,
                        medId = med.med_id,
                        medicineName = med.name,
                        baseName = med.base_name
                    };
                })
                .Where(x => x != null)
                .OrderBy(x => x.daysLeft)
                .ToList();

            if (!alerts.Any())
                return Ok(new { message = "No expiry alerts", alerts = alerts });

            return Ok(new
            {
                storeId = storeId,
                checkedOn = today,
                totalAlerts = alerts.Count,
                critical = alerts.Count(x => x.urgency == "critical"),
                high = alerts.Count(x => x.urgency == "high"),
                medium = alerts.Count(x => x.urgency == "medium"),
                alerts = alerts
            });
        }

        // ─────────────────────────────────────────────
        // LOW STOCK ALERT
        // GET api/Store/LowStock?storeId=1&threshold=20
        // Returns medicines with remaining pills below threshold
        // ─────────────────────────────────────────────
        [HttpGet]
        [Route("LowStock")]
        public IHttpActionResult LowStock(int storeId, int threshold = 20)
        {
            var medicines = _db.medicines
                .Where(m => m.store_id == storeId)
                .ToList()
                .Select(m =>
                {
                    int totalStock = _db.medicine_batches
                        .Where(b => b.med_id == m.med_id && b.remaining_pills > 0)
                        .Sum(b => (int?)b.remaining_pills) ?? 0;

                    return new
                    {
                        m.med_id,
                        m.name,
                        m.base_name,
                        m.category,
                        m.pills_per_pack,
                        totalStock,
                        isCritical = totalStock == 0,
                        isLow = totalStock > 0 && totalStock <= threshold
                    };
                })
                .Where(x => x.totalStock <= threshold)
                .OrderBy(x => x.totalStock)
                .ToList();

            return Ok(new
            {
                storeId = storeId,
                threshold = threshold,
                totalAlerts = medicines.Count,
                outOfStock = medicines.Count(x => x.isCritical),
                lowStock = medicines.Count(x => x.isLow),
                medicines = medicines
            });
        }
    }

    // ─────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────
    public class StoreSignupDto
    {
        public string name { get; set; }
        public string email { get; set; }
        public string location { get; set; }
        public string images { get; set; }
        public string password { get; set; }
    }

    public class AddMedicineDto
    {
        public int storeId { get; set; }
        public string name { get; set; }
        public string baseName { get; set; }
        public int price { get; set; }
        public int pillsPerPack { get; set; }
        public string category { get; set; }
        public string strength { get; set; }
        public string expiryDate { get; set; }
        public int quantity { get; set; }
    }

    public class AddBatchDto
    {
        public int medId { get; set; }
        public int price { get; set; }
        public string expiryDate { get; set; }
        public int quantity { get; set; }
    }
    public class UpdateStoreProfileDto
    {
        public int storeId { get; set; }
        public string name { get; set; }
        public string location { get; set; }
        public string images { get; set; }
        public double? latitude { get; set; }
        public double? longitude { get; set; }
    }

    public class AddRiderDto
    {
        public int storeId { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string password { get; set; }
        public string contact { get; set; }
    }
}