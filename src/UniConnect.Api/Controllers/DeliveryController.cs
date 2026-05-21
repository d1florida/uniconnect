using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.DTOs;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Enums;
using UniConnect.Delivery.Interfaces;

namespace UniConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/delivery")]
[Tags("Delivery")]
public class DeliveryController(IDeliveryService deliveryService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DeliveryDashboardDto>> GetDashboard(CancellationToken ct) =>
        Ok(await deliveryService.GetDashboardAsync(ct));

    [HttpGet("fleets")]
    public async Task<ActionResult<IReadOnlyList<FleetDto>>> GetFleets(CancellationToken ct) =>
        Ok(await deliveryService.GetFleetsAsync(ct));

    [HttpPost("fleets")]
    public async Task<ActionResult<FleetDto>> CreateFleet([FromBody] CreateFleetRequest request, CancellationToken ct) =>
        Ok(await deliveryService.CreateFleetAsync(request.Name, request.Slug, ct));

    [HttpGet("fleets/{fleetId:guid}/business-accounts")]
    public async Task<ActionResult<IReadOnlyList<BusinessAccountDto>>> GetBusinessAccounts(Guid fleetId, CancellationToken ct) =>
        Ok(await deliveryService.GetBusinessAccountsAsync(fleetId, ct));

    [HttpPost("fleets/{fleetId:guid}/business-accounts")]
    public async Task<ActionResult<BusinessAccountDto>> CreateBusinessAccount(Guid fleetId, [FromBody] CreateBusinessAccountRequest request, CancellationToken ct) =>
        Ok(await deliveryService.CreateBusinessAccountAsync(fleetId, request, ct));

    [HttpGet("fleets/{fleetId:guid}/orders")]
    public async Task<ActionResult<IReadOnlyList<DeliveryOrderDto>>> GetOrders(Guid fleetId, [FromQuery] DeliveryChannel? channel, CancellationToken ct) =>
        Ok(await deliveryService.GetOrdersAsync(fleetId, channel, ct));

    [HttpPost("fleets/{fleetId:guid}/orders")]
    public async Task<ActionResult<DeliveryOrderDto>> CreateOrder(Guid fleetId, [FromBody] CreateDeliveryOrderRequest request, CancellationToken ct) =>
        Ok(await deliveryService.CreateOrderAsync(fleetId, request, ct));

    [HttpGet("fleets/{fleetId:guid}/vehicles")]
    public async Task<ActionResult<IReadOnlyList<DeliveryVehicleDto>>> GetVehicles(Guid fleetId, CancellationToken ct) =>
        Ok(await deliveryService.GetVehiclesAsync(fleetId, ct));

    [HttpGet("fleets/{fleetId:guid}/tracking")]
    public async Task<ActionResult<IReadOnlyList<DeliveryTrackingDto>>> GetTracking(Guid fleetId, CancellationToken ct) =>
        Ok(await deliveryService.GetTrackingAsync(fleetId, ct));

    [HttpGet("orders/{orderId:guid}")]
    public async Task<ActionResult<DeliveryOrderDto>> GetOrder(Guid orderId, CancellationToken ct)
    {
        var order = await deliveryService.GetOrderAsync(orderId, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPatch("orders/{orderId:guid}/status")]
    public async Task<ActionResult<DeliveryOrderDto>> UpdateStatus(Guid orderId, [FromBody] UpdateDeliveryStatusRequest request, CancellationToken ct) =>
        Ok(await deliveryService.UpdateStatusAsync(orderId, request, ct));

    [HttpPost("orders/{orderId:guid}/assign")]
    public async Task<ActionResult<DeliveryOrderDto>> Assign(Guid orderId, [FromBody] AssignDeliveryRequest request, CancellationToken ct) =>
        Ok(await deliveryService.AssignAsync(orderId, request, ct));

    [HttpGet("fleets/{fleetId:guid}/routes")]
    public async Task<ActionResult<IReadOnlyList<DeliveryRouteDto>>> GetRoutes(Guid fleetId, CancellationToken ct) =>
        Ok(await deliveryService.GetRoutesAsync(fleetId, ct));

    [HttpPost("fleets/{fleetId:guid}/routes")]
    public async Task<ActionResult<DeliveryRouteDetailDto>> CreateRoute(Guid fleetId, [FromBody] CreateDeliveryRouteRequest request, CancellationToken ct) =>
        Ok(await deliveryService.CreateRouteAsync(fleetId, request, ct));

    [HttpGet("routes/{routeId:guid}")]
    public async Task<ActionResult<DeliveryRouteDetailDto>> GetRoute(Guid routeId, CancellationToken ct)
    {
        var route = await deliveryService.GetRouteAsync(routeId, ct);
        return route is null ? NotFound() : Ok(route);
    }

    [HttpPut("routes/{routeId:guid}")]
    public async Task<ActionResult<DeliveryRouteDetailDto>> UpdateRoute(Guid routeId, [FromBody] UpdateDeliveryRouteRequest request, CancellationToken ct) =>
        Ok(await deliveryService.UpdateRouteAsync(routeId, request, ct));

    [HttpPatch("routes/{routeId:guid}/status")]
    public async Task<ActionResult<DeliveryRouteDetailDto>> UpdateRouteStatus(Guid routeId, [FromBody] UpdateRouteStatusRequest request, CancellationToken ct) =>
        Ok(await deliveryService.UpdateRouteStatusAsync(routeId, request, ct));

    [HttpPost("routes/{routeId:guid}/assign")]
    public async Task<ActionResult<DeliveryRouteDetailDto>> AssignRoute(Guid routeId, [FromBody] AssignRouteRequest request, CancellationToken ct) =>
        Ok(await deliveryService.AssignRouteAsync(routeId, request, ct));

    [HttpPost("routes/{routeId:guid}/stops")]
    public async Task<ActionResult<DeliveryRouteStopDto>> AddRouteStop(Guid routeId, [FromBody] AddRouteStopRequest request, CancellationToken ct) =>
        Ok(await deliveryService.AddRouteStopAsync(routeId, request, ct));

    [HttpPut("routes/{routeId:guid}/stops/{stopId:guid}")]
    public async Task<ActionResult<DeliveryRouteStopDto>> UpdateRouteStop(Guid routeId, Guid stopId, [FromBody] UpdateRouteStopRequest request, CancellationToken ct) =>
        Ok(await deliveryService.UpdateRouteStopAsync(routeId, stopId, request, ct));

    [HttpPut("routes/{routeId:guid}/stops/reorder")]
    public async Task<ActionResult<DeliveryRouteDetailDto>> ReorderRouteStops(Guid routeId, [FromBody] ReorderRouteStopsRequest request, CancellationToken ct) =>
        Ok(await deliveryService.ReorderRouteStopsAsync(routeId, request, ct));

    [HttpDelete("routes/{routeId:guid}/stops/{stopId:guid}")]
    public async Task<IActionResult> DeleteRouteStop(Guid routeId, Guid stopId, CancellationToken ct)
    {
        await deliveryService.DeleteRouteStopAsync(routeId, stopId, ct);
        return NoContent();
    }

    [HttpPatch("routes/{routeId:guid}/stops/{stopId:guid}/status")]
    public async Task<ActionResult<DeliveryRouteStopDto>> UpdateStopStatus(Guid routeId, Guid stopId, [FromBody] UpdateStopStatusRequest request, CancellationToken ct) =>
        Ok(await deliveryService.UpdateStopStatusAsync(routeId, stopId, request, ct));
}
