using System.Collections.Generic;

namespace FYPBackend.DTOs.Order
{
    public class PlaceOrderDto
    {
        public int custId { get; set; }
        public int storeId { get; set; }
        public int prespId { get; set; }
        public int medId { get; set; }
        public int quantity { get; set; }
    }


    public class PrescriptionMedicineOrderDto
    {
        public string baseName { get; set; }
        public int quantity { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        public int orderId { get; set; }
        public string status { get; set; }
    }
}