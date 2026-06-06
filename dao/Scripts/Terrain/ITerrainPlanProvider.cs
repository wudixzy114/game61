using System.Diagnostics.CodeAnalysis;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Stable read-only terrain world plan facade for quests, AI, resources, map tools, and UI.</summary>
public interface ITerrainPlanProvider
{
    TerrainWorldPlanSnapshot GetWorldPlanSnapshot();
    bool TryGetWorldPlanSnapshot([NotNullWhen(true)] out TerrainWorldPlanSnapshot? snapshot);
    TerrainWorldPointOfInterest[] GetPointsOfInterest();
    TerrainWorldRoute[] GetRoutes();
    TerrainWorldPointOfInterestSummary[] QueryNearestPointsOfInterest(
        Vector2 world,
        float radius,
        int maxResults,
        TerrainPointOfInterestKind? kind = null);
    bool TryFindNearestPointOfInterest(
        Vector2 world,
        float radius,
        TerrainPointOfInterestKind? kind,
        out TerrainWorldPointOfInterest point);
    TerrainWorldPointOfInterest[] QueryPointsOfInterest(
        Rect2 worldBounds,
        TerrainPointOfInterestKind? kind = null);
    TerrainGameplayTagRegionSummary[] QueryGameplayTagRegions(
        Rect2 worldBounds,
        TerrainGameplayTag requiredTags,
        TerrainGameplayTag excludedTags = TerrainGameplayTag.None,
        int maxResults = 32);
    TerrainWorldRoute[] QueryRoutesNear(Vector2 world, float radius);
    TerrainWorldRouteSummary[] QueryRouteSummariesNear(Vector2 world, float radius, int maxResults);
    TerrainRouteCorridorSample SampleRouteCorridor(Vector2 world);
}
