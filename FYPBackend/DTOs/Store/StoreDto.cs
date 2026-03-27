using System.Collections.Generic;

namespace FYPBackend.DTOs.Store
{
    public class NearbyStoresRequestDto
    {
        public double userLat { get; set; }
        public double userLng { get; set; }
        public double radiusKm { get; set; } = 20;
        public List<string> medicineBaseNames { get; set; }
    }

    public class StoreWithStockDto
    {
        public int storeId { get; set; }
        public string name { get; set; }
        public string location { get; set; }
        public double distanceKm { get; set; }
        public bool isSpecialOrder { get; set; }  // true = outside 20km
        public List<MedicineStockDto> medicines { get; set; }
    }

    public class MedicineStockDto
    {
        public int medId { get; set; }
        public string name { get; set; }
        public string baseName { get; set; }
        public int remainingPills { get; set; }
        public int? pricePerPack { get; set; }
        public int? pillsPerPack { get; set; }
    }
    public class UpdateStoreProfileDto
    {
        public int storeId { get; set; }
        public string name { get; set; }
        public string location { get; set; }
        public string images { get; set; }
        public double? latitude { get; set; }
        public double? longitude { get; set; }
    }

    // Used by: POST api/Store/AddRider
    public class AddRiderDto
    {
        public int storeId { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string password { get; set; }
        public string contact { get; set; }
    }
}