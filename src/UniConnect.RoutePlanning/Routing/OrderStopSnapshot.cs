namespace UniConnect.RoutePlanning.Routing;

public readonly record struct OrderStopSnapshot(
    Guid Id,
    string PickupAddress,
    string DeliveryAddress,
    string RecipientName,
    string ParcelDescription,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    decimal? DeliveryLatitude,
    decimal? DeliveryLongitude);
