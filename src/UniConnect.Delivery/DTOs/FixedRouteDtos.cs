namespace UniConnect.Delivery.DTOs;



public record DeliveryZoneDto(

    Guid Id,

    Guid TenantId,

    string Name,

    string MatchType,

    bool IsActive,

    int CustomerCount,

    DateTime CreatedAt);



public record CreateDeliveryZoneRequest(string Name);



public record UpdateDeliveryZoneRequest(string Name, bool IsActive);



public record FixedRouteTemplateDto(

    Guid Id,

    Guid TenantId,

    string Name,

    Guid DeliveryZoneId,

    string DeliveryZoneName,

    IReadOnlyList<string> DaysOfWeek,

    string DaysOfWeekLabel,

    Guid DepotId,

    string? DepotName,

    Guid? DefaultVehicleId,

    Guid? DefaultDriverId,

    bool IsActive,

    int HeldOrderCount,

    int DueOrderCount,

    string? NextRouteDate,

    DateTime CreatedAt);



public record CreateFixedRouteTemplateRequest(

    string Name,

    Guid DeliveryZoneId,

    IReadOnlyList<DayOfWeek> DaysOfWeek,

    Guid DepotId,

    Guid? DefaultVehicleId = null,

    Guid? DefaultDriverId = null);



public record UpdateFixedRouteTemplateRequest(

    string Name,

    IReadOnlyList<DayOfWeek> DaysOfWeek,

    Guid DepotId,

    Guid? DefaultVehicleId,

    Guid? DefaultDriverId,

    bool IsActive);

