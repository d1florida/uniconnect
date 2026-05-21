using Microsoft.EntityFrameworkCore;
using UniConnect.Application.DTOs;
using UniConnect.Application.Interfaces;
using UniConnect.Domain.Entities;
using UniConnect.Domain.Enums;
using UniConnect.Infrastructure.Data;

namespace UniConnect.Infrastructure.Services;

public class FleetService(AppDbContext db, ICurrentUserService currentUser) : IFleetService
{
    public async Task<IReadOnlyList<FleetDto>> GetFleetsAsync(FleetType? fleetType, CancellationToken ct = default)
    {
        var query = db.Fleets.AsNoTracking();
        if (!currentUser.IsPlatformAdmin && currentUser.FleetId.HasValue)
            query = query.Where(f => f.Id == currentUser.FleetId.Value);
        else if (fleetType.HasValue)
            query = query.Where(f => f.FleetType == fleetType.Value);

        return await query
            .OrderBy(f => f.Name)
            .Select(f => new FleetDto(f.Id, f.Name, f.Slug, f.FleetType, f.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<FleetDto> CreateFleetAsync(string name, string slug, FleetType fleetType, CancellationToken ct = default)
    {
        if (!currentUser.IsPlatformAdmin)
            throw new UnauthorizedAccessException("Only platform administrators can create fleets.");

        var fleet = new Fleet
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            FleetType = fleetType,
            CreatedAt = DateTime.UtcNow
        };
        db.Fleets.Add(fleet);
        await db.SaveChangesAsync(ct);
        return new FleetDto(fleet.Id, fleet.Name, fleet.Slug, fleet.FleetType, fleet.CreatedAt);
    }

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid fleetId, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.FleetId == fleetId).ToListAsync(ct);
        var locs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);
        return vehicles.Select(v => ToVehicleDto(v, locs.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<VehicleDto?> GetVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var v = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vehicleId, ct);
        if (v is null) return null;
        currentUser.EnsureFleetAccess(v.FleetId);
        var loc = await LocationHelper.GetLatestAsync(db, vehicleId, ct);
        return ToVehicleDto(v, loc);
    }

    public async Task<VehicleDto> CreateVehicleAsync(Guid fleetId, CreateVehicleRequest request, CancellationToken ct = default)
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
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        return ToVehicleDto(vehicle, null);
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

    public async Task<IReadOnlyList<FleetVehicleTrackingDto>> GetFleetTrackingAsync(Guid fleetId, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.FleetId == fleetId).ToListAsync(ct);
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

    private async Task EnsureVehicleAccessAsync(Guid vehicleId, CancellationToken ct)
    {
        var fleetId = await db.Vehicles.AsNoTracking().Where(v => v.Id == vehicleId).Select(v => v.FleetId).FirstOrDefaultAsync(ct);
        if (fleetId == Guid.Empty) throw new InvalidOperationException("Vehicle not found.");
        currentUser.EnsureFleetAccess(fleetId);
    }

    private static VehicleDto ToVehicleDto(Vehicle v, LocationDto? loc) =>
        new(v.Id, v.FleetId, v.Vin, v.Make, v.Model, v.Year, v.LicensePlate, v.CurrentMileage, v.Status, loc);
}
