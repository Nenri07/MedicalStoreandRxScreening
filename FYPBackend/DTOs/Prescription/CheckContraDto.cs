using System.Collections.Generic;

namespace FYPBackend.DTOs.Prescription
{
    public class CheckContraDto
    {
        public int profile_id { get; set; }
        public List<MedicineDto> medicines { get; set; }
    }
}