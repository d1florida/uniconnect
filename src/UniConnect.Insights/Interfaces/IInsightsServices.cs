using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;

namespace UniConnect.Insights.Interfaces;

public interface IOperationalEventRecorder
{
    Task RecordAsync(RecordOperationalEventRequest request, CancellationToken ct = default);
}

public interface ICustomerDirectory
{
    Task<Customer> GetOrCreateAsync(Guid tenantId, string name, string? phone, CancellationToken ct = default);
}

public interface IDriverDirectory
{
    Task<IReadOnlyList<DriverDto>> GetDriversAsync(Guid tenantId, CancellationToken ct = default);
    Task<DriverDto?> GetDriverAsync(Guid tenantId, Guid driverId, CancellationToken ct = default);
    Task<DriverDto> CreateDriverAsync(Guid tenantId, CreateDriverRequest request, CancellationToken ct = default);
    Task<DriverDto> UpdateDriverAsync(Guid tenantId, Guid driverId, UpdateDriverRequest request, CancellationToken ct = default);
    Task DeleteDriverAsync(Guid tenantId, Guid driverId, CancellationToken ct = default);
}

public interface IInsightsService
{
    Task<TenantDigestDto> GetTenantDigestAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DriverDto>> GetDriversAsync(Guid tenantId, CancellationToken ct = default);
    Task<SubjectSummaryDto?> GetDriverSummaryAsync(Guid tenantId, Guid driverId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<SubjectSummaryDto?> GetVehicleSummaryAsync(Guid tenantId, Guid vehicleId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<SubjectSummaryDto?> GetCustomerSummaryAsync(Guid tenantId, Guid customerId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<PlannerSummaryDto?> GetPlannerSummaryAsync(Guid tenantId, Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<PlannerSummaryDto>> GetPlannersAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<AnalyticsReportBundleDto?> GetReportAsync(string reportType, Guid tenantId, Guid subjectId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<OperationalEventDto>> GetEventsAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        string? domain = null,
        Guid? driverId = null,
        Guid? vehicleId = null,
        Guid? customerId = null,
        Guid? userId = null,
        CancellationToken ct = default);
}
