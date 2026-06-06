using System;
using System.Text;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Threshold bounds used to validate terrain generation quality.</summary>
public readonly record struct TerrainQualityThresholds(
    float MinLandRatio,
    float MaxLandRatio,
    float MinRiverRatio,
    float MinScenicRatio,
    float MinTraversableLandRatio,
    int MinDistinctLandscapeKinds,
    int MinDistinctBiomeKinds,
    float MinPlainsGrasslandRatio,
    float MinDesertOasisRatio,
    float MinIslandCoastRatio,
    float MinHillMountainRatio,
    float MinSnowRatio,
    float MinLakeRatio)
{
    public static TerrainQualityThresholds OpenWorldDefault { get; } = new(
        MinLandRatio: 0.38f,
        MaxLandRatio: 0.82f,
        MinRiverRatio: 0.035f,
        MinScenicRatio: 0.045f,
        MinTraversableLandRatio: 0.28f,
        MinDistinctLandscapeKinds: 6,
        MinDistinctBiomeKinds: 7,
        MinPlainsGrasslandRatio: 0.10f,
        MinDesertOasisRatio: 0.005f,
        MinIslandCoastRatio: 0.015f,
        MinHillMountainRatio: 0.004f,
        MinSnowRatio: 0.002f,
        MinLakeRatio: 0.002f);
}

/// <summary>Detailed terrain quality metrics from sampling a world region.</summary>
public readonly record struct TerrainQualityReport(
    int SampleCount,
    float WorldSize,
    float MinHeight,
    float MaxHeight,
    float AverageHeight,
    float LandRatio,
    float OceanRatio,
    float CoastRatio,
    float RiverRatio,
    float ScenicRatio,
    float TraversableLandRatio,
    int DistinctLandscapeKinds,
    int DistinctBiomeKinds,
    int OceanCount,
    int CoastCount,
    int LowlandCount,
    int WetlandCount,
    int ForestBasinCount,
    int RiverValleyCount,
    int CanyonCount,
    int HighlandsCount,
    int MountainMassifCount,
    int SnowfieldCount,
    int VistaPlateauCount,
    int LakeCount,
    int BiomeOceanCount,
    int BiomeCoastCount,
    int IslandCount,
    int PlainsCount,
    int GrasslandCount,
    int DesertCount,
    int OasisCount,
    int ForestCount,
    int BiomeWetlandCount,
    int HillsCount,
    int MountainsCount,
    int BiomeSnowfieldCount,
    int BiomeLakeCount)
{
    public float PlainsGrasslandRatio => Ratio(PlainsCount + GrasslandCount);
    public float DesertOasisRatio => Ratio(DesertCount + OasisCount);
    public float IslandCoastRatio => Ratio(IslandCount + Math.Max(CoastCount, BiomeCoastCount));
    public float HillMountainRatio => Ratio(Math.Max(HillsCount + MountainsCount, HighlandsCount + MountainMassifCount + VistaPlateauCount));
    public float SnowRatio => Ratio(Math.Max(SnowfieldCount, BiomeSnowfieldCount));
    public float LakeRatio => Ratio(Math.Max(LakeCount, BiomeLakeCount));

    public int CountFor(TerrainLandscapeKind kind)
    {
        return kind switch
        {
            TerrainLandscapeKind.Ocean => OceanCount,
            TerrainLandscapeKind.Coast => CoastCount,
            TerrainLandscapeKind.Lowland => LowlandCount,
            TerrainLandscapeKind.Wetland => WetlandCount,
            TerrainLandscapeKind.ForestBasin => ForestBasinCount,
            TerrainLandscapeKind.RiverValley => RiverValleyCount,
            TerrainLandscapeKind.Canyon => CanyonCount,
            TerrainLandscapeKind.Highlands => HighlandsCount,
            TerrainLandscapeKind.MountainMassif => MountainMassifCount,
            TerrainLandscapeKind.Snowfield => SnowfieldCount,
            TerrainLandscapeKind.VistaPlateau => VistaPlateauCount,
            TerrainLandscapeKind.Lake => LakeCount,
            _ => 0
        };
    }

    public int CountFor(TerrainBiomeKind kind)
    {
        return kind switch
        {
            TerrainBiomeKind.Ocean => BiomeOceanCount,
            TerrainBiomeKind.Coast => BiomeCoastCount,
            TerrainBiomeKind.Island => IslandCount,
            TerrainBiomeKind.Plains => PlainsCount,
            TerrainBiomeKind.Grassland => GrasslandCount,
            TerrainBiomeKind.Desert => DesertCount,
            TerrainBiomeKind.Oasis => OasisCount,
            TerrainBiomeKind.Forest => ForestCount,
            TerrainBiomeKind.Wetland => BiomeWetlandCount,
            TerrainBiomeKind.Hills => HillsCount,
            TerrainBiomeKind.Mountains => MountainsCount,
            TerrainBiomeKind.Snowfield => BiomeSnowfieldCount,
            TerrainBiomeKind.Lake => BiomeLakeCount,
            _ => 0
        };
    }

    private float Ratio(int count)
    {
        return SampleCount <= 0 ? 0.0f : count / (float)SampleCount;
    }
}

/// <summary>Result of validating terrain quality against quality thresholds.</summary>
public readonly record struct TerrainQualityGateResult(
    bool Passed,
    TerrainQualityReport Report,
    string Summary);

/// <summary>Analyzes terrain field samples to produce quality reports and validate against configurable thresholds.</summary>
public static class TerrainQualityAnalyzer
{
    /// <summary>Analyzes terrain quality by sampling fields across the world area and computing aggregate metrics.</summary>
    public static TerrainQualityReport Analyze(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int sampleResolution,
        CancellationToken cancellationToken = default)
    {
        return TerrainQualityAnalysisService.Analyze(
            profile,
            center,
            worldSize,
            sampleResolution,
            cancellationToken);
    }

    /// <summary>Analyzes and validates terrain quality against the given thresholds.</summary>
    public static TerrainQualityGateResult Validate(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int sampleResolution,
        TerrainQualityThresholds thresholds,
        CancellationToken cancellationToken = default)
    {
        TerrainQualityReport report = Analyze(profile, center, worldSize, sampleResolution, cancellationToken);
        return ValidateReport(report, thresholds);
    }

    /// <summary>Validates a pre-computed quality report against thresholds.</summary>
    public static TerrainQualityGateResult ValidateReport(
        TerrainQualityReport report,
        TerrainQualityThresholds thresholds)
    {
        return TerrainQualityAnalysisService.ValidateReport(report, thresholds);
    }

    /// <summary>Validates a quality report against the default open-world thresholds.</summary>
    public static TerrainQualityGateResult ValidateOpenWorldDefault(TerrainQualityReport report)
    {
        return ValidateReport(report, TerrainQualityThresholds.OpenWorldDefault);
    }

    /// <summary>Analyzes and validates terrain quality against the default open-world thresholds.</summary>
    public static TerrainQualityGateResult ValidateOpenWorldDefault(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int sampleResolution,
        CancellationToken cancellationToken = default)
    {
        return Validate(profile, center, worldSize, sampleResolution, TerrainQualityThresholds.OpenWorldDefault, cancellationToken);
    }

}
