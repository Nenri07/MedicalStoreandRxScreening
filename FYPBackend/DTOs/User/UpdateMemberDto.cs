using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FYPBackend.DTOs.User
{
    public class UpdateMemberDto
    {
        public int cus_id { get; set; }
        public int profile_id { get; set; }
        public string fname { get; set; }
        public string relation { get; set; }
        public string gender { get; set; }
        public string contact { get; set; }
        public int age { get; set; }
        public decimal lat { get; set; }
        public decimal lng { get; set; }
        public string address { get; set; }
    }

}



