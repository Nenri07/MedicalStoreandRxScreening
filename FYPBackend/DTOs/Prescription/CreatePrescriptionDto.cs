namespace FYPBackend.DTOs.Prescription
{
    public class CreatePrescriptionDto
    {
        public int profile_id { get; set; }
        public string address { get; set; }
        public string rx_image { get; set; }
    }
}