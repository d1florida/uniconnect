namespace UniConnect.RoutePlanning.Options;

public class RoutePlanningOptions
{
    public const string SectionName = "RoutePlanning";

    /// <summary>Deterministic (offline demo) or Nominatim (OpenStreetMap).</summary>
    public string GeocodingProvider { get; set; } = "Deterministic";

    /// <summary>Haversine (offline) or Osrm (driving durations).</summary>
    public string TravelTimeProvider { get; set; } = "Haversine";

    public string OsrmBaseUrl { get; set; } = "https://router.project-osrm.org";

    public double AverageSpeedKph { get; set; } = 35;

    public double RoadDistanceFactor { get; set; } = 1.35;

    public int DefaultServiceMinutes { get; set; } = 5;

    public string NominatimUserAgent { get; set; } = "UniConnect/1.0 (route-planning)";

    /// <summary>When true, US addresses also use the Census Bureau geocoder before postal fallback.</summary>
    public bool EnableUsCensusGeocoder { get; set; } = true;
}
