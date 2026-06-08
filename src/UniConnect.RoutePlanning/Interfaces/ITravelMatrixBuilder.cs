using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Interfaces;

public interface ITravelMatrixBuilder
{
    Task<ITravelTimeMatrix> BuildAsync(IReadOnlyList<GeoPoint> points, CancellationToken ct = default);
}
