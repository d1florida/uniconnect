namespace UniConnect.Tenant.Enums;

/// <summary>
/// Product modules enabled for a tenant. Flags can be combined (e.g. General | Delivery).
/// </summary>
[Flags]
public enum ProductModule
{
    None = 0,
    General = 1,
    RoboTaxi = 2,
    Delivery = 4,
    RoutePlanning = 8,
    Insights = 16
}
