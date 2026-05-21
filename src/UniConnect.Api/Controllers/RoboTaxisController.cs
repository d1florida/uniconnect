using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.DTOs;
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

    [HttpGet("fleets")]
    public async Task<ActionResult<IReadOnlyList<FleetDto>>> GetFleets(CancellationToken ct) =>
        Ok(await roboTaxiService.GetFleetsAsync(ct));

    [HttpPost("fleets")]
    public async Task<ActionResult<FleetDto>> CreateFleet([FromBody] CreateFleetRequest request, CancellationToken ct) =>
        Ok(await roboTaxiService.CreateFleetAsync(request.Name, request.Slug, ct));

    [HttpGet("fleets/{fleetId:guid}/vehicles")]
    public async Task<ActionResult<IReadOnlyList<RoboTaxiVehicleDto>>> GetVehicles(Guid fleetId, CancellationToken ct) =>
        Ok(await roboTaxiService.GetVehiclesAsync(fleetId, ct));

    [HttpPost("fleets/{fleetId:guid}/vehicles")]
    public async Task<ActionResult<RoboTaxiVehicleDto>> CreateVehicle(Guid fleetId, [FromBody] CreateRoboTaxiVehicleRequest request, CancellationToken ct) =>
        Ok(await roboTaxiService.CreateVehicleAsync(fleetId, request, ct));

    [HttpGet("fleets/{fleetId:guid}/tracking")]
    public async Task<ActionResult<IReadOnlyList<RoboTaxiTrackingDto>>> GetTracking(Guid fleetId, CancellationToken ct) =>
        Ok(await roboTaxiService.GetTrackingAsync(fleetId, ct));

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
