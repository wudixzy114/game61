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
    Settlement,
    Vista,
    RiverCrossing,
    MountainPass,
    AncientStone,
    CoastalLanding,
    ResourceGrove,
    CanyonOverlook
}

public readonly record struct TerrainScatterInstance(
    TerrainScatterKind Kind,
    Vector3 LocalPosition,
    float RotationY,
    float UniformScale,
    Color Color,
    TerrainLandmarkKind LandmarkKind)
{
    public TerrainScatterInstance(
        TerrainScatterKind kind,
        Vector3 localPosition,
        float rotationY,
        float uniformScale,
        Color color)
        : this(kind, localPosition, rotationY, uniformScale, color, TerrainLandmarkKind.AncientStone)
    {
    }
}

public readonly record struct TerrainLandmarkData(
    TerrainLandmarkKind Kind,
    Vector3 LocalPosition,
    float Score,
    string DebugName);
