namespace UniConnect.RoutePlanning.Entities;

public class GeocodedAddress
{
    public string NormalizedAddress { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? FormattedAddress { get; set; }
    public DateTime GeocodedAt { get; set; }
}
