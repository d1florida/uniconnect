using UniConnect.Delivery.DTOs;

namespace UniConnect.Delivery.Interfaces;

public interface IFixedRouteDirectory
{
    Task<IReadOnlyList<DeliveryZoneDto>> GetZonesAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default);
    Task<DeliveryZoneDto> CreateZoneAsync(Guid tenantId, CreateDeliveryZoneRequest request, CancellationToken ct = default);
    Task<DeliveryZoneDto> UpdateZoneAsync(Guid tenantId, Guid zoneId, UpdateDeliveryZoneRequest request, CancellationToken ct = default);
    Task DeleteZoneAsync(Guid tenantId, Guid zoneId, CancellationToken ct = default);

    Task<IReadOnlyList<FixedRouteTemplateDto>> GetTemplatesAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default);
    Task<FixedRouteTemplateDto?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken ct = default);
    Task<FixedRouteTemplateDto> CreateTemplateAsync(Guid tenantId, CreateFixedRouteTemplateRequest request, CancellationToken ct = default);
    Task<FixedRouteTemplateDto> UpdateTemplateAsync(Guid tenantId, Guid templateId, UpdateFixedRouteTemplateRequest request, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid tenantId, Guid templateId, CancellationToken ct = default);

    Task ApplyHoldToOrderAsync(Entities.DeliveryOrder order, Guid customerId, CancellationToken ct = default);
    Task RefreshHoldsForCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    void ClearHoldFromOrder(Entities.DeliveryOrder order);
}
