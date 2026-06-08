using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.GeneralFleet.DTOs;
using UniConnect.GeneralFleet.Interfaces;

namespace UniConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/fleet")]
[Tags("General Fleet")]
public class GeneralFleetController(IGeneralFleetService generalFleetService) : ControllerBase
{
    [HttpGet("tenants/{tenantId:guid}/vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetVehicles(Guid tenantId, CancellationToken ct) =>
        Ok(await generalFleetService.GetVehiclesAsync(tenantId, ct));

    [HttpPost("tenants/{tenantId:guid}/vehicles")]
    public async Task<ActionResult<VehicleDto>> CreateVehicle(Guid tenantId, [FromBody] CreateVehicleRequest request, CancellationToken ct) =>
        Ok(await generalFleetService.CreateVehicleAsync(tenantId, request, ct));

    [HttpPut("vehicles/{vehicleId:guid}")]
    public async Task<ActionResult<VehicleDto>> UpdateVehicle(Guid vehicleId, [FromBody] UpdateVehicleRequest request, CancellationToken ct) =>
        Ok(await generalFleetService.UpdateVehicleAsync(vehicleId, request, ct));

    [HttpDelete("vehicles/{vehicleId:guid}")]
    public async Task<IActionResult> DeleteVehicle(Guid vehicleId, CancellationToken ct)
    {
        await generalFleetService.DeleteVehicleAsync(vehicleId, ct);
        return NoContent();
    }

    [HttpGet("tenants/{tenantId:guid}/tracking")]
    public async Task<ActionResult<IReadOnlyList<FleetVehicleTrackingDto>>> GetTracking(Guid tenantId, CancellationToken ct) =>
        Ok(await generalFleetService.GetFleetTrackingAsync(tenantId, ct));

    [HttpGet("vehicles/{vehicleId:guid}")]
    public async Task<ActionResult<VehicleDto>> GetVehicle(Guid vehicleId, CancellationToken ct)
    {
        var vehicle = await generalFleetService.GetVehicleAsync(vehicleId, ct);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    [HttpGet("vehicles/{vehicleId:guid}/maintenance")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecordDto>>> GetMaintenance(Guid vehicleId, CancellationToken ct) =>
        Ok(await generalFleetService.GetMaintenanceAsync(vehicleId, ct));

    [HttpPost("vehicles/{vehicleId:guid}/maintenance")]
    public async Task<ActionResult<MaintenanceRecordDto>> CreateMaintenance(Guid vehicleId, [FromBody] CreateMaintenanceRequest request, CancellationToken ct) =>
        Ok(await generalFleetService.CreateMaintenanceAsync(vehicleId, request, ct));

    [HttpGet("vehicles/{vehicleId:guid}/locations")]
    public async Task<ActionResult<IReadOnlyList<VehicleLocationDto>>> GetLocations(Guid vehicleId, CancellationToken ct) =>
        Ok(await generalFleetService.GetLocationHistoryAsync(vehicleId, ct));

    [HttpPost("vehicles/{vehicleId:guid}/locations")]
    public async Task<ActionResult<VehicleLocationDto>> RecordLocation(Guid vehicleId, [FromBody] RecordLocationRequest request, CancellationToken ct) =>
        Ok(await generalFleetService.RecordLocationAsync(vehicleId, request, ct));
}
