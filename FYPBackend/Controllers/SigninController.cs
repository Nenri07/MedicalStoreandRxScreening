using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using FYPBackend.Models; // Assuming your models are in this namespace

namespace FYPBackend.Controllers
{
    [RoutePrefix("api/Signin")]
    public class SigninController : ApiController
    {
        // Initialize your database context here. 
        // Change "YourDbContextNameEntities" to your actual EF Context name.
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        // GET: api/Signin/Login?email=abc&password=123
        [HttpGet]
        [Route("Login")]
        public IHttpActionResult Login(string email, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                    return BadRequest("Email and Password required");

                // Assuming your user table is named 'user' based on navigation properties
                var role = _db.users.FirstOrDefault(u => u.email == email && u.password == password);
                if (role == null)
                    return BadRequest("Email or password is incorrect");

                // 1. User Logic
                if (role.Role == "user") // Note: EF sometimes renames 'role' to 'role1' if it conflicts, adjust if necessary
                {
                    var res = _db.customers.FirstOrDefault(c => c.email == email && c.password == password);
                    if (res == null)
                        return Content(HttpStatusCode.NotFound, "Customer not found");

                    return Ok(new
                    {
                        id = res.c_id,
                        Name = res.name,
                        role = role.Role // Adjust property name based on your user model
                    });
                }

                // 2. Rider Logic
                if (role.Role == "Rider")
                {
                    var res = _db.Riders.FirstOrDefault(r => r.email == email && r.password == password);
                    if (res == null)
                        return Content(HttpStatusCode.NotFound, "Rider not found");

                    var store = _db.medicalstores.FirstOrDefault(s => s.store_id == res.med_id);
                    if (store == null)
                        return Content(HttpStatusCode.NotFound, "Store not found");

                    return Ok(new
                    {
                        id = res.rider_id,
                        medid = store.store_id,
                        nam = res.name,
                        role = role.Role
                    });
                }

                // 3. Store Logic
                if (role.Role == "Store")
                {
                    var res = _db.medicalstores.FirstOrDefault(s => s.email == email && s.password == password);
                    if (res == null)
                        return Content(HttpStatusCode.NotFound, "Store not found");

                    return Ok(new
                    {
                        id = res.store_id,
                        nam = res.name,
                        role = role.Role
                    });
                }

                return BadRequest("Something went wrong");
            }
            catch (Exception ex)
            {
                // In classic Web API, returning 500 with a custom message is done like this:
                return Content(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // Optional: Dispose of the DbContext to free up database connections
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}