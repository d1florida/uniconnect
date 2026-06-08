using System.Text.Json;
using UniConnect.RoutePlanning.DTOs;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

public static class PlanProposalSync
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IReadOnlyList<PlannedRouteProposalDto> RefreshFromOrders(
        IReadOnlyList<PlannedRouteProposalDto> proposals,
        IReadOnlyDictionary<Guid, OrderStopSnapshot> orders,
        IReadOnlyDictionary<Guid, CustomerDeliveryWindow?>? deliveryWindows = null)
    {
        if (proposals.Count == 0 || orders.Count == 0)
            return proposals;

        return proposals
            .Select(proposal => proposal with
            {
                Stops = proposal.Stops.Select(stop => RefreshStop(stop, orders, deliveryWindows)).ToList(),
            })
            .ToList();
    }

    public static IReadOnlyList<PlannedRouteProposalDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<PlannedRouteProposalDto>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<PlannedRouteProposalDto> proposals) =>
        JsonSerializer.Serialize(proposals, JsonOptions);

    private static PlannedStopDto RefreshStop(
        PlannedStopDto stop,
        IReadOnlyDictionary<Guid, OrderStopSnapshot> orders,
        IReadOnlyDictionary<Guid, CustomerDeliveryWindow?>? deliveryWindows)
    {
        if (!stop.OrderId.HasValue || !orders.TryGetValue(stop.OrderId.Value, out var order))
            return stop;

        CustomerDeliveryWindow? window = null;
        if (stop.StopType == "Dropoff"
            && deliveryWindows is not null
            && deliveryWindows.TryGetValue(stop.OrderId.Value, out var orderWindow))
            window = orderWindow;

        return stop.StopType switch
        {
            "Pickup" => stop with
            {
                Address = order.PickupAddress,
                RecipientName = order.RecipientName,
                ParcelDescription = order.ParcelDescription,
                Latitude = order.PickupLatitude,
                Longitude = order.PickupLongitude,
            },
            "Dropoff" => stop with
            {
                Address = order.DeliveryAddress,
                RecipientName = order.RecipientName,
                ParcelDescription = order.ParcelDescription,
                Latitude = order.DeliveryLatitude,
                Longitude = order.DeliveryLongitude,
                DeliveryOpenStart = CustomerDeliveryWindow.Format(window?.OpenStart),
                DeliveryOpenEnd = CustomerDeliveryWindow.Format(window?.OpenEnd),
                NoDeliveryStart = CustomerDeliveryWindow.Format(window?.NoDeliveryStart),
                NoDeliveryEnd = CustomerDeliveryWindow.Format(window?.NoDeliveryEnd),
            },
            _ => stop,
        };
    }
}
