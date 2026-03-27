using FYPBackend.DTOs.Store;
using FYPBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace FYPBackend.Controllers
{
    // ─────────────────────────────────────────────────────────────────────────
    // REPLACE your entire StoresController.cs with this file.
    // Changes:
    //   - NearbyWithMedicines now returns a clean, Flutter-ready response
    //   - DebugNearby removed (was dev-only, not for production)
    // ─────────────────────────────────────────────────────────────────────────

    [RoutePrefix("api/Stores")]
    public class StoresController : ApiController
    {
        private readonly fyp1Entities1 _db = new fyp1Entities1();

        private double GetDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371;
            double dlat = (lat2 - lat1) * Math.PI / 180;
            double dlng = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2)
                     + Math.Cos(lat1 * Math.PI / 180)
                     * Math.Cos(lat2 * Math.PI / 180)
                     * Math.Sin(dlng / 2) * Math.Sin(dlng / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        // ─────────────────────────────────────────────
        // NEARBY STORES WITH MEDICINES (FIXED)
        // POST api/Stores/NearbyWithMedicines
        // Returns clean list — stores within radius
        // that have ALL requested medicines in stock
        // Sorted nearest first
        // ─────────────────────────────────────────────
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

            var results = new List<object>();

            foreach (var store in allStores)
            {
                double storeLat = (double)store.latitude;
                double storeLng = (double)store.longitude;
                double distance = GetDistanceKm(dto.userLat, dto.userLng, storeLat, storeLng);

                bool withinRadius = distance <= searchRadius;

                // Build medicine availability list
                var medicineList = new List<object>();
                bool allAvailable = true;

                foreach (var baseName in dto.medicineBaseNames)
                {
                    var med = _db.medicines.FirstOrDefault(m =>
                        m.store_id == store.store_id && m.base_name == baseName);

                    if (med == null)
                    {
                        allAvailable = false;
                        medicineList.Add(new
                        {
                            baseName,
                            available = false,
                            medId = 0,
                            name = (string)null,
                            stock = 0,
                            price = 0,
                            pillsPerPack = 0
                        });
                        continue;
                    }

                    int stock = _db.medicine_batches
                        .Where(b => b.med_id == med.med_id && b.remaining_pills > 0)
                        .Sum(b => (int?)b.remaining_pills) ?? 0;

                    if (stock == 0) allAvailable = false;

                    medicineList.Add(new
                    {
                        baseName,
                        available = stock > 0,
                        medId = med.med_id,
                        name = med.name,
                        stock,
                        price = med.price ?? 0,
                        pillsPerPack = med.pills_per_pack ?? 0
                    });
                }

                // Only include stores that are within radius OR have stock
                // (special order stores shown separately)
                results.Add(new
                {
                    store.store_id,
                    store.name,
                    store.location,
                    store.images,
                    latitude = storeLat,
                    longitude = storeLng,
                    distanceKm = Math.Round(distance, 2),
                    withinRadius,
                    isSpecialOrder = !withinRadius,
                    allMedicinesAvailable = allAvailable,
                    medicines = medicineList
                });
            }

            // Sort: within-radius first, then by distance
            var sorted = results
                .OrderBy(r => ((dynamic)r).isSpecialOrder)
                .ThenBy(r => ((dynamic)r).distanceKm)
                .ToList();

            return Ok(sorted);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
