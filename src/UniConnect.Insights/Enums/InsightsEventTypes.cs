namespace UniConnect.Insights.Enums;

public static class InsightsDomains
{
    public const string Delivery = "delivery";
    public const string RoutePlanning = "route_planning";
}

public static class DeliveryEventTypes
{
    public const string OrderCreated = "delivery.order.created";
    public const string OrderAssigned = "delivery.order.assigned";
    public const string OrderStatusChanged = "delivery.order.status_changed";
    public const string OrderDelivered = "delivery.order.delivered";
    public const string OrderFailed = "delivery.order.failed";
    public const string RouteCreated = "delivery.route.created";
    public const string RouteAssigned = "delivery.route.assigned";
    public const string RouteStarted = "delivery.route.started";
    public const string RouteCompleted = "delivery.route.completed";
    public const string StopCompleted = "delivery.stop.completed";
}

public static class RoutePlanningEventTypes
{
    public const string PlanRequested = "route_planning.plan.requested";
    public const string PlanCompleted = "route_planning.plan.completed";
    public const string PlanAccepted = "route_planning.plan.accepted";
    public const string PlanDiscarded = "route_planning.plan.discarded";
    public const string SequenceOptimized = "route_planning.sequence.optimized";
}
