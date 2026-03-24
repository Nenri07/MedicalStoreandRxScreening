using FYPBackend.DTOs.Prescription;
using FYPBackend.Models;
using System;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace FYPBackend.Controllers
{
    [RoutePrefix("api/Prescription")]
    public class PrescriptionController : ApiController
    {
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        // ✅ 1. CREATE PRESCRIPTION WITH IMAGE UPLOAD
       
        [HttpPost]
        [Route("Create")]
        public IHttpActionResult CreatePrescription()
        {
            try
            {
                var request = HttpContext.Current.Request;

                int profileId = Convert.ToInt32(request["profile_id"]);
                string address = request["address"];

                if (string.IsNullOrEmpty(address))
                    return BadRequest("Address required");

                var profile = _db.profiles.FirstOrDefault(p => p.id == profileId);
                if (profile == null)
                    return BadRequest("Invalid profile");

                string imagePath = "";

                if (request.Files.Count > 0)
                {
                    var file = request.Files[0];

                    string folder = HttpContext.Current.Server.MapPath("~/prescriptions/");
                    if (!System.IO.Directory.Exists(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    string fileName = "RX_" + DateTime.Now.Ticks + "_" + file.FileName;
                    string fullPath = System.IO.Path.Combine(folder, fileName);

                    file.SaveAs(fullPath);
                    imagePath = "/prescriptions/" + fileName;
                }

                var prescription = new prescription
                {
                    cust_id = profile.cus_id,   // ✅ IMPORTANT FIX
                    profileid = profileId,
                    location = address,
                    rx_image = imagePath
                };

                _db.prescriptions.Add(prescription);
                _db.SaveChanges();

                return Ok(new
                {
                    message = "Prescription Created",
                    prescription_id = prescription.id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ✅ 2. ADD MEDICINES (FIXED FK)
        [HttpPost]
        [Route("AddMedicines")]
        public IHttpActionResult AddMedicines(int prescription_id, CheckContraDto dto)
        {
            if (dto == null || dto.medicines == null)
                return BadRequest("Invalid data");

            foreach (var med in dto.medicines)
            {
                int total = med.per_day * med.days;

                _db.prescription_medicine.Add(new prescription_medicine
                {
                    prescription_id = prescription_id, // ✅ FIXED
                    medicine_name = med.medicine_name,
                    per_day = med.per_day,
                    days = med.days,
                    total_quantity = total
                });
            }

            _db.SaveChanges();

            return Ok("Medicines Added");
        }

        // ✅ 3. CONTRAINDICATION CHECK (FINAL)
        [HttpPost]
        [Route("CheckContraindication")]
        public IHttpActionResult CheckContraindication(CheckContraDto dto)
        {
            if (dto == null || dto.medicines == null)
                return BadRequest("Invalid data");

            int profileId = dto.profile_id;

            var selectedMeds = dto.medicines
                .Select(x => x.medicine_name)
                .ToList();

            // 🔹 PHR DATA
            var profileExists = _db.profiles.Any(p => p.id == profileId);
            if (!profileExists)
                return BadRequest("Profile does not exist");
            var phrData = _db.phrs.Where(x => x.profile_id == profileId).ToList();

            var diseases = phrData
                .Where(x => x.category == "PastDisease")
                .Select(x => x.entry_name)
                .ToList();

            var currentMeds = phrData
                .Where(x => x.category == "AlreadyTakingMedicine")
                .Select(x => x.entry_name)
                .ToList();

            // 🔹 DB CHECK
            var diseaseConflicts = _db.contraindications
                .Where(c => selectedMeds.Contains(c.base_name)
                         && c.disease != null
                         && diseases.Contains(c.disease))
                .ToList();

            var medConflicts = _db.contraindications
                .Where(c => selectedMeds.Contains(c.base_name)
                         && c.with_base != null
                         && currentMeds.Contains(c.with_base))
                .ToList();

            var allConflicts = diseaseConflicts.Concat(medConflicts).ToList();

            // 🔥 RESPONSE
            if (allConflicts.Any())
            {
                return Ok(new
                {
                    status = "HIGH RISK",
                    data = allConflicts.Select(c => new
                    {
                        medicine = c.base_name,
                        conflict_with = c.disease ?? c.with_base,
                        severity = c.severity,
                        message = c.message
                    })
                });
            }

            return Ok(new
            {
                status = "SAFE",
                message = "All medicines are safe"
            });
        }
    }
}