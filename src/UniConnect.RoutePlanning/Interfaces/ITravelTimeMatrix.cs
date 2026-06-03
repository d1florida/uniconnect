using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Interfaces;

public interface ITravelTimeMatrix
{
    int TravelMinutes(GeoPoint from, GeoPoint to);
    double TravelKilometers(GeoPoint from, GeoPoint to);
}
