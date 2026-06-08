namespace UniConnect.Tenant.DTOs;

public record TenantDeliverySettingsDto(
    bool AllowMultipleRoutesPerDriverPerDay,
    string TimeZoneId,
    DateTime? UpdatedAt);

public record UpdateTenantDeliverySettingsRequest(
    bool AllowMultipleRoutesPerDriverPerDay,
    string? TimeZoneId = null);
