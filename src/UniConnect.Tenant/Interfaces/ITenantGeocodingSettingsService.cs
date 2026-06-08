using UniConnect.Tenant.DTOs;

namespace UniConnect.Tenant.Interfaces;

public interface ITenantGeocodingSettingsService
{
    Task<TenantGeocodingSettingsDto> GetMySettingsAsync(CancellationToken ct = default);
    Task<TenantGeocodingSettingsDto> UpdateMySettingsAsync(UpdateTenantGeocodingSettingsRequest request, CancellationToken ct = default);
    Task<TestTenantGeocodingResultDto> TestMyGeocodingAsync(TestTenantGeocodingRequest request, CancellationToken ct = default);
}
