using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FYPBackend.DTOs.User
{
    public class PhrDto
    {


        public int ProfileId { get; set; }


        public List<string> Allergies { get; set; } = new List<string>();


        public List<string> PastDiseases { get; set; } = new List<string>();


        public List<string> AlreadyTakingMedicines { get; set; } = new List<string>();
    }
}