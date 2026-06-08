using UniConnect.Delivery.DTOs;
using UniConnect.GeneralFleet.DTOs;

namespace UniConnect.Delivery.Interfaces;

public interface IDeliveryService
{
    Task<DeliveryDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryOrderDto>> GetOrdersAsync(Guid tenantId, CancellationToken ct = default);
    Task<DeliveryOrderDto?> GetOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<DeliveryOrderDto> CreateOrderAsync(Guid tenantId, CreateDeliveryOrderRequest request, CancellationToken ct = default);
    Task<DeliveryOrderDto> UpdateOrderAsync(Guid orderId, UpdateDeliveryOrderRequest request, CancellationToken ct = default);
    Task<DeliveryOrderDto> UpdateStatusAsync(Guid orderId, UpdateDeliveryStatusRequest request, CancellationToken ct = default);
    Task<DeliveryOrderDto> AssignAsync(Guid orderId, AssignDeliveryRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryVehicleDto>> GetVehiclesAsync(Guid tenantId, Guid? depotId = null, CancellationToken ct = default);
    Task<DeliveryVehicleDto> AssignVehicleHomeDepotAsync(Guid tenantId, Guid vehicleId, AssignVehicleHomeDepotRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryTrackingDto>> GetTrackingAsync(Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesAsync(Guid tenantId, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto?> GetRouteAsync(Guid routeId, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> CreateRouteAsync(Guid tenantId, CreateDeliveryRouteRequest request, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> UpdateRouteAsync(Guid routeId, UpdateDeliveryRouteRequest request, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> UpdateRouteStatusAsync(Guid routeId, UpdateRouteStatusRequest request, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> AssignRouteAsync(Guid routeId, AssignRouteRequest request, CancellationToken ct = default);
    Task<DeliveryRouteStopDto> AddRouteStopAsync(Guid routeId, AddRouteStopRequest request, CancellationToken ct = default);
    Task<DeliveryRouteStopDto> UpdateRouteStopAsync(Guid routeId, Guid stopId, UpdateRouteStopRequest request, CancellationToken ct = default);
    Task<DeliveryRouteDetailDto> ReorderRouteStopsAsync(Guid routeId, ReorderRouteStopsRequest request, CancellationToken ct = default);
    Task DeleteRouteStopAsync(Guid routeId, Guid stopId, CancellationToken ct = default);
    Task<DeliveryRouteStopDto> UpdateStopStatusAsync(Guid routeId, Guid stopId, UpdateStopStatusRequest request, CancellationToken ct = default);
    Task SyncRouteOrderAssignmentsAsync(Guid routeId, CancellationToken ct = default);

    Task<IReadOnlyList<DeliveryOrderDto>> GetPartnerOrdersAsync(CancellationToken ct = default);
    Task<DeliveryOrderDto?> GetPartnerOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<DeliveryOrderDto> CreatePartnerOrderAsync(CreateDeliveryOrderRequest request, CancellationToken ct = default);
}
