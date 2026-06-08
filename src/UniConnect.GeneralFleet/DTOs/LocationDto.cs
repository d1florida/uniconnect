namespace UniConnect.GeneralFleet.DTOs;

public record LocationDto(
    decimal Latitude,
    decimal Longitude,
    DateTime RecordedAt,
    decimal? SpeedKph);
