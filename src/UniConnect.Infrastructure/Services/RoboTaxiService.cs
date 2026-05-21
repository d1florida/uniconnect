using Microsoft.EntityFrameworkCore;
using UniConnect.Application.DTOs;
using UniConnect.Application.Interfaces;
using UniConnect.Domain.Entities;
using UniConnect.Domain.Enums;
using UniConnect.Infrastructure.Data;
using UniConnect.RoboTaxi.DTOs;
using UniConnect.RoboTaxi.Entities;
using UniConnect.RoboTaxi.Enums;
using UniConnect.RoboTaxi.Interfaces;

namespace UniConnect.Infrastructure.Services;

public class RoboTaxiService(AppDbContext db, ICurrentUserService currentUser) : IRoboTaxiService
{
    public async Task<IReadOnlyList<FleetDto>> GetFleetsAsync(CancellationToken ct = default)
    {
        var query = db.Fleets.AsNoTracking().Where(f => f.FleetType == FleetType.RoboTaxi);
        if (!currentUser.IsPlatformAdmin && currentUser.FleetId.HasValue)
            query = query.Where(f => f.Id == currentUser.FleetId.Value);
        return await query.OrderBy(f => f.Name)
            .Select(f => new FleetDto(f.Id, f.Name, f.Slug, f.FleetType, f.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<FleetDto> CreateFleetAsync(string name, string slug, CancellationToken ct = default)
    {
        if (!currentUser.IsPlatformAdmin)
            throw new UnauthorizedAccessException("Only platform administrators can create fleets.");
        var fleet = new Fleet
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            FleetType = FleetType.RoboTaxi,
            CreatedAt = DateTime.UtcNow
        };
        db.Fleets.Add(fleet);
        await db.SaveChangesAsync(ct);
        return new FleetDto(fleet.Id, fleet.Name, fleet.Slug, fleet.FleetType, fleet.CreatedAt);
    }

    public async Task<IReadOnlyList<RoboTaxiVehicleDto>> GetVehiclesAsync(Guid fleetId, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.FleetId == fleetId).ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => vehicles.Select(v => v.Id).Contains(p.VehicleId))
            .ToDictionaryAsync(p => p.VehicleId, ct);
        var locs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);

        return vehicles
            .Where(v => profiles.ContainsKey(v.Id))
            .Select(v => MapVehicle(v, profiles[v.Id], locs.GetValueOrDefault(v.Id)))
            .ToList();
    }

    public async Task<RoboTaxiVehicleDto?> GetVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var v = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vehicleId, ct);
        var p = await db.RoboTaxiProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.VehicleId == vehicleId, ct);
        if (v is null || p is null) return null;
        currentUser.EnsureFleetAccess(v.FleetId);
        var loc = await LocationHelper.GetLatestAsync(db, vehicleId, ct);
        return MapVehicle(v, p, loc);
    }

    public async Task<RoboTaxiVehicleDto> CreateVehicleAsync(Guid fleetId, CreateRoboTaxiVehicleRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            FleetId = fleetId,
            Vin = request.Vin,
            Make = request.Make,
            Model = request.Model,
            Year = request.Year,
            LicensePlate = request.LicensePlate,
            CurrentMileage = request.CurrentMileage,
            Status = VehicleStatus.Active
        };
        var profile = new RoboTaxiProfile
        {
            VehicleId = vehicle.Id,
            AutonomyLevel = request.AutonomyLevel,
            OperationalState = OperationalState.Idle,
            SoftwareVersion = request.SoftwareVersion,
            PassengerCapacity = request.PassengerCapacity,
            GroundedReason = GroundedReason.None
        };
        db.Vehicles.Add(vehicle);
        db.RoboTaxiProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return MapVehicle(vehicle, profile, null);
    }

    public async Task<RoboTaxiProfileDto> UpdateStateAsync(Guid vehicleId, UpdateRoboTaxiStateRequest request, CancellationToken ct = default)
    {
        var profile = await db.RoboTaxiProfiles.FirstOrDefaultAsync(p => p.VehicleId == vehicleId, ct)
            ?? throw new InvalidOperationException("Robo-taxi profile not found.");
        var fleetId = await db.Vehicles.Where(v => v.Id == vehicleId).Select(v => v.FleetId).FirstAsync(ct);
        currentUser.EnsureFleetAccess(fleetId);

        profile.OperationalState = request.OperationalState;
        profile.BatteryPercent = request.BatteryPercent;
        profile.GroundedReason = request.OperationalState == OperationalState.Grounded
            ? request.GroundedReason ?? GroundedReason.SafetyReview
            : GroundedReason.None;

        await db.SaveChangesAsync(ct);
        return MapProfile(profile);
    }

    public async Task<IReadOnlyList<RoboTaxiTrackingDto>> GetTrackingAsync(Guid fleetId, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.FleetId == fleetId).ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => vehicles.Select(v => v.Id).Contains(p.VehicleId))
            .ToDictionaryAsync(p => p.VehicleId, ct);
        var locs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);

        return vehicles
            .Where(v => profiles.ContainsKey(v.Id))
            .Select(v => new RoboTaxiTrackingDto(
                v.Id, v.LicensePlate, profiles[v.Id].OperationalState, locs.GetValueOrDefault(v.Id)))
            .ToList();
    }

    public async Task<RoboTaxiDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var fleetIdsQuery = db.Fleets.AsNoTracking().Where(f => f.FleetType == FleetType.RoboTaxi);
        if (!currentUser.IsPlatformAdmin && currentUser.FleetId.HasValue)
            fleetIdsQuery = fleetIdsQuery.Where(f => f.Id == currentUser.FleetId.Value);
        var fleetIds = await fleetIdsQuery.Select(f => f.Id).ToListAsync(ct);
        var fleetVehicleIds = await db.Vehicles.AsNoTracking()
            .Where(v => fleetIds.Contains(v.FleetId)).Select(v => v.Id).ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => fleetVehicleIds.Contains(p.VehicleId)).ToListAsync(ct);
        var vehicleIds = profiles.Select(p => p.VehicleId).ToList();
        var staleThreshold = DateTime.UtcNow.AddHours(-24);
        var recentLocs = await db.VehicleLocations.AsNoTracking()
            .Where(l => vehicleIds.Contains(l.VehicleId) && l.RecordedAt >= staleThreshold)
            .Select(l => l.VehicleId)
            .Distinct()
            .ToListAsync(ct);

        return new RoboTaxiDashboardDto(
            profiles.Count,
            profiles.Count(p => p.OperationalState == OperationalState.Idle),
            profiles.Count(p => p.OperationalState == OperationalState.OnTrip),
            profiles.Count(p => p.OperationalState == OperationalState.Charging),
            profiles.Count(p => p.OperationalState == OperationalState.Maintenance),
            profiles.Count(p => p.OperationalState == OperationalState.Grounded),
            profiles.Count(p => !recentLocs.Contains(p.VehicleId)));
    }

    private static RoboTaxiVehicleDto MapVehicle(Vehicle v, RoboTaxiProfile p, LocationDto? loc) =>
        new(v.Id, v.FleetId, v.Vin, v.Make, v.Model, v.Year, v.LicensePlate, v.CurrentMileage, MapProfile(p), loc);

    private static RoboTaxiProfileDto MapProfile(RoboTaxiProfile p) =>
        new(p.VehicleId, p.AutonomyLevel, p.OperationalState, p.SoftwareVersion, p.BatteryPercent,
            p.PassengerCapacity, p.LastDisengagementAt, p.GroundedReason);
}
