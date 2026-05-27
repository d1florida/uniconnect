using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.RoboTaxi.DTOs;
using UniConnect.RoboTaxi.Interfaces;

namespace UniConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/robo-taxis")]
[Tags("Robo-Taxi")]
public class RoboTaxisController(IRoboTaxiService roboTaxiService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<RoboTaxiDashboardDto>> GetDashboard(CancellationToken ct) =>
        Ok(await roboTaxiService.GetDashboardAsync(ct));

    [HttpGet("tenants/{tenantId:guid}/vehicles")]
    public async Task<ActionResult<IReadOnlyList<RoboTaxiVehicleDto>>> GetVehicles(Guid tenantId, CancellationToken ct) =>
        Ok(await roboTaxiService.GetVehiclesAsync(tenantId, ct));

    [HttpPost("tenants/{tenantId:guid}/vehicles")]
    public async Task<ActionResult<RoboTaxiVehicleDto>> CreateVehicle(Guid tenantId, [FromBody] CreateRoboTaxiVehicleRequest request, CancellationToken ct) =>
        Ok(await roboTaxiService.CreateVehicleAsync(tenantId, request, ct));

    [HttpGet("tenants/{tenantId:guid}/tracking")]
    public async Task<ActionResult<IReadOnlyList<RoboTaxiTrackingDto>>> GetTracking(Guid tenantId, CancellationToken ct) =>
        Ok(await roboTaxiService.GetTrackingAsync(tenantId, ct));

    [HttpGet("vehicles/{vehicleId:guid}")]
    public async Task<ActionResult<RoboTaxiVehicleDto>> GetVehicle(Guid vehicleId, CancellationToken ct)
    {
        var vehicle = await roboTaxiService.GetVehicleAsync(vehicleId, ct);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    [HttpPatch("vehicles/{vehicleId:guid}/state")]
    public async Task<ActionResult<RoboTaxiProfileDto>> UpdateState(Guid vehicleId, [FromBody] UpdateRoboTaxiStateRequest request, CancellationToken ct) =>
        Ok(await roboTaxiService.UpdateStateAsync(vehicleId, request, ct));
}
