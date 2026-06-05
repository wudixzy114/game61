using System.Collections.Concurrent;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Broad landscape form classification for a terrain position.</summary>
public enum TerrainLandscapeKind
{
    Ocean = 0,
    Coast = 1,
    Lowland = 2,
    Wetland = 3,
    ForestBasin = 4,
    RiverValley = 5,
    Canyon = 6,
    Highlands = 7,
    MountainMassif = 8,
    Snowfield = 9,
    VistaPlateau = 10,
    Lake = 11
}

/// <summary>Climate-based biome classification for a terrain position.</summary>
public enum TerrainBiomeKind
{
    Ocean = 0,
    Coast = 1,
    Island = 2,
    Plains = 3,
    Grassland = 4,
    Desert = 5,
    Oasis = 6,
    Forest = 7,
    Wetland = 8,
    Hills = 9,
    Mountains = 10,
    Snowfield = 11,
    Lake = 12
}

/// <summary>Complete set of derived terrain attributes sampled at a world position.</summary>
public readonly record struct TerrainWorldField(
    Vector2 WorldPosition,
    float Height,
    float Continent,
    float Basin,
    float Shelf,
    float Mountains,
    float BroadElevation,
    float River,
    float Lake,
    float Moisture,
    float Temperature,
    float ScenicPotential,
    float Traversability,
    float Exposure,
    float ResourcePotential,
    float HazardPotential,
    float EncounterPotential,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind);

/// <summary>Primary sampler that computes all terrain field values (height, climate, resources, etc.) at any world coordinate.</summary>
public static partial class TerrainWorldFieldSampler
{
    public const int NativeFieldGridStride = 18;

    private static readonly ConcurrentDictionary<TerrainGenerationProfile, float> LandBalanceOffsets = new();
    private const int MaxNativeBiomeKind = (int)TerrainBiomeKind.Lake;
    private const int MaxNativeLandscapeKind = (int)TerrainLandscapeKind.Lake;

    /// <summary>Full terrain sample at a world position, computing all field attributes including height.</summary>
    public static TerrainWorldField Sample(Vector2 world, TerrainGenerationProfile profile)
    {
        TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: true);
        float height = BuildHeight(terms, profile);
        return BuildField(world, profile, terms, height);
    }

    /// <summary>Full terrain sample using a caller-cached land-balance offset for hot grid sampling paths.</summary>
    public static TerrainWorldField Sample(Vector2 world, TerrainGenerationProfile profile, float landBalanceOffset)
    {
        TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: true);
        float height = BuildHeight(terms, profile, landBalanceOffset);
        return BuildField(world, profile, terms, height);
    }

    /// <summary>Samples terrain fields using a precomputed height, skipping micro-detail for efficiency.</summary>
    public static TerrainWorldField SampleKnownHeight(Vector2 world, TerrainGenerationProfile profile, float height)
    {
        TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: false);
        return BuildField(world, profile, terms, height);
    }

    /// <summary>Deserializes a <see cref="TerrainWorldField"/> from a native sampler's flat float array at the given index.</summary>
    public static TerrainWorldField SampleNativeFieldGrid(Vector2 world, TerrainGenerationProfile profile, float[] samples, int sampleIndex)
    {
        return SampleNativeFieldGrid(world, profile, samples, sampleIndex, containsDerivedFields: false);
    }

    /// <summary>Deserializes a native field grid sample. Derived grids already contain gameplay fields and classifications.</summary>
    public static TerrainWorldField SampleNativeFieldGrid(
        Vector2 world,
        TerrainGenerationProfile profile,
        float[] samples,
        int sampleIndex,
        bool containsDerivedFields)
    {
        int offset = sampleIndex * NativeFieldGridStride;
        float height = samples[offset];
        if (containsDerivedFields)
        {
            TerrainBiomeKind biome = (TerrainBiomeKind)Mathf.Clamp(Mathf.RoundToInt(samples[offset + 16]), 0, MaxNativeBiomeKind);
            TerrainLandscapeKind landscape = (TerrainLandscapeKind)Mathf.Clamp(Mathf.RoundToInt(samples[offset + 17]), 0, MaxNativeLandscapeKind);
            return new TerrainWorldField(
                world,
                height,
                samples[offset + 1],
                samples[offset + 2],
                samples[offset + 3],
                samples[offset + 4],
                samples[offset + 5],
                samples[offset + 6],
                samples[offset + 7],
                samples[offset + 8],
                samples[offset + 9],
                samples[offset + 10],
                samples[offset + 11],
                samples[offset + 12],
                samples[offset + 13],
                samples[offset + 14],
                samples[offset + 15],
                biome,
                landscape);
        }

        TerrainShapeTerms terms = new(
            samples[offset + 1],
            samples[offset + 2],
            samples[offset + 3],
            samples[offset + 4],
            samples[offset + 5],
            samples[offset + 6],
            samples[offset + 7],
            0.0f,
            samples[offset + 8],
            samples[offset + 9],
            samples[offset + 10],
            samples[offset + 11],
            samples[offset + 12],
            samples[offset + 13],
            samples[offset + 14],
            samples[offset + 15],
            samples[offset + 16],
            samples[offset + 17]);
        return BuildField(world, profile, terms, height);
    }

    /// <summary>Returns the cached land-balance height offset for the given profile (computes if needed).</summary>
    public static float LandBalanceOffsetFor(TerrainGenerationProfile profile)
    {
        return GetLandBalanceOffset(profile);
    }

    private readonly record struct TerrainShapeTerms(
        float Continent,
        float Basin,
        float Shelf,
        float Mountains,
        float BroadElevation,
        float River,
        float Lake,
        float MicroDetail,
        float BaseMoisture,
        float BaseTemperature,
        float Aridity,
        float Plains,
        float Wetland,
        float Forest,
        float Hills,
        float Alpine,
        float Island,
        float DuneDetail);
}
