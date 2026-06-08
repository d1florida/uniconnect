using UniConnect.Delivery.DTOs;

namespace UniConnect.Delivery.Interfaces;

public interface IDepotDirectory
{
    Task<IReadOnlyList<DepotDto>> GetDepotsAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default);
    Task<DepotDto?> GetDepotAsync(Guid tenantId, Guid depotId, CancellationToken ct = default);
    Task<DepotDto> CreateDepotAsync(Guid tenantId, CreateDepotRequest request, CancellationToken ct = default);
    Task<DepotDto> UpdateDepotAsync(Guid tenantId, Guid depotId, UpdateDepotRequest request, CancellationToken ct = default);
    Task DeleteDepotAsync(Guid tenantId, Guid depotId, CancellationToken ct = default);
    Task<string> ResolveDepotAddressAsync(Guid tenantId, Guid? depotId, string? depotAddress, CancellationToken ct = default);
}
