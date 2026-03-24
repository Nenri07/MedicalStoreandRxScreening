using FYPBackend.DTOs.Store;
using FYPBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace FYPBackend.Controllers
{
    [RoutePrefix("api/Stores")]
    public class StoresController : ApiController
    {
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        // Haversine formula — returns distance in km between two GPS points
        private double GetDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371;
            double dlat = (lat2 - lat1) * Math.PI / 180;
            double dlng = (lng2 - lng1) * Math.PI / 180;

            double a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2)
                     + Math.Cos(lat1 * Math.PI / 180)
                     * Math.Cos(lat2 * Math.PI / 180)
                     * Math.Sin(dlng / 2) * Math.Sin(dlng / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        // POST api/Stores/NearbyWithMedicines
        // Send: { userLat, userLng, radiusKm, medicineBaseNames[] }
        // Returns: stores within radius that have requested medicines in stock, sorted nearest first
        [HttpPost]
        [Route("NearbyWithMedicines")]
        public IHttpActionResult NearbyWithMedicines([FromBody] NearbyStoresRequestDto dto)
        {
            if (dto == null || dto.medicineBaseNames == null || dto.medicineBaseNames.Count == 0)
                return BadRequest("User location and medicine list required");

            const double DEFAULT_RADIUS = 20.0;
            double searchRadius = dto.radiusKm > 0 ? dto.radiusKm : DEFAULT_RADIUS;

            var allStores = _db.medicalstores
                .Where(s => s.latitude != null && s.longitude != null)
                .ToList();

            var trace = new List<object>();

            foreach (var store in allStores)
            {
                double storeLat = (double)store.latitude;
                double storeLng = (double)store.longitude;
                double distance = GetDistanceKm(dto.userLat, dto.userLng, storeLat, storeLng);

                var storeTrace = new
                {
                    store.store_id,
                    store.name,
                    distance = Math.Round(distance, 2),
                    withinRadius = distance <= searchRadius,
                    medicineChecks = dto.medicineBaseNames.Select(baseName =>
                    {
                        var med = _db.medicines.FirstOrDefault(m =>
                            m.store_id == store.store_id && m.base_name == baseName);

                        if (med == null)
                            return new { baseName, medFound = false, medId = 0, stock = 0 };

                        int stock = _db.medicine_batches
                            .Where(b => b.med_id == med.med_id && b.remaining_pills > 0)
                            .Sum(b => (int?)b.remaining_pills) ?? 0;

                        return new { baseName, medFound = true, medId = med.med_id, stock };
                    }).ToList()
                };

                trace.Add(storeTrace);
            }

            return Ok(trace);
        }

        // POST api/Stores/DebugNearby  — keep this for testing, remove before production
        [HttpPost]
        [Route("DebugNearby")]
        public IHttpActionResult DebugNearby([FromBody] NearbyStoresRequestDto dto)
        {
            var allStores = _db.medicalstores
                .Where(s => s.latitude != null && s.longitude != null)
                .ToList();

            var debugSteps = allStores.Select(s =>
            {
                double storeLat = (double)s.latitude;
                double storeLng = (double)s.longitude;
                double distance = GetDistanceKm(dto.userLat, dto.userLng, storeLat, storeLng);

                var meds = _db.medicines
                    .Where(m => m.store_id == s.store_id)
                    .Select(m => m.base_name)
                    .ToList();

                var matchedMeds = meds
                    .Where(b => dto.medicineBaseNames.Contains(b))
                    .ToList();

                return new
                {
                    s.store_id,
                    s.name,
                    distance = Math.Round(distance, 2),
                    withinRadius = distance <= dto.radiusKm,
                    allMedsInStore = meds,
                    matchedMeds,
                    matchCount = matchedMeds.Count
                };
            }).ToList();

            return Ok(debugSteps);
        }
    }
}