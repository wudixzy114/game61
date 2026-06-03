using Godot;

namespace Dao.Terrain.Generation;

public readonly record struct TerrainSample(
    float Height,
    float Continental,
    float Mountain,
    float River,
    float Moisture,
    float Temperature,
    float ScenicPotential,
    float Traversability,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind,
    float Slope,
    Color Color);
