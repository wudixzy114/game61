using Godot;

namespace Dao.Terrain.Generation;

/// <summary>A single sampled terrain placement candidate with semantic context for gameplay systems.</summary>
public readonly record struct TerrainPlacementCandidate(
    Vector2 WorldPosition,
    float Height,
    float Score,
    TerrainGameplayTags Tags,
    TerrainTraversalCost Traversal,
    TerrainWaterState Water,
    TerrainRouteCorridorSample RouteCorridor)
{
    public bool NearRoute => RouteCorridor.HasInfluence;
}

internal readonly record struct TerrainPlacementCandidateScore(
    Vector2 WorldPosition,
    float Height,
    float Score,
    TerrainGameplayTags Tags,
    TerrainTraversalCost Traversal,
    TerrainWaterState Water,
    TerrainRouteCorridorSample RouteCorridor);
