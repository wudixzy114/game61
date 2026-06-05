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

    TerrainRouteGraphSnapshot GetRouteGraphSnapshot();
    bool TryGetRouteGraphSnapshot([NotNullWhen(true)] out TerrainRouteGraphSnapshot? snapshot);
}
