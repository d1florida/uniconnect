using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

public static class DepotPickupMatcher
{
    private const double MaxDepotPickupKm = 0.25;

    public static string NormalizeAddress(string address) =>
        address.Trim().ToLowerInvariant();

    public static bool IsPickupAtDepot(
        string pickupAddress,
        GeoPoint pickup,
        GeoPoint depot,
        string depotAddress) =>
        IsPickupAtDepot(pickupAddress, (GeoPoint?)pickup, depot, depotAddress);

    public static bool IsPickupAtDepot(
        string pickupAddress,
        GeoPoint? pickup,
        GeoPoint depot,
        string depotAddress)
    {
        if (string.IsNullOrWhiteSpace(pickupAddress) || string.IsNullOrWhiteSpace(depotAddress))
            return false;

        if (NormalizeAddress(pickupAddress) == NormalizeAddress(depotAddress))
            return true;

        if (pickup.HasValue && GeoMath.HaversineKilometers(pickup.Value, depot) <= MaxDepotPickupKm)
            return true;

        return false;
    }
}
