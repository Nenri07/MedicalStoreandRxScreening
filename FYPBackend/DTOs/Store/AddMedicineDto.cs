using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FYPBackend.DTOs.Store
{
    public class AddMedicineDto
    {

        public void AddUser(UsersDTO   s)
        {

        }
    }


    public class UsersDTO
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Nic { get; set; }
        public string Role { get; set; }
        public bool? IsBlocked { get; set; }
        public string ProfilePic { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int PropertiesCount { get; set; } // For landlords
    }


}