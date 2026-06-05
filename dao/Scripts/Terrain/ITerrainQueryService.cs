using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Gameplay-facing terrain sampling facade for systems that should not depend on TerrainWorld directly.</summary>
public interface ITerrainQueryService
{
    TerrainWorldField SampleField(Vector2 world);
    TerrainSample SampleSurface(Vector2 world, float spacing = 4.0f);
    Vector3 SurfacePositionAt(Vector2 world, float heightOffset = 0.0f);
    TerrainWaterState SampleWaterState(Vector2 world);
    TerrainGameplayTags SampleGameplayTags(Vector2 world);
    TerrainTraversalCost SampleTraversalCost(Vector2 world, float spacing = 4.0f);
    bool IsTraversable(Vector2 world, float minTraversability = 0.45f);
    bool IsAboveWater(Vector2 world, float margin = 0.0f);
}
