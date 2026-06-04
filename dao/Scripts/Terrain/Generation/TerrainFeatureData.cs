using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Categorizes the visual/gameplay type of a surface scatter instance.</summary>
public enum TerrainScatterKind
{
    Tree = 0,
    Rock = 1,
    Understory = 2,
    ResourceNode = 3,
    HazardOutcrop = 4,
    GrassTuft = 5,
    DesertShrub = 6,
    CactusCluster = 7,
    ReedCluster = 8,
    SnowClump = 9,
    AlpinePine = 10,
    CoastalPalm = 11,
    Driftwood = 12,
    MangroveRoot = 13,
    LakeReed = 14,
    WaterLily = 15,
    Landmark = 16
}

/// <summary>Defines the specific landmark type for planned POIs and natural scenic features.</summary>
public enum TerrainLandmarkKind
{
    Settlement = 0,
    Vista = 1,
    RiverCrossing = 2,
    MountainPass = 3,
    AncientStone = 4,
    CoastalLanding = 5,
    ResourceGrove = 6,
    CanyonOverlook = 7,
    Oasis = 8,
    Village = 9,
    Town = 10,
    OasisHub = 11,
    VillageHouse = 12,
    TownBlock = 13,
    OasisCanopy = 14,
    SettlementPlaza = 15,
    OasisPool = 16,
    Waterfall = 17,
    RoadMarker = 18,
    BridgeSpan = 19,
    DuneCrest = 20,
    DesertMonolith = 21,
    CanyonNeedle = 22,
    IceSpire = 23,
    NaturalArch = 24,
    GeothermalSpring = 25,
    GlacialRidge = 26,
    VillageWell = 27,
    MarketStall = 28,
    WatchTower = 29,
    OasisGarden = 30,
    SettlementGateway = 31
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
