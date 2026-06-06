using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static TerrainWorldRoute[] BuildRoutes(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        TerrainRouteRuleSetSnapshot routeRules,
        int resolution,
        int maxRoutes,
        CancellationToken cancellationToken)
    {
        return RoutePlanningService.BuildRoutes(points, fields, profile, routeRules, resolution, maxRoutes, cancellationToken);
    }

}
