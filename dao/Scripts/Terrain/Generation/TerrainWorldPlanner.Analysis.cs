using System;
using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static TerrainWorldPlanningReport AnalyzePlanning(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldRoute[] routes,
        float worldSize)
    {
        Span<int> poiCounts = stackalloc int[Enum.GetValues<TerrainPointOfInterestKind>().Length];
        Span<int> routeCounts = stackalloc int[Enum.GetValues<TerrainRouteKind>().Length];
        Span<int> settlementTierCounts = stackalloc int[Enum.GetValues<TerrainSettlementTier>().Length];
        float scoreSum = 0.0f;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            poiCounts[Mathf.Clamp((int)point.Kind, 0, poiCounts.Length - 1)]++;
            settlementTierCounts[Mathf.Clamp((int)point.SettlementTier, 0, settlementTierCounts.Length - 1)]++;
            scoreSum += point.Score;
        }

        float routeCostSum = 0.0f;
        float routeScenicSum = 0.0f;
        float routeTraversabilitySum = 0.0f;
        var connected = new HashSet<int>();
        var connectedSettlements = new HashSet<int>();
        int settlementPointCount = 0;
        int settlementRouteCount = 0;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            if (IsSettlementHub(point))
            {
                settlementPointCount++;
            }
        }

        foreach (TerrainWorldRoute route in routes)
        {
            routeCounts[Mathf.Clamp((int)route.Kind, 0, routeCounts.Length - 1)]++;
            routeCostSum += route.Cost;
            routeScenicSum += route.AverageScenicPotential;
            routeTraversabilitySum += route.AverageTraversability;
            connected.Add(route.FromPointId);
            connected.Add(route.ToPointId);

            bool fromSettlement = IsSettlementHub(points, route.FromPointId);
            bool toSettlement = IsSettlementHub(points, route.ToPointId);
            if (fromSettlement)
            {
                connectedSettlements.Add(route.FromPointId);
            }

            if (toSettlement)
            {
                connectedSettlements.Add(route.ToPointId);
            }

            if (fromSettlement && toSettlement)
            {
                settlementRouteCount++;
            }
        }

        int distinctPoiKinds = CountNonZero(poiCounts);
        int distinctRouteKinds = CountNonZero(routeCounts);
        float invPoiCount = points.Length == 0 ? 0.0f : 1.0f / points.Length;
        float invRouteCount = routes.Length == 0 ? 0.0f : 1.0f / routes.Length;

        return new TerrainWorldPlanningReport(
            points.Length,
            distinctPoiKinds,
            routes.Length,
            distinctRouteKinds,
            points.Length == 0 ? 0.0f : connected.Count / (float)points.Length,
            settlementPointCount == 0 ? 0.0f : connectedSettlements.Count / (float)settlementPointCount,
            settlementRouteCount,
            ComputePointCoverage(points, worldSize),
            ComputeRouteCoverage(routes, worldSize),
            scoreSum * invPoiCount,
            routeCostSum * invRouteCount,
            routeScenicSum * invRouteCount,
            routeTraversabilitySum * invRouteCount,
            poiCounts[(int)TerrainPointOfInterestKind.SettlementCandidate],
            poiCounts[(int)TerrainPointOfInterestKind.Vista],
            poiCounts[(int)TerrainPointOfInterestKind.RiverCrossing],
            poiCounts[(int)TerrainPointOfInterestKind.MountainPass],
            poiCounts[(int)TerrainPointOfInterestKind.CoastalLanding],
            poiCounts[(int)TerrainPointOfInterestKind.ResourceGrove],
            poiCounts[(int)TerrainPointOfInterestKind.AncientSite],
            poiCounts[(int)TerrainPointOfInterestKind.CanyonOverlook],
            poiCounts[(int)TerrainPointOfInterestKind.Oasis],
            settlementTierCounts[(int)TerrainSettlementTier.Village],
            settlementTierCounts[(int)TerrainSettlementTier.Town],
            settlementTierCounts[(int)TerrainSettlementTier.OasisHub],
            routeCounts[(int)TerrainRouteKind.PrimaryTrail],
            routeCounts[(int)TerrainRouteKind.RiverRoad],
            routeCounts[(int)TerrainRouteKind.RidgePass],
            routeCounts[(int)TerrainRouteKind.CoastalPath],
            routeCounts[(int)TerrainRouteKind.ScenicTrail]);
    }

    private static float ComputePointCoverage(
        TerrainWorldPointOfInterest[] points,
        float worldSize)
    {
        if (points.Length == 0)
        {
            return 0.0f;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            minX = Mathf.Min(minX, point.WorldPosition.X);
            maxX = Mathf.Max(maxX, point.WorldPosition.X);
            minY = Mathf.Min(minY, point.WorldPosition.Y);
            maxY = Mathf.Max(maxY, point.WorldPosition.Y);
        }

        return ComputeNormalizedCoverage(minX, maxX, minY, maxY, worldSize);
    }

    private static float ComputePointCoverage(
        List<TerrainWorldPointOfInterest> points,
        float worldSize)
    {
        if (points.Count == 0)
        {
            return 0.0f;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            minX = Mathf.Min(minX, point.WorldPosition.X);
            maxX = Mathf.Max(maxX, point.WorldPosition.X);
            minY = Mathf.Min(minY, point.WorldPosition.Y);
            maxY = Mathf.Max(maxY, point.WorldPosition.Y);
        }

        return ComputeNormalizedCoverage(minX, maxX, minY, maxY, worldSize);
    }

    private static float ComputeCoverageWithCandidate(
        List<TerrainWorldPointOfInterest> points,
        Vector2 candidateWorldPosition,
        float worldSize)
    {
        if (points.Count == 0)
        {
            return 0.0f;
        }

        float minX = candidateWorldPosition.X;
        float maxX = candidateWorldPosition.X;
        float minY = candidateWorldPosition.Y;
        float maxY = candidateWorldPosition.Y;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            minX = Mathf.Min(minX, point.WorldPosition.X);
            maxX = Mathf.Max(maxX, point.WorldPosition.X);
            minY = Mathf.Min(minY, point.WorldPosition.Y);
            maxY = Mathf.Max(maxY, point.WorldPosition.Y);
        }

        return ComputeNormalizedCoverage(minX, maxX, minY, maxY, worldSize);
    }

    private static float ComputeNearestPointDistanceRatio(
        Vector2 worldPosition,
        List<TerrainWorldPointOfInterest> points,
        float worldSize)
    {
        float minDistanceSquared = float.PositiveInfinity;
        foreach (TerrainWorldPointOfInterest point in points)
        {
            minDistanceSquared = Mathf.Min(minDistanceSquared, worldPosition.DistanceSquaredTo(point.WorldPosition));
        }

        float distance = Mathf.Sqrt(minDistanceSquared);
        return Mathf.Clamp(distance / Mathf.Max(1.0f, worldSize * 0.32f), 0.0f, 1.0f);
    }

    private static float ComputeRouteCoverage(
        TerrainWorldRoute[] routes,
        float worldSize)
    {
        bool hasWaypoint = false;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (TerrainWorldRoute route in routes)
        {
            foreach (Vector2 waypoint in route.Waypoints)
            {
                hasWaypoint = true;
                minX = Mathf.Min(minX, waypoint.X);
                maxX = Mathf.Max(maxX, waypoint.X);
                minY = Mathf.Min(minY, waypoint.Y);
                maxY = Mathf.Max(maxY, waypoint.Y);
            }
        }

        return hasWaypoint
            ? ComputeNormalizedCoverage(minX, maxX, minY, maxY, worldSize)
            : 0.0f;
    }

    private static float ComputeNormalizedCoverage(
        float minX,
        float maxX,
        float minY,
        float maxY,
        float worldSize)
    {
        float safeWorldSize = Mathf.Max(1.0f, worldSize);
        float coverageX = Mathf.Clamp((maxX - minX) / safeWorldSize, 0.0f, 1.0f);
        float coverageY = Mathf.Clamp((maxY - minY) / safeWorldSize, 0.0f, 1.0f);
        return Mathf.Clamp(Mathf.Sqrt((coverageX * coverageX) + (coverageY * coverageY)) / 1.4142135f, 0.0f, 1.0f);
    }

    private static int CountNonZero(ReadOnlySpan<int> values)
    {
        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] > 0)
            {
                count++;
            }
        }

        return count;
    }
}
