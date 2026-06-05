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
    bool TryFindNearestPointOfInterest(
        Vector2 world,
        float radius,
        TerrainPointOfInterestKind? kind,
        out TerrainWorldPointOfInterest point);
    TerrainWorldPointOfInterest[] QueryPointsOfInterest(
        Rect2 worldBounds,
        TerrainPointOfInterestKind? kind = null);
    TerrainWorldRoute[] QueryRoutesNear(Vector2 world, float radius);
    TerrainRouteCorridorSample SampleRouteCorridor(Vector2 world);
}
