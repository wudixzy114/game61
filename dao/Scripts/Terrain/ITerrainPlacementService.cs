using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Gameplay-facing placement candidate facade for resources, encounters, audio emitters, and local world interactions.</summary>
public interface ITerrainPlacementService
{
    TerrainPlacementCandidate[] QueryPlacementCandidates(
        Rect2 worldBounds,
        TerrainGameplayTag requiredTags,
        TerrainGameplayTag excludedTags = TerrainGameplayTag.None,
        int maxCandidates = 32,
        float sampleSpacing = 32.0f,
        float minTraversability = 0.45f,
        float maxTraversalCost = 2.4f,
        float maxHazardPotential = 1.0f,
        bool requireRouteInfluence = false,
        float minRouteInfluence = 0.0f);
}
