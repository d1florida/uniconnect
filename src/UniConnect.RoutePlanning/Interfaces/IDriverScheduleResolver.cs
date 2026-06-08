using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Interfaces;

public interface IDriverScheduleResolver
{
    Task<IReadOnlyDictionary<Guid, ResolvedDriverDay>> ResolveAsync(
        Guid tenantId,
        DateOnly date,
        IReadOnlyList<Guid> driverIds,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateOnly, ResolvedDriverDay>>> ResolveRangeAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid> driverIds,
        CancellationToken ct = default);
}
