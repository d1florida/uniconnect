using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Interfaces;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Interfaces;

namespace UniConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/delivery")]
[Tags("Delivery")]
public class DeliveryController(IDeliveryService deliveryService, IDriverDirectory drivers, IDepotDirectory depotDirectory) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DeliveryDashboardDto>> GetDashboard(CancellationToken ct) =>
        Ok(await deliveryService.GetDashboardAsync(ct));

    [HttpGet("tenants/{tenantId:guid}/orders")]
    public async Task<ActionResult<IReadOnlyList<DeliveryOrderDto>>> GetOrders(Guid tenantId, CancellationToken ct) =>
        Ok(await deliveryService.GetOrdersAsync(tenantId, ct));

    [HttpPost("tenants/{tenantId:guid}/orders")]
    public async Task<ActionResult<DeliveryOrderDto>> CreateOrder(Guid tenantId, [FromBody] CreateDeliveryOrderRequest request, CancellationToken ct) =>
        Ok(await deliveryService.CreateOrderAsync(tenantId, request, ct));

    [HttpGet("tenants/{tenantId:guid}/vehicles")]
    public async Task<ActionResult<IReadOnlyList<DeliveryVehicleDto>>> GetVehicles(
        Guid tenantId,
        [FromQuery] Guid? depotId,
        CancellationToken ct) =>
        Ok(await deliveryService.GetVehiclesAsync(tenantId, depotId, ct));

    [HttpPatch("tenants/{tenantId:guid}/vehicles/{vehicleId:guid}/home-depot")]
    public async Task<ActionResult<DeliveryVehicleDto>> AssignVehicleHomeDepot(
        Guid tenantId,
        Guid vehicleId,
        [FromBody] AssignVehicleHomeDepotRequest request,
        CancellationToken ct) =>
        Ok(await deliveryService.AssignVehicleHomeDepotAsync(tenantId, vehicleId, request, ct));

    [HttpGet("tenants/{tenantId:guid}/tracking")]
    public async Task<ActionResult<IReadOnlyList<DeliveryTrackingDto>>> GetTracking(Guid tenantId, CancellationToken ct) =>
        Ok(await deliveryService.GetTrackingAsync(tenantId, ct));

    [HttpGet("orders/{orderId:guid}")]
    public async Task<ActionResult<DeliveryOrderDto>> GetOrder(Guid orderId, CancellationToken ct)
    {
        var order = await deliveryService.GetOrderAsync(orderId, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPut("orders/{orderId:guid}")]
    public async Task<ActionResult<DeliveryOrderDto>> UpdateOrder(Guid orderId, [FromBody] UpdateDeliveryOrderRequest request, CancellationToken ct) =>
        Ok(await deliveryService.UpdateOrderAsync(orderId, request, ct));

    [HttpPatch("orders/{orderId:guid}/status")]
    public async Task<ActionResult<DeliveryOrderDto>> UpdateStatus(Guid orderId, [FromBody] UpdateDeliveryStatusRequest request, CancellationToken ct) =>
        Ok(await deliveryService.UpdateStatusAsync(orderId, request, ct));

    [HttpPost("orders/{orderId:guid}/assign")]
    public async Task<ActionResult<DeliveryOrderDto>> Assign(Guid orderId, [FromBody] AssignDeliveryRequest request, CancellationToken ct) =>
        Ok(await deliveryService.AssignAsync(orderId, request, ct));

    [HttpGet("tenants/{tenantId:guid}/routes")]
    public async Task<ActionResult<IReadOnlyList<DeliveryRouteDto>>> GetRoutes(Guid tenantId, CancellationToken ct) =>
        Ok(await deliveryService.GetRoutesAsync(tenantId, ct));

    [HttpPost("tenants/{tenantId:guid}/routes")]
    public async Task<ActionResult<DeliveryRouteDetailDto>> CreateRoute(Guid tenantId, [FromBody] CreateDeliveryRouteRequest request, CancellationToken ct) =>
        Ok(await deliveryService.CreateRouteAsync(tenantId, request, ct));

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

    [HttpGet("tenants/{tenantId:guid}/drivers")]
    public async Task<ActionResult<IReadOnlyList<DriverDto>>> GetDrivers(Guid tenantId, CancellationToken ct) =>
        Ok(await drivers.GetDriversAsync(tenantId, ct));

    [HttpPost("tenants/{tenantId:guid}/drivers")]
    public async Task<ActionResult<DriverDto>> CreateDriver(Guid tenantId, [FromBody] CreateDriverRequest request, CancellationToken ct) =>
        Ok(await drivers.CreateDriverAsync(tenantId, request, ct));

    [HttpPatch("tenants/{tenantId:guid}/drivers/{driverId:guid}")]
    public async Task<ActionResult<DriverDto>> UpdateDriver(Guid tenantId, Guid driverId, [FromBody] UpdateDriverRequest request, CancellationToken ct) =>
        Ok(await drivers.UpdateDriverAsync(tenantId, driverId, request, ct));

    [HttpDelete("tenants/{tenantId:guid}/drivers/{driverId:guid}")]
    public async Task<IActionResult> DeleteDriver(Guid tenantId, Guid driverId, CancellationToken ct)
    {
        await drivers.DeleteDriverAsync(tenantId, driverId, ct);
        return NoContent();
    }

    [HttpGet("tenants/{tenantId:guid}/depots")]
    public async Task<ActionResult<IReadOnlyList<DepotDto>>> GetDepots(Guid tenantId, [FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        Ok(await depotDirectory.GetDepotsAsync(tenantId, includeInactive, ct));

    [HttpPost("tenants/{tenantId:guid}/depots")]
    public async Task<ActionResult<DepotDto>> CreateDepot(Guid tenantId, [FromBody] CreateDepotRequest request, CancellationToken ct) =>
        Ok(await depotDirectory.CreateDepotAsync(tenantId, request, ct));

    [HttpPatch("tenants/{tenantId:guid}/depots/{depotId:guid}")]
    public async Task<ActionResult<DepotDto>> UpdateDepot(Guid tenantId, Guid depotId, [FromBody] UpdateDepotRequest request, CancellationToken ct) =>
        Ok(await depotDirectory.UpdateDepotAsync(tenantId, depotId, request, ct));

    [HttpDelete("tenants/{tenantId:guid}/depots/{depotId:guid}")]
    public async Task<IActionResult> DeleteDepot(Guid tenantId, Guid depotId, CancellationToken ct)
    {
        await depotDirectory.DeleteDepotAsync(tenantId, depotId, ct);
        return NoContent();
    }
}
