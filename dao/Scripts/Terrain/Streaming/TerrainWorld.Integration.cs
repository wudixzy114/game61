using System.Diagnostics.CodeAnalysis;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
    /// <summary>Queries gameplay-facing placement candidates from terrain semantics without instantiating resources or encounters.</summary>
    public TerrainPlacementCandidate[] QueryPlacementCandidates(
        Rect2 worldBounds,
        TerrainGameplayTag requiredTags,
        TerrainGameplayTag excludedTags = TerrainGameplayTag.None,
        int maxCandidates = 32,
        float sampleSpacing = 32.0f,
        float minTraversability = 0.45f,
        float maxTraversalCost = 2.4f,
        float maxHazardPotential = 1.0f,
        bool requireRouteInfluence = false,
        float minRouteInfluence = 0.0f)
    {
        return TerrainPlacementHelper.QueryCandidates(
            CurrentProfile,
            worldBounds,
            requiredTags,
            excludedTags,
            maxCandidates,
            sampleSpacing,
            minTraversability,
            maxTraversalCost,
            maxHazardPotential,
            requireRouteInfluence,
            minRouteInfluence,
            _worldPlan is null ? null : _routeCorridors);
    }

    /// <summary>Builds a traversal-cost grid handoff for navigation, AI, and gameplay tools without performing pathfinding.</summary>
    public TerrainTraversalCostGrid CreateTraversalCostGrid(
        Vector2 center,
        float worldSize,
        int gridSize,
        float spacing = 24.0f)
    {
        return TerrainMapExporter.CreateTraversalCostGrid(CurrentProfile, center, worldSize, gridSize, spacing);
    }

    /// <summary>Builds a traversal-cost grid exactly covering a streaming tile without requiring that tile to be loaded.</summary>
    public TerrainTraversalCostGrid CreateTraversalCostGridForTile(
        TerrainTileCoord coord,
        int gridSize,
        float spacing = 24.0f)
    {
        return TerrainMapExporter.CreateTraversalCostGridForTile(CurrentProfile, coord, gridSize, spacing);
    }

    /// <summary>Samples traversal costs inside a bounded world-space region without performing pathfinding.</summary>
    public TerrainTraversalCost[] QueryTraversalCosts(
        Rect2 worldBounds,
        float sampleSpacing = 24.0f,
        int maxSamples = 1024)
    {
        return TerrainMapExporter.QueryTraversalCosts(CurrentProfile, worldBounds, sampleSpacing, maxSamples);
    }

    /// <summary>Returns a snapshot copy of the current planned route graph, or an empty snapshot when no plan is ready.</summary>
    public TerrainRouteGraphSnapshot GetRouteGraphSnapshot()
    {
        return _worldPlan is null
            ? TerrainRouteGraphSnapshot.Empty
            : TerrainRouteGraphSnapshot.FromPlan(_worldPlan);
    }

    /// <summary>Builds a waypoint graph from the current planned route graph for AI/navigation importers without requiring loaded tiles.</summary>
    public TerrainNavigationWaypointGraph CreateNavigationWaypointGraph()
    {
        return _worldPlan is null
            ? TerrainNavigationWaypointGraph.Empty
            : TerrainNavigationWaypointGraph.FromPlan(_worldPlan);
    }

    /// <summary>Returns a snapshot copy of the current planned route graph without exposing internal mutable waypoint arrays.</summary>
    public bool TryGetRouteGraphSnapshot([NotNullWhen(true)] out TerrainRouteGraphSnapshot? snapshot)
    {
        if (_worldPlan is null)
        {
            snapshot = null;
            return false;
        }

        snapshot = TerrainRouteGraphSnapshot.FromPlan(_worldPlan);
        return true;
    }

    /// <summary>Finds a high-level planned route path between two POIs without requiring streamed tiles or a rendered nav mesh.</summary>
    public bool TryFindRoutePath(int fromPointId, int toPointId, [NotNullWhen(true)] out TerrainRouteGraphPath? path)
    {
        path = null;
        if (_worldPlan is null)
        {
            return false;
        }

        TerrainRouteGraphSnapshot snapshot = TerrainRouteGraphSnapshot.FromPlan(_worldPlan);
        return snapshot.TryFindPath(fromPointId, toPointId, out path);
    }
}
