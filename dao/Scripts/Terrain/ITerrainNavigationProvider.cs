using System.Diagnostics.CodeAnalysis;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Navigation and path-cost handoff facade for systems that consume terrain semantics but do not own pathfinding.</summary>
public interface ITerrainNavigationProvider
{
    TerrainTraversalCostGrid CreateTraversalCostGrid(
        Vector2 center,
        float worldSize,
        int gridSize,
        float spacing = 24.0f);

    TerrainTraversalCostGrid CreateTraversalCostGridForTile(
        TerrainTileCoord coord,
        int gridSize,
        float spacing = 24.0f);

    TerrainTraversalCost[] QueryTraversalCosts(
        Rect2 worldBounds,
        float sampleSpacing = 24.0f,
        int maxSamples = 1024);

    TerrainRouteGraphSnapshot GetRouteGraphSnapshot();
    TerrainNavigationWaypointGraph CreateNavigationWaypointGraph();
    bool TryGetRouteGraphSnapshot([NotNullWhen(true)] out TerrainRouteGraphSnapshot? snapshot);
    bool TryFindRoutePath(int fromPointId, int toPointId, [NotNullWhen(true)] out TerrainRouteGraphPath? path);
}
