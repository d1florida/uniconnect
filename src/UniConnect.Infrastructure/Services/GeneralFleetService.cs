using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.GeneralFleet.DTOs;
using UniConnect.GeneralFleet.Entities;
using UniConnect.GeneralFleet.Enums;
using UniConnect.GeneralFleet.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Tenant;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services;

public class GeneralFleetService(AppDbContext db, ICurrentUserService currentUser) : IGeneralFleetService
{
    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsureGeneralFleetAsync(tenantId, ct);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.TenantId == tenantId).ToListAsync(ct);
        var locs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);
        return vehicles.Select(v => ToVehicleDto(v, locs.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<VehicleDto?> GetVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var v = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vehicleId, ct);
        if (v is null) return null;
        await EnsureGeneralFleetAsync(v.TenantId, ct);
        var loc = await LocationHelper.GetLatestAsync(db, vehicleId, ct);
        return ToVehicleDto(v, loc);
    }

    public async Task<VehicleDto> CreateVehicleAsync(Guid tenantId, CreateVehicleRequest request, CancellationToken ct = default)
    {
        await EnsureGeneralFleetAsync(tenantId, ct);

        var vin = request.Vin.Trim();
        var make = request.Make.Trim();
        var model = request.Model.Trim();
        var licensePlate = request.LicensePlate.Trim();
        if (string.IsNullOrEmpty(vin))
            throw new ArgumentException("VIN is required.");
        if (string.IsNullOrEmpty(make))
            throw new ArgumentException("Make is required.");
        if (string.IsNullOrEmpty(model))
            throw new ArgumentException("Model is required.");
        if (string.IsNullOrEmpty(licensePlate))
            throw new ArgumentException("License plate is required.");
        if (await db.Vehicles.AsNoTracking().AnyAsync(v => v.Vin == vin, ct))
            throw new ArgumentException("A vehicle with this VIN already exists.");

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Vin = vin,
            Make = make,
            Model = model,
            Year = request.Year,
            LicensePlate = licensePlate,
            CurrentMileage = request.CurrentMileage,
            Status = VehicleStatus.Active
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        return ToVehicleDto(vehicle, null);
    }

    public async Task DeleteVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        await EnsureVehicleAccessAsync(vehicleId, ct);

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehicle not found.");

        if (await db.DeliveryAssignments.AsNoTracking().AnyAsync(a => a.VehicleId == vehicleId, ct))
            throw new InvalidOperationException("Cannot delete a vehicle that is assigned to a delivery order.");

        if (await db.DeliveryRoutes.AsNoTracking().AnyAsync(r => r.VehicleId == vehicleId, ct))
            throw new InvalidOperationException("Cannot delete a vehicle that is assigned to a delivery route.");

        var profile = await db.RoboTaxiProfiles.FindAsync([vehicleId], ct);
        if (profile is not null)
            db.RoboTaxiProfiles.Remove(profile);

        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MaintenanceRecordDto>> GetMaintenanceAsync(Guid vehicleId, CancellationToken ct = default)
    {
        await EnsureVehicleAccessAsync(vehicleId, ct);
        return await db.MaintenanceRecords.AsNoTracking()
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.PerformedOn)
            .Select(m => new MaintenanceRecordDto(
                m.Id, m.VehicleId, m.ServiceType, m.PerformedOn, m.MileageAtService, m.Cost, m.Notes, m.Status))
            .ToListAsync(ct);
    }

    public async Task<MaintenanceRecordDto> CreateMaintenanceAsync(Guid vehicleId, CreateMaintenanceRequest request, CancellationToken ct = default)
    {
        await EnsureVehicleAccessAsync(vehicleId, ct);
        var record = new MaintenanceRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = request.ServiceType,
            PerformedOn = request.PerformedOn,
            MileageAtService = request.MileageAtService,
            Cost = request.Cost,
            Notes = request.Notes,
            Status = request.Status
        };
        db.MaintenanceRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return new MaintenanceRecordDto(record.Id, record.VehicleId, record.ServiceType, record.PerformedOn,
            record.MileageAtService, record.Cost, record.Notes, record.Status);
    }

    public async Task<IReadOnlyList<FleetVehicleTrackingDto>> GetFleetTrackingAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsureGeneralFleetAsync(tenantId, ct);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.TenantId == tenantId).ToListAsync(ct);
        var locs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);
        return vehicles.Select(v => new FleetVehicleTrackingDto(
            v.Id, v.LicensePlate, v.Status, locs.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<IReadOnlyList<VehicleLocationDto>> GetLocationHistoryAsync(Guid vehicleId, CancellationToken ct = default)
    {
        await EnsureVehicleAccessAsync(vehicleId, ct);
        return await db.VehicleLocations.AsNoTracking()
            .Where(l => l.VehicleId == vehicleId)
            .OrderByDescending(l => l.RecordedAt)
            .Take(50)
            .Select(l => new VehicleLocationDto(l.Id, l.Latitude, l.Longitude, l.RecordedAt, l.SpeedKph, l.Source))
            .ToListAsync(ct);
    }

    public async Task<VehicleLocationDto> RecordLocationAsync(Guid vehicleId, RecordLocationRequest request, CancellationToken ct = default)
    {
        await EnsureVehicleAccessAsync(vehicleId, ct);
        var loc = new VehicleLocation
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RecordedAt = DateTime.UtcNow,
            SpeedKph = request.SpeedKph,
            Source = LocationSource.Manual
        };
        db.VehicleLocations.Add(loc);
        await db.SaveChangesAsync(ct);
        return new VehicleLocationDto(loc.Id, loc.Latitude, loc.Longitude, loc.RecordedAt, loc.SpeedKph, loc.Source);
    }

    private async Task EnsureGeneralFleetAsync(Guid tenantId, CancellationToken ct)
    {
        var modules = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Modules)
            .FirstOrDefaultAsync(ct);
        if (!ProductModuleHelper.HasModule(modules, ProductModule.General))
            throw new InvalidOperationException("This operation is only available for General Fleet tenants.");
        currentUser.EnsureTenantAccess(tenantId);
    }

    private async Task EnsureVehicleAccessAsync(Guid vehicleId, CancellationToken ct)
    {
        var tenantId = await db.Vehicles.AsNoTracking().Where(v => v.Id == vehicleId).Select(v => v.TenantId).FirstOrDefaultAsync(ct);
        if (tenantId == Guid.Empty) throw new InvalidOperationException("Vehicle not found.");
        await EnsureGeneralFleetAsync(tenantId, ct);
    }

    private static VehicleDto ToVehicleDto(Vehicle v, LocationDto? loc) =>
        new(v.Id, v.TenantId, v.Vin, v.Make, v.Model, v.Year, v.LicensePlate, v.CurrentMileage, v.Status, loc);
}
