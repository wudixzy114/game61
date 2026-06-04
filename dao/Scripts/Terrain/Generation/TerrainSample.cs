using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Aggregated terrain field data sampled at a single world position, including slope and surface color.</summary>
public readonly record struct TerrainSample(
    float Height,
    float Continental,
    float Mountain,
    float River,
    float Lake,
    float Moisture,
    float Temperature,
    float ScenicPotential,
    float Traversability,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind,
    float Slope,
    Color Color);
