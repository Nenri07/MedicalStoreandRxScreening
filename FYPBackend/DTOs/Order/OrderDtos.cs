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


    public class PlaceFromPrescriptionDto
    {
        public int custId { get; set; }
        public int storeId { get; set; }
        public int prescriptionId { get; set; }
        public string deliveryAddress { get; set; }
        public List<PrescriptionOrderMedicineDto> medicines { get; set; }
    }
    public class PrescriptionOrderMedicineDto
    {
        public string baseName { get; set; }
        public int quantity { get; set; }  // total pills needed
    }

    // Used by: POST api/Order/PlaceDirect
    // Customer buys medicines directly (Buy Again / Cart)
    public class PlaceDirectOrderDto
    {
        public int custId { get; set; }
        public int storeId { get; set; }
        public string deliveryAddress { get; set; }
        public List<DirectOrderItemDto> medicines { get; set; }
    }

    public class DirectOrderItemDto
    {
        public int medId { get; set; }
        public int quantity { get; set; }  // number of pills
    }

    // Used by: PUT api/Order/AssignRider
    // Store assigns a rider to an order
    public class AssignRiderDto
    {
        public int orderId { get; set; }
        public int riderId { get; set; }
    }


    public class UpdateOrderStatusDto
    {
        public int orderId { get; set; }
        public string status { get; set; }
    }
 
}