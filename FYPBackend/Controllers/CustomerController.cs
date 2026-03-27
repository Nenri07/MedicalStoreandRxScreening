using FYPBackend.DTOs.User;
using FYPBackend.Models;
using System;
using System.Linq;
using System.Web.Http;

namespace FYPBackend.Controllers
{
    [RoutePrefix("api/Customer")]
    public class CustomerController : ApiController
    {
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        // ✅ Health Check
        [HttpGet]
        [Route("")]
        public IHttpActionResult HealthCheck()
        {
            return Ok("Controller is working");
        }

        // ✅ Signup Customer
        [HttpPost]
        [Route("Signup")]
        public IHttpActionResult SignupCustomer(CustomerDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.password))
                return BadRequest("Email and Password are required");

            bool emailExists = _db.users.Any(u => u.email == dto.Email);
            if (emailExists)
                return BadRequest("Email already exists");

            var user = new user
            {
                email = dto.Email,
                password = dto.password,
                Role = "user"
            };

            _db.users.Add(user);
            _db.SaveChanges();

            var customer = new customer
            {
                c_id = user.Id,
                name = dto.Name,
                email = dto.Email,
                password = dto.password,
                contact = dto.Contact,
                dob = dto.Dob
            };

            _db.customers.Add(customer);
            _db.SaveChanges();

            return Ok("Customer Registered Successfully");
        }

        // ✅ Add Profile
        [HttpPost]
        [Route("AddProfile")]
        public IHttpActionResult AddProfile(AddMemberDto member)
        {
            if (member == null)
                return BadRequest("Invalid data");

            bool customerExists = _db.customers.Any(c => c.c_id == member.cus_id);
            if (!customerExists)
                return BadRequest("Customer not found");

            try
            {
                var profile = new profile
                {
                    cus_id = member.cus_id,
                    Fullname = member.fname,
                    relation = member.relation,
                    gender = member.gender,
                    contact = member.contact,
                    age = member.age,
                    default_lat = member.lat,
                    default_long = member.lng,
                    Addres = member.address
                };

                _db.profiles.Add(profile);
                _db.SaveChanges();

                int profileId = profile.id;

                // If no PHR data
                if (member.Allergies == null && member.PastDiseases == null && member.AlreadyTakingMedicines == null)
                    return Ok("Profile added without PHR");

                // Allergies
                if (member.Allergies != null)
                {
                    foreach (var item in member.Allergies)
                    {
                        _db.phrs.Add(new phr
                        {
                            profile_id = profileId,
                            entry_name = item,
                            category = "Allergy"
                        });
                    }
                }

                // Diseases
                if (member.PastDiseases != null)
                {
                    foreach (var item in member.PastDiseases)
                    {
                        _db.phrs.Add(new phr
                        {
                            profile_id = profileId,
                            entry_name = item,
                            category = "PastDisease"
                        });
                    }
                }

                // Medicines
                if (member.AlreadyTakingMedicines != null)
                {
                    foreach (var item in member.AlreadyTakingMedicines)
                    {
                        _db.phrs.Add(new phr
                        {
                            profile_id = profileId,
                            entry_name = item,
                            category = "AlreadyTakingMedicine"
                        });
                    }
                }

                _db.SaveChanges();

                return Ok("Profile and PHR added successfully");
            }
            catch (Exception e)
            {
                return BadRequest("Error: " + e.Message);
            }
        }

        // ✅ Update PHR
        [HttpPut]
        [Route("PhrUpdate")]
        public IHttpActionResult UpdatePhr(PhrDto phrDto)
        {
            if (phrDto == null)
                return BadRequest("Invalid data");

            try
            {
                var existing = _db.phrs.Where(x => x.profile_id == phrDto.ProfileId).ToList();

                var allergies = existing.Where(x => x.category == "Allergy").ToList();
                var diseases = existing.Where(x => x.category == "PastDisease").ToList();
                var medicines = existing.Where(x => x.category == "AlreadyTakingMedicine").ToList();

                // Remove old
                foreach (var a in allergies)
                    if (!phrDto.Allergies.Contains(a.entry_name))
                        _db.phrs.Remove(a);

                foreach (var d in diseases)
                    if (!phrDto.PastDiseases.Contains(d.entry_name))
                        _db.phrs.Remove(d);

                foreach (var m in medicines)
                    if (!phrDto.AlreadyTakingMedicines.Contains(m.entry_name))
                        _db.phrs.Remove(m);

                // Add new
                foreach (var item in phrDto.Allergies)
                    if (!allergies.Any(a => a.entry_name == item))
                        _db.phrs.Add(new phr { profile_id = phrDto.ProfileId, entry_name = item, category = "Allergy" });

                foreach (var item in phrDto.PastDiseases)
                    if (!diseases.Any(d => d.entry_name == item))
                        _db.phrs.Add(new phr { profile_id = phrDto.ProfileId, entry_name = item, category = "PastDisease" });

                foreach (var item in phrDto.AlreadyTakingMedicines)
                    if (!medicines.Any(m => m.entry_name == item))
                        _db.phrs.Add(new phr { profile_id = phrDto.ProfileId, entry_name = item, category = "AlreadyTakingMedicine" });

                _db.SaveChanges();

                return Ok("Updated Successfully");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ✅ Update Member
        [HttpPut]
        [Route("MemberUpdate")]
        public IHttpActionResult UpdateMember(UpdateMemberDto member)
        {
            if (member == null || member.cus_id <= 0)
                return BadRequest("Invalid data");

            var profile = _db.profiles.FirstOrDefault(p => p.id == member.profile_id && p.cus_id == member.cus_id);

            if (profile == null)
                return NotFound();

            profile.Fullname = member.fname;
            profile.relation = member.relation;
            profile.gender = member.gender;
            profile.contact = member.contact;
            profile.age = member.age;
            profile.default_lat = member.lat;

            _db.SaveChanges();

            return Ok("Updated Successfully");
        }
        [HttpGet]
        [Route("GetProfiles")]
        public IHttpActionResult GetProfiles(int custId)
        {
            bool customerExists = _db.customers.Any(c => c.c_id == custId);
            if (!customerExists)
                return BadRequest("Customer not found");

            var profiles = _db.profiles
                .Where(p => p.cus_id == custId)
                .ToList()
                .Select(p => new
                {
                    p.id,
                    p.cus_id,
                    fullname = p.Fullname,
                    p.relation,
                    p.gender,
                    p.contact,
                    p.age,
                    p.default_lat,
                    p.default_long,
                    address = p.Addres,
                    phr = _db.phrs
                        .Where(x => x.profile_id == p.id)
                        .ToList()
                        .Select(x => new
                        {
                            x.id,
                            x.entry_name,
                            x.category
                        })
                        .ToList()
                })
                .ToList();

            return Ok(profiles);
        }

        // ✅ GET SINGLE PROFILE DETAIL
        // GET api/Customer/GetProfile?profileId=1
        // Used by: Edit member screen, PHR view screen
        [HttpGet]
        [Route("GetProfile")]
        public IHttpActionResult GetProfile(int profileId)
        {
            var p = _db.profiles.FirstOrDefault(x => x.id == profileId);
            if (p == null)
                return NotFound();

            var phrData = _db.phrs.Where(x => x.profile_id == profileId).ToList();

            return Ok(new
            {
                p.id,
                p.cus_id,
                fullname = p.Fullname,
                p.relation,
                p.gender,
                p.contact,
                p.age,
                p.default_lat,
                p.default_long,
                address = p.Addres,
                allergies = phrData
                    .Where(x => x.category == "Allergy")
                    .Select(x => x.entry_name)
                    .ToList(),
                pastDiseases = phrData
                    .Where(x => x.category == "PastDisease")
                    .Select(x => x.entry_name)
                    .ToList(),
                alreadyTakingMedicines = phrData
                    .Where(x => x.category == "AlreadyTakingMedicine")
                    .Select(x => x.entry_name)
                    .ToList()
            });
        }

        // ✅ DELETE PROFILE
        // DELETE api/Customer/DeleteProfile?profileId=1
        // Used by: Edit member screen (delete button)
        [HttpDelete]
        [Route("DeleteProfile")]
        public IHttpActionResult DeleteProfile(int profileId)
        {
            var profile = _db.profiles.FirstOrDefault(p => p.id == profileId);
            if (profile == null)
                return NotFound();

            // Remove PHR entries first (FK constraint)
            var phrs = _db.phrs.Where(x => x.profile_id == profileId).ToList();
            foreach (var phr in phrs)
                _db.phrs.Remove(phr);

            _db.profiles.Remove(profile);
            _db.SaveChanges();

            return Ok("Profile deleted successfully");
        }

    }
}