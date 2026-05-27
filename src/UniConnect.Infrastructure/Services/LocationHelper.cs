using Microsoft.EntityFrameworkCore;
using UniConnect.GeneralFleet.DTOs;
using UniConnect.GeneralFleet.Entities;
using UniConnect.Infrastructure.Data;

namespace UniConnect.Infrastructure.Services;

internal static class LocationHelper
{
    public static async Task<LocationDto?> GetLatestAsync(AppDbContext db, Guid vehicleId, CancellationToken ct)
    {
        var loc = await db.VehicleLocations
            .AsNoTracking()
            .Where(l => l.VehicleId == vehicleId)
            .OrderByDescending(l => l.RecordedAt)
            .FirstOrDefaultAsync(ct);

        return loc is null ? null : new LocationDto(loc.Latitude, loc.Longitude, loc.RecordedAt, loc.SpeedKph);
    }

    public static async Task<Dictionary<Guid, LocationDto>> GetLatestForVehiclesAsync(
        AppDbContext db, IEnumerable<Guid> vehicleIds, CancellationToken ct)
    {
        var ids = vehicleIds.ToList();
        if (ids.Count == 0) return [];

        var locations = await db.VehicleLocations
            .AsNoTracking()
            .Where(l => ids.Contains(l.VehicleId))
            .OrderByDescending(l => l.RecordedAt)
            .ToListAsync(ct);

        return locations
            .GroupBy(l => l.VehicleId)
            .ToDictionary(g => g.Key, g =>
            {
                var loc = g.First();
                return new LocationDto(loc.Latitude, loc.Longitude, loc.RecordedAt, loc.SpeedKph);
            });
    }
}
