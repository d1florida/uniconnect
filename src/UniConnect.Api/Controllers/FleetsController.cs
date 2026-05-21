using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.DTOs;
using UniConnect.Application.Interfaces;
using UniConnect.Domain.Enums;

namespace UniConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/fleets")]
[Tags("Fleet")]
public class FleetsController(IFleetService fleetService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FleetDto>>> GetFleets([FromQuery] FleetType? fleetType, CancellationToken ct) =>
        Ok(await fleetService.GetFleetsAsync(fleetType, ct));

    [HttpPost]
    public async Task<ActionResult<FleetDto>> CreateFleet([FromBody] CreateFleetRequest request, CancellationToken ct) =>
        Ok(await fleetService.CreateFleetAsync(request.Name, request.Slug, FleetType.General, ct));

    [HttpGet("{fleetId:guid}/vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetVehicles(Guid fleetId, CancellationToken ct) =>
        Ok(await fleetService.GetVehiclesAsync(fleetId, ct));

    [HttpPost("{fleetId:guid}/vehicles")]
    public async Task<ActionResult<VehicleDto>> CreateVehicle(Guid fleetId, [FromBody] CreateVehicleRequest request, CancellationToken ct) =>
        Ok(await fleetService.CreateVehicleAsync(fleetId, request, ct));

    [HttpGet("{fleetId:guid}/tracking")]
    public async Task<ActionResult<IReadOnlyList<FleetVehicleTrackingDto>>> GetTracking(Guid fleetId, CancellationToken ct) =>
        Ok(await fleetService.GetFleetTrackingAsync(fleetId, ct));
}

[Authorize]
[ApiController]
[Route("api/vehicles")]
[Tags("Fleet")]
public class VehiclesController(IFleetService fleetService) : ControllerBase
{
    [HttpGet("{vehicleId:guid}")]
    public async Task<ActionResult<VehicleDto>> GetVehicle(Guid vehicleId, CancellationToken ct)
    {
        var vehicle = await fleetService.GetVehicleAsync(vehicleId, ct);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    [HttpGet("{vehicleId:guid}/maintenance")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecordDto>>> GetMaintenance(Guid vehicleId, CancellationToken ct) =>
        Ok(await fleetService.GetMaintenanceAsync(vehicleId, ct));

    [HttpPost("{vehicleId:guid}/maintenance")]
    public async Task<ActionResult<MaintenanceRecordDto>> CreateMaintenance(Guid vehicleId, [FromBody] CreateMaintenanceRequest request, CancellationToken ct) =>
        Ok(await fleetService.CreateMaintenanceAsync(vehicleId, request, ct));

    [HttpGet("{vehicleId:guid}/locations")]
    public async Task<ActionResult<IReadOnlyList<VehicleLocationDto>>> GetLocations(Guid vehicleId, CancellationToken ct) =>
        Ok(await fleetService.GetLocationHistoryAsync(vehicleId, ct));

    [HttpPost("{vehicleId:guid}/locations")]
    public async Task<ActionResult<VehicleLocationDto>> RecordLocation(Guid vehicleId, [FromBody] RecordLocationRequest request, CancellationToken ct) =>
        Ok(await fleetService.RecordLocationAsync(vehicleId, request, ct));
}
