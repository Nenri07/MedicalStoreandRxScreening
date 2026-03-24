using FYPBackend.Models;
using System;
using System.Linq;
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
        // ─────────────────────────────────────────────
        [HttpPost]
        [Route("StoreSignup")]
        public IHttpActionResult StoreSignup([FromBody] StoreSignupDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid data");

            if (string.IsNullOrEmpty(dto.email) || string.IsNullOrEmpty(dto.password))
                return BadRequest("Email and Password are required");

            bool emailExists = _db.users.Any(u => u.email == dto.email);
            if (emailExists)
                return BadRequest("Email already exists");

            // create user row first
            var newUser = new user
            {
                email = dto.email,
                password = dto.password,
                Role = "Store"
            };

            _db.users.Add(newUser);
            _db.SaveChanges();

            // create medicalstore row linked to user
            var newStore = new medicalstore
            {
                store_id = newUser.Id,
                name = dto.name,
                email = dto.email,
                location = dto.location,
                password = dto.password,
                images = dto.images
            };

            _db.medicalstores.Add(newStore);
            _db.SaveChanges();

            return Ok(new
            {
                message = "Medical store registered successfully",
                storeId = newStore.store_id
            });
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
}