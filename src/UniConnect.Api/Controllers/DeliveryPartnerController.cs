using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Interfaces;
using UniConnect.Infrastructure.Auth;

namespace UniConnect.Api.Controllers;

[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
[ApiController]
[Route("api/delivery/partner")]
[Tags("Delivery — Partner API")]
public class DeliveryPartnerController(IDeliveryService deliveryService) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<DeliveryOrderDto>>> GetOrders(CancellationToken ct) =>
        Ok(await deliveryService.GetPartnerOrdersAsync(ct));

    [HttpPost("orders")]
    public async Task<ActionResult<DeliveryOrderDto>> CreateOrder([FromBody] CreateDeliveryOrderRequest request, CancellationToken ct) =>
        Ok(await deliveryService.CreatePartnerOrderAsync(request, ct));

    [HttpGet("orders/{orderId:guid}")]
    public async Task<ActionResult<DeliveryOrderDto>> GetOrder(Guid orderId, CancellationToken ct)
    {
        var order = await deliveryService.GetPartnerOrderAsync(orderId, ct);
        return order is null ? NotFound() : Ok(order);
    }
}
