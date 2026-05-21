using UniConnect.Application.DTOs;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Enums;

namespace UniConnect.Delivery.Interfaces;

public interface IDeliveryService
{
    Task<DeliveryDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FleetDto>> GetFleetsAsync(CancellationToken ct = default);
    Task<FleetDto> CreateFleetAsync(string name, string slug, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessAccountDto>> GetBusinessAccountsAsync(Guid fleetId, CancellationToken ct = default);
    Task<BusinessAccountDto> CreateBusinessAccountAsync(Guid fleetId, CreateBusinessAccountRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryOrderDto>> GetOrdersAsync(Guid fleetId, DeliveryChannel? channel, CancellationToken ct = default);
    Task<DeliveryOrderDto?> GetOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<DeliveryOrderDto> CreateOrderAsync(Guid fleetId, CreateDeliveryOrderRequest request, CancellationToken ct = default);
    Task<DeliveryOrderDto> UpdateStatusAsync(Guid orderId, UpdateDeliveryStatusRequest request, CancellationToken ct = default);
    Task<DeliveryOrderDto> AssignAsync(Guid orderId, AssignDeliveryRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryVehicleDto>> GetVehiclesAsync(Guid fleetId, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryTrackingDto>> GetTrackingAsync(Guid fleetId, CancellationToken ct = default);

    Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesAsync(Guid fleetId, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto?> GetRouteAsync(Guid routeId, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> CreateRouteAsync(Guid fleetId, CreateDeliveryRouteRequest request, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> UpdateRouteAsync(Guid routeId, UpdateDeliveryRouteRequest request, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> UpdateRouteStatusAsync(Guid routeId, UpdateRouteStatusRequest request, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> AssignRouteAsync(Guid routeId, AssignRouteRequest request, CancellationToken ct = default);
    Task<DeliveryRouteStopDto> AddRouteStopAsync(Guid routeId, AddRouteStopRequest request, CancellationToken ct = default);
    Task<DeliveryRouteStopDto> UpdateRouteStopAsync(Guid routeId, Guid stopId, UpdateRouteStopRequest request, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> ReorderRouteStopsAsync(Guid routeId, ReorderRouteStopsRequest request, CancellationToken ct = default);
    Task DeleteRouteStopAsync(Guid routeId, Guid stopId, CancellationToken ct = default);
    Task<DeliveryRouteStopDto> UpdateStopStatusAsync(Guid routeId, Guid stopId, UpdateStopStatusRequest request, CancellationToken ct = default);
}
