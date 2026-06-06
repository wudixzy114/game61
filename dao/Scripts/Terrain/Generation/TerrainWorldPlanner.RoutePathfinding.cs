using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static TerrainWorldRoute? TryBuildRoute(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        TerrainRouteRuleSetSnapshot routeRules,
        int resolution,
        CancellationToken cancellationToken)
    {
        return RoutePathfinderService.TryBuildRoute(from, to, fields, profile, routeRules, resolution, cancellationToken);
    }

    private static long PointPairKey(int a, int b)
    {
        int min = Math.Min(a, b);
        int max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    private static bool IsSettlementHub(TerrainWorldPointOfInterest point)
    {
        return point.SettlementTier is TerrainSettlementTier.Village or TerrainSettlementTier.Town or TerrainSettlementTier.OasisHub;
    }

    private static bool IsSettlementHub(TerrainWorldPointOfInterest[] points, int pointId)
    {
        return (uint)pointId < (uint)points.Length && IsSettlementHub(points[pointId]);
    }

    private static int CountSettlementHubs(TerrainWorldPointOfInterest[] points)
    {
        int count = 0;
        foreach (TerrainWorldPointOfInterest point in points)
        {
            if (IsSettlementHub(point))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSettlementRoutes(
        TerrainWorldPointOfInterest[] points,
        List<TerrainWorldRoute> routes)
    {
        int count = 0;
        foreach (TerrainWorldRoute route in routes)
        {
            if (IsSettlementHub(points, route.FromPointId) && IsSettlementHub(points, route.ToPointId))
            {
                count++;
            }
        }

        return count;
    }

    private static float SettlementTierRouteWeight(TerrainSettlementTier tier)
    {
        return tier switch
        {
            TerrainSettlementTier.Town => 1.0f,
            TerrainSettlementTier.OasisHub => 0.94f,
            TerrainSettlementTier.Village => 0.76f,
            _ => 0.0f
        };
    }
}
