using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

/// <summary>
/// Groups orders into geographic territories before per-route sequencing to reduce cross-region zig-zags.
/// </summary>
public static class GeographicRouteClusterer
{
    private const int MaxKMeansIterations = 50;

    public static IReadOnlyList<IReadOnlyList<OrderForPlanning>> PartitionForRoutes(
        IReadOnlyList<OrderForPlanning> orders,
        int routeCount,
        GeoPoint depot,
        string depotAddress,
        string balanceObjective,
        int maxOrdersPerRoute,
        int? maxRouteMinutes,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (orders.Count == 0 || routeCount <= 0)
            return [];

        if (routeCount == 1 || orders.Count == 1)
            return [orders];

        var k = Math.Min(routeCount, orders.Count);
        var clusters = KMeansCluster(orders, k);
        EnforceClusterLimits(
            clusters,
            depot,
            depotAddress,
            maxOrdersPerRoute,
            maxRouteMinutes,
            matrix,
            serviceMinutesPerStop);
        RefineClusterBalance(
            clusters,
            depot,
            depotAddress,
            balanceObjective,
            maxOrdersPerRoute,
            maxRouteMinutes,
            matrix,
            serviceMinutesPerStop);

        return clusters
            .Where(c => c.Count > 0)
            .Select(c => (IReadOnlyList<OrderForPlanning>)c)
            .ToList();
    }

    private static List<List<OrderForPlanning>> KMeansCluster(IReadOnlyList<OrderForPlanning> orders, int k)
    {
        var centroids = InitializeCentroids(orders, k);
        var assignments = new int[orders.Count];

        for (var iteration = 0; iteration < MaxKMeansIterations; iteration++)
        {
            var changed = false;
            for (var i = 0; i < orders.Count; i++)
            {
                var nearest = NearestCentroidIndex(orders[i].Delivery, centroids);
                if (assignments[i] != nearest)
                {
                    assignments[i] = nearest;
                    changed = true;
                }
            }

            if (!changed)
                break;

            for (var c = 0; c < k; c++)
            {
                var members = orders.Where((_, i) => assignments[i] == c).ToList();
                centroids[c] = members.Count > 0
                    ? ComputeCentroid(members.Select(o => o.Delivery))
                    : centroids[c];
            }
        }

        var clusters = Enumerable.Range(0, k).Select(_ => new List<OrderForPlanning>()).ToList();
        for (var i = 0; i < orders.Count; i++)
            clusters[assignments[i]].Add(orders[i]);

        return clusters;
    }

    private static List<GeoPoint> InitializeCentroids(IReadOnlyList<OrderForPlanning> orders, int k)
    {
        var centroids = new List<GeoPoint> { orders[0].Delivery };

        while (centroids.Count < k)
        {
            var bestOrder = orders[0];
            var bestDistance = -1.0;

            foreach (var order in orders)
            {
                var minDist = centroids.Min(c => SquaredDistance(order.Delivery, c));
                if (minDist > bestDistance)
                {
                    bestDistance = minDist;
                    bestOrder = order;
                }
            }

            centroids.Add(bestOrder.Delivery);
        }

        return centroids;
    }

    private static void EnforceClusterLimits(
        List<List<OrderForPlanning>> clusters,
        GeoPoint depot,
        string depotAddress,
        int maxOrdersPerRoute,
        int? maxRouteMinutes,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < clusters.Count; i++)
            {
                while (ClusterNeedsRelief(clusters[i], depot, depotAddress, maxOrdersPerRoute, maxRouteMinutes, matrix, serviceMinutesPerStop))
                {
                    var order = SelectBorderOrder(clusters[i], clusters, i);
                    if (order is null)
                        break;

                    var target = NearestClusterWithCapacity(
                        order, clusters, i, depot, depotAddress, maxOrdersPerRoute, maxRouteMinutes, matrix, serviceMinutesPerStop);
                    if (target < 0)
                        break;

                    clusters[i].Remove(order);
                    clusters[target].Add(order);
                    changed = true;
                }
            }
        }
    }

    private static bool ClusterNeedsRelief(
        IReadOnlyList<OrderForPlanning> cluster,
        GeoPoint depot,
        string depotAddress,
        int maxOrdersPerRoute,
        int? maxRouteMinutes,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (cluster.Count > maxOrdersPerRoute)
            return true;

        if (!maxRouteMinutes.HasValue || cluster.Count == 0)
            return false;

        return EstimateClusterMinutes(cluster, depot, depotAddress, matrix, serviceMinutesPerStop) > maxRouteMinutes.Value;
    }

    private static void RefineClusterBalance(
        List<List<OrderForPlanning>> clusters,
        GeoPoint depot,
        string depotAddress,
        string balanceObjective,
        int maxOrdersPerRoute,
        int? maxRouteMinutes,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (clusters.Count <= 1)
            return;

        var useDriveMinutes = string.Equals(balanceObjective, "driveMinutes", StringComparison.OrdinalIgnoreCase);
        var loads = clusters
            .Select(c => useDriveMinutes
                ? EstimateClusterMinutes(c, depot, depotAddress, matrix, serviceMinutesPerStop)
                : c.Count)
            .ToArray();

        for (var pass = 0; pass < clusters.Count * 4; pass++)
        {
            var heavy = Array.IndexOf(loads, loads.Max());
            var light = Array.IndexOf(loads, loads.Min());
            if (heavy == light || clusters[heavy].Count <= 1)
                break;

            var imbalance = loads[heavy] - loads[light];
            if (imbalance <= Math.Max(1, loads[heavy] * 0.15))
                break;

            var order = SelectBorderOrder(clusters[heavy], clusters, heavy);
            if (order is null)
                break;

            var trial = clusters[light].Append(order).ToList();
            if (trial.Count > maxOrdersPerRoute)
                break;
            if (maxRouteMinutes.HasValue &&
                EstimateClusterMinutes(trial, depot, depotAddress, matrix, serviceMinutesPerStop) > maxRouteMinutes.Value)
                break;

            var weight = useDriveMinutes
                ? EstimateOrderMinutes(order, depot, depotAddress, matrix, serviceMinutesPerStop)
                : 1;

            clusters[heavy].Remove(order);
            clusters[light].Add(order);
            loads[heavy] -= weight;
            loads[light] += weight;
        }
    }

    private static OrderForPlanning? SelectBorderOrder(
        List<OrderForPlanning> cluster,
        IReadOnlyList<List<OrderForPlanning>> allClusters,
        int clusterIndex)
    {
        if (cluster.Count == 0)
            return null;

        var ownCentroid = ComputeCentroid(cluster.Select(o => o.Delivery));
        var otherCentroids = allClusters
            .Select((c, i) => i == clusterIndex || c.Count == 0 ? (GeoPoint?)null : ComputeCentroid(c.Select(o => o.Delivery)))
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();

        if (otherCentroids.Count == 0)
            return cluster[^1];

        OrderForPlanning? best = null;
        var bestScore = double.MinValue;

        foreach (var order in cluster)
        {
            var distOwn = SquaredDistance(order.Delivery, ownCentroid);
            var distOther = otherCentroids.Min(c => SquaredDistance(order.Delivery, c));
            var score = distOwn - distOther;
            if (score > bestScore)
            {
                bestScore = score;
                best = order;
            }
        }

        return best;
    }

    private static int NearestClusterWithCapacity(
        OrderForPlanning order,
        IReadOnlyList<List<OrderForPlanning>> clusters,
        int excludeIndex,
        GeoPoint depot,
        string depotAddress,
        int maxOrdersPerRoute,
        int? maxRouteMinutes,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        var best = -1;
        var bestDist = double.MaxValue;

        for (var i = 0; i < clusters.Count; i++)
        {
            if (i == excludeIndex)
                continue;

            var trial = clusters[i].Append(order).ToList();
            if (trial.Count > maxOrdersPerRoute)
                continue;
            if (maxRouteMinutes.HasValue &&
                EstimateClusterMinutes(trial, depot, depotAddress, matrix, serviceMinutesPerStop) > maxRouteMinutes.Value)
                continue;

            var centroid = ComputeCentroid(clusters[i].Select(o => o.Delivery).Append(order.Delivery));
            var dist = SquaredDistance(order.Delivery, centroid);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    private static int NearestCentroidIndex(GeoPoint point, IReadOnlyList<GeoPoint> centroids)
    {
        var best = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < centroids.Count; i++)
        {
            var dist = SquaredDistance(point, centroids[i]);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    private static GeoPoint ComputeCentroid(IEnumerable<GeoPoint> points)
    {
        var list = points.ToList();
        if (list.Count == 0)
            return default;

        var lat = list.Average(p => (double)p.Latitude);
        var lon = list.Average(p => (double)p.Longitude);
        return new GeoPoint((decimal)lat, (decimal)lon);
    }

    private static double SquaredDistance(GeoPoint a, GeoPoint b)
    {
        var dLat = (double)(a.Latitude - b.Latitude);
        var dLon = (double)(a.Longitude - b.Longitude);
        return dLat * dLat + dLon * dLon;
    }

    private static int EstimateClusterMinutes(
        IReadOnlyList<OrderForPlanning> orders,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (orders.Count == 0)
            return 0;

        var stops = PickupDeliveryRouteOptimizer.BuildPlanningStops(orders, depot, depotAddress);
        return PickupDeliveryRouteOptimizer.EstimateRouteMinutes(depot, stops, matrix, serviceMinutesPerStop);
    }

    private static int EstimateOrderMinutes(
        OrderForPlanning order,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        var depotPickup = PickupDeliveryRouteOptimizer.IsPickupAtDepot(order, depot, depotAddress);
        var toFirst = matrix.TravelMinutes(depot, depotPickup ? order.Delivery : order.Pickup);
        var within = depotPickup ? 0 : matrix.TravelMinutes(order.Pickup, order.Delivery);
        var toDepot = matrix.TravelMinutes(order.Delivery, depot);
        var service = (depotPickup ? 1 : 2) * serviceMinutesPerStop;
        return toFirst + within + toDepot + service;
    }
}
