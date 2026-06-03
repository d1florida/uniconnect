using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Routing;

namespace UniConnect.RoutePlanning.Interfaces;

public interface IGeocodingService
{
    Task<GeoPoint?> GeocodeAsync(
        string address,
        CancellationToken ct = default,
        bool forceRefresh = false,
        AddressGeocodeHints hints = default);

    Task<IReadOnlyDictionary<string, GeoPoint>> GeocodeManyAsync(IEnumerable<string> addresses, CancellationToken ct = default);
    string NormalizeAddress(string address);
    bool ShouldUpgradeCachedSource(string source, string address);
}
