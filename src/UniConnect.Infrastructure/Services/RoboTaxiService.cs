using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.GeneralFleet.DTOs;
using UniConnect.GeneralFleet.Entities;
using UniConnect.GeneralFleet.Enums;
using UniConnect.Infrastructure.Data;
using UniConnect.RoboTaxi.DTOs;
using UniConnect.RoboTaxi.Entities;
using UniConnect.RoboTaxi.Enums;
using UniConnect.RoboTaxi.Interfaces;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services;

public class RoboTaxiService(AppDbContext db, ICurrentUserService currentUser) : IRoboTaxiService
{
    public async Task<IReadOnlyList<RoboTaxiVehicleDto>> GetVehiclesAsync(Guid tenantId, CancellationToken ct = default)
    {
        currentUser.EnsureTenantAccess(tenantId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.TenantId == tenantId).ToListAsync(ct);
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
        currentUser.EnsureTenantAccess(v.TenantId);
        var loc = await LocationHelper.GetLatestAsync(db, vehicleId, ct);
        return MapVehicle(v, p, loc);
    }

    public async Task<RoboTaxiVehicleDto> CreateVehicleAsync(Guid tenantId, CreateRoboTaxiVehicleRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureTenantAccess(tenantId);
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Vin = request.Vin,
            Make = request.Make,
            Model = request.Model,
            Year = request.Year,
            Category = request.Category,
            VehicleNumber = request.VehicleNumber.Trim(),
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
        var tenantId = await db.Vehicles.Where(v => v.Id == vehicleId).Select(v => v.TenantId).FirstAsync(ct);
        currentUser.EnsureTenantAccess(tenantId);

        profile.OperationalState = request.OperationalState;
        profile.BatteryPercent = request.BatteryPercent;
        profile.GroundedReason = request.OperationalState == OperationalState.Grounded
            ? request.GroundedReason ?? GroundedReason.SafetyReview
            : GroundedReason.None;

        await db.SaveChangesAsync(ct);
        return MapProfile(profile);
    }

    public async Task<IReadOnlyList<RoboTaxiTrackingDto>> GetTrackingAsync(Guid tenantId, CancellationToken ct = default)
    {
        currentUser.EnsureTenantAccess(tenantId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.TenantId == tenantId).ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => vehicles.Select(v => v.Id).Contains(p.VehicleId))
            .ToDictionaryAsync(p => p.VehicleId, ct);
        var locs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);

        return vehicles
            .Where(v => profiles.ContainsKey(v.Id))
            .Select(v => new RoboTaxiTrackingDto(
                v.Id, v.Category, v.VehicleNumber, v.LicensePlate, profiles[v.Id].OperationalState, locs.GetValueOrDefault(v.Id)))
            .ToList();
    }

    public async Task<RoboTaxiDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var tenantIdsQuery = db.Tenants.AsNoTracking().Where(t => (t.Modules & ProductModule.RoboTaxi) == ProductModule.RoboTaxi);
        if (!currentUser.IsPlatformAdmin && currentUser.TenantId.HasValue)
            tenantIdsQuery = tenantIdsQuery.Where(t => t.Id == currentUser.TenantId.Value);
        var tenantIds = await tenantIdsQuery.Select(t => t.Id).ToListAsync(ct);
        var tenantVehicleIds = await db.Vehicles.AsNoTracking()
            .Where(v => tenantIds.Contains(v.TenantId)).Select(v => v.Id).ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => tenantVehicleIds.Contains(p.VehicleId)).ToListAsync(ct);
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
        new(v.Id, v.TenantId, v.Vin, v.Make, v.Model, v.Year, v.Category, v.VehicleNumber, v.LicensePlate, v.CurrentMileage, MapProfile(p), loc);

    private static RoboTaxiProfileDto MapProfile(RoboTaxiProfile p) =>
        new(p.VehicleId, p.AutonomyLevel, p.OperationalState, p.SoftwareVersion, p.BatteryPercent,
            p.PassengerCapacity, p.LastDisengagementAt, p.GroundedReason);
}
