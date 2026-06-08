using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;

namespace UniConnect.Insights.Interfaces;

public interface IOperationalEventRecorder
{
    Task RecordAsync(RecordOperationalEventRequest request, CancellationToken ct = default);
}

public interface ICustomerDirectory
{
    Task<Customer> GetOrCreateAsync(Guid tenantId, string name, string? phone, string? deliveryAddress = null, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default);
    Task<CustomerDto?> GetCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    Task<CustomerDto> CreateCustomerAsync(Guid tenantId, CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerDto> UpdateCustomerAsync(Guid tenantId, Guid customerId, UpdateCustomerRequest request, CancellationToken ct = default);
    Task DeleteCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    Task SyncDeliveryAddressAsync(Guid tenantId, Guid customerId, string deliveryAddress, CancellationToken ct = default);
    Task SyncExternalRefAsync(Guid tenantId, Guid customerId, string? externalRef, CancellationToken ct = default);
}

public interface IDriverDirectory
{
    Task<IReadOnlyList<DriverDto>> GetDriversAsync(Guid tenantId, CancellationToken ct = default);
    Task<DriverDto?> GetDriverAsync(Guid tenantId, Guid driverId, CancellationToken ct = default);
    Task<DriverDto> CreateDriverAsync(Guid tenantId, CreateDriverRequest request, CancellationToken ct = default);
    Task<DriverDto> UpdateDriverAsync(Guid tenantId, Guid driverId, UpdateDriverRequest request, CancellationToken ct = default);
    Task DeleteDriverAsync(Guid tenantId, Guid driverId, CancellationToken ct = default);
    Task<IReadOnlyList<DriverWorkPatternDayDto>> GetWorkPatternAsync(Guid tenantId, Guid driverId, CancellationToken ct = default);
    Task<IReadOnlyList<DriverWorkPatternDayDto>> UpdateWorkPatternAsync(
        Guid tenantId,
        Guid driverId,
        IReadOnlyList<UpdateDriverWorkPatternDayRequest> pattern,
        CancellationToken ct = default);
    Task<IReadOnlyList<ResolvedDriverScheduleDto>> GetResolvedScheduleAsync(
        Guid tenantId,
        DateOnly date,
        CancellationToken ct = default);

    Task<DriverCalendarDto> GetDriverCalendarAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    Task<DriverScheduleExceptionDto> UpsertScheduleExceptionAsync(
        Guid tenantId,
        Guid driverId,
        UpsertDriverScheduleExceptionRequest request,
        CancellationToken ct = default);

    Task DeleteScheduleExceptionAsync(
        Guid tenantId,
        Guid driverId,
        DateOnly date,
        CancellationToken ct = default);

    Task<IReadOnlyList<DriverScheduleExceptionDto>> BulkUpsertScheduleExceptionsAsync(
        Guid tenantId,
        Guid driverId,
        BulkUpsertDriverScheduleExceptionRequest request,
        CancellationToken ct = default);
}

public interface IDriverScheduleRequestService
{
    Task<IReadOnlyList<DriverScheduleRequestDto>> GetRequestsAsync(
        Guid tenantId,
        string? status = null,
        CancellationToken ct = default);

    Task<DriverScheduleRequestDto> CreateRequestAsync(
        Guid tenantId,
        CreateDriverScheduleRequestRequest request,
        CancellationToken ct = default);

    Task<DriverScheduleRequestDto> ApproveRequestAsync(
        Guid tenantId,
        Guid requestId,
        ReviewDriverScheduleRequestRequest? review = null,
        CancellationToken ct = default);

    Task<DriverScheduleRequestDto> DenyRequestAsync(
        Guid tenantId,
        Guid requestId,
        ReviewDriverScheduleRequestRequest? review = null,
        CancellationToken ct = default);

    Task CancelRequestAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default);
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
