using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static TerrainWorldPointOfInterest[] SelectPointsOfInterest(
        List<PoiCandidate> candidates,
        TerrainGenerationProfile profile,
        TerrainPointOfInterestRuleSetSnapshot poiRules,
        int maxPoints,
        float cellSize,
        float worldSize,
        CancellationToken cancellationToken)
    {
        return PoiSelectionService.SelectPointsOfInterest(
            candidates,
            profile,
            poiRules,
            maxPoints,
            cellSize,
            worldSize,
            cancellationToken);
    }
}
