using Godot;

namespace Dao.Terrain.Generation;

public enum TerrainScatterKind
{
    Tree,
    Rock,
    Landmark
}

public enum TerrainLandmarkKind
{
    Vista,
    RiverCrossing,
    MountainPass,
    AncientStone
}

public readonly record struct TerrainScatterInstance(
    TerrainScatterKind Kind,
    Vector3 LocalPosition,
    float RotationY,
    float UniformScale,
    Color Color);

public readonly record struct TerrainLandmarkData(
    TerrainLandmarkKind Kind,
    Vector3 LocalPosition,
    float Score,
    string DebugName);
