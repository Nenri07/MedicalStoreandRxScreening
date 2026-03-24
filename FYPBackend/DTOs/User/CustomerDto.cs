using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FYPBackend.DTOs.User
{
    public class CustomerDto
    {

        public string Name { get; set; }
        public string Email { get; set; }



        public string  password { get; set; }
        public string Contact { get; set; }
        public DateTime Dob { get; set; }
    }
}
