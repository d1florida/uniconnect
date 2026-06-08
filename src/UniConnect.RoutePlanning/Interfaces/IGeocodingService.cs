using UniConnect.RoutePlanning.Models;

using UniConnect.RoutePlanning.Routing;



namespace UniConnect.RoutePlanning.Interfaces;



public interface IGeocodingService

{

    Task<GeocodeResult?> GeocodeAsync(

        string address,

        CancellationToken ct = default,

        bool forceRefresh = false,

        AddressGeocodeHints hints = default,

        Guid? tenantId = null);



    Task<IReadOnlyDictionary<string, GeocodeResult>> GeocodeManyAsync(

        IEnumerable<string> addresses,

        CancellationToken ct = default,

        Guid? tenantId = null);



    string NormalizeAddress(string address);



    Task<bool> ShouldUpgradeCachedSourceAsync(

        string source,

        string address,

        Guid? tenantId = null,

        CancellationToken ct = default);

    /// <summary>Provider error from the most recent failed geocode in this request scope.</summary>
    string? GetLastErrorMessage();
}

