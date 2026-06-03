using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Categorizes the visual/gameplay type of a surface scatter instance.</summary>
public enum TerrainScatterKind
{
    Tree,
    Rock,
    Understory,
    ResourceNode,
    HazardOutcrop,
    GrassTuft,
    DesertShrub,
    CactusCluster,
    ReedCluster,
    SnowClump,
    AlpinePine,
    CoastalPalm,
    Driftwood,
    MangroveRoot,
    Landmark
}

/// <summary>Defines the specific landmark type for planned POIs and natural scenic features.</summary>
public enum TerrainLandmarkKind
{
    Settlement,
    Vista,
    RiverCrossing,
    MountainPass,
    AncientStone,
    CoastalLanding,
    ResourceGrove,
    CanyonOverlook,
    Oasis,
    Village,
    Town,
    OasisHub,
    VillageHouse,
    TownBlock,
    OasisCanopy,
    SettlementPlaza,
    OasisPool,
    Waterfall,
    RoadMarker,
    BridgeSpan,
    DuneCrest,
    DesertMonolith,
    CanyonNeedle,
    IceSpire,
    NaturalArch,
    GeothermalSpring,
    GlacialRidge
}

/// <summary>A single placed scatter object (tree, rock, landmark, etc.) with transform and tint.</summary>
public readonly record struct TerrainScatterInstance(
    TerrainScatterKind Kind,
    Vector3 LocalPosition,
    float RotationY,
    float UniformScale,
    Color Color,
    TerrainLandmarkKind LandmarkKind)
{
    /// <summary>Creates a non-landmark scatter instance with a default landmark kind.</summary>
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

/// <summary>Metadata for a named landmark placed on the tile (used for POI identification and debugging).</summary>
public readonly record struct TerrainLandmarkData(
    TerrainLandmarkKind Kind,
    Vector3 LocalPosition,
    float Score,
    string DebugName);
