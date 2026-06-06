using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Compact gameplay-facing summary for nearest-point queries without copying the full plan payload.</summary>
public readonly record struct TerrainWorldPointOfInterestSummary(
    int Id,
    TerrainPointOfInterestKind Kind,
    Vector2 WorldPosition,
    float Distance,
    float Score,
    float ScenicPotential,
    float Traversability,
    TerrainSettlementTier SettlementTier,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind);

/// <summary>Compact gameplay-facing summary for nearby-route queries without copying waypoint arrays.</summary>
public readonly record struct TerrainWorldRouteSummary(
    int FromPointId,
    int ToPointId,
    TerrainRouteKind Kind,
    float Distance,
    float Cost,
    float AverageScenicPotential,
    float AverageTraversability,
    int WaypointCount);

/// <summary>Compact gameplay-facing region summary for bounded gameplay-tag queries without exposing the full plan grid.</summary>
public readonly record struct TerrainGameplayTagRegionSummary(
    int GridX,
    int GridY,
    Vector2 WorldPosition,
    Rect2 WorldBounds,
    TerrainGameplayTag Flags,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind,
    TerrainWorldRegionKind RegionKind,
    float Traversability,
    float ScenicPotential,
    float ResourcePotential,
    float HazardPotential,
    float EncounterPotential);
