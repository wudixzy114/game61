using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Godot resource that stores all terrain generation parameters and produces immutable <see cref="TerrainGenerationProfile"/> snapshots.</summary>
[GlobalClass]
public partial class TerrainSettings : Resource
{
    [ExportGroup("Structured Profiles")]
    [Export] public TerrainWorldSettingsResource? WorldProfile { get; set; }
    [Export] public TerrainShapeSettingsResource? ShapeProfile { get; set; }
    [Export] public TerrainGameplaySettingsResource? GameplayProfile { get; set; }
    [Export] public TerrainStreamingSettingsResource? StreamingProfile { get; set; }
    [Export] public TerrainRenderingSettingsResource? RenderingProfile { get; set; }
    [Export] public TerrainScenicLandmarkRuleSet? ScenicLandmarkRuleSet { get; set; }

    [ExportGroup("World")]
    [Export] public int Seed { get; set; } = 613_061;
    [Export(PropertyHint.Range, "64,2048,1")] public float ChunkSize { get; set; } = 192.0f;
    [Export(PropertyHint.Range, "16,192,1")] public int BaseResolution { get; set; } = 64;
    [Export(PropertyHint.Range, "1,12,1")] public int StreamRadiusChunks { get; set; } = 5;
    [Export(PropertyHint.Range, "0,8,1")] public int CollisionRadiusChunks { get; set; } = 2;
    [Export(PropertyHint.Range, "0,5,1")] public int MaxLod { get; set; } = 3;

    [ExportGroup("Shape")]
    [Export(PropertyHint.Range, "64,3000,1")] public float HeightScale { get; set; } = 780.0f;
    [Export(PropertyHint.Range, "-400,400,1")] public float SeaLevel { get; set; } = -18.0f;
    [Export(PropertyHint.Range, "512,12000,1")] public float ContinentScale { get; set; } = 5200.0f;
    [Export(PropertyHint.Range, "128,6000,1")] public float MountainScale { get; set; } = 1800.0f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float MountainWeight { get; set; } = 0.72f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ValleyWeight { get; set; } = 0.44f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float DetailWeight { get; set; } = 0.18f;

    [ExportGroup("Gameplay Landmarks")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float VistaFrequency { get; set; } = 0.42f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RiverStrength { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,500,1")] public float RiverCarveDepth { get; set; } = 115.0f;
    [Export(PropertyHint.Range, "0,300,1")] public float TerraceStrength { get; set; } = 72.0f;

    [ExportGroup("Rendering")]
    [Export(PropertyHint.Range, "0,120,1")] public float SkirtDepth { get; set; } = 42.0f;
    [Export(PropertyHint.Range, "1,16,1")] public int MaxCompletedTilesPerFrame { get; set; } = 4;
    [Export(PropertyHint.Range, "1,64,1")] public int MaxQueuedTileJobs { get; set; } = 24;
    [Export(PropertyHint.Range, "0,512,1")] public int MaxCachedTileData { get; set; } = 96;
    [Export] public bool GenerateCollision { get; set; } = true;
    [Export] public bool UseNativeSamplerWhenAvailable { get; set; } = true;

    /// <summary>Creates an immutable snapshot profile from the current settings, clamping values to valid ranges.</summary>
    public TerrainGenerationProfile Snapshot()
    {
        TerrainWorldSettingsResource? world = WorldProfile;
        TerrainShapeSettingsResource? shape = ShapeProfile;
        TerrainGameplaySettingsResource? gameplay = GameplayProfile;
        TerrainStreamingSettingsResource? streaming = StreamingProfile;
        TerrainRenderingSettingsResource? rendering = RenderingProfile;
        string scenicLandmarkRuleSetHash = TerrainScenicLandmarkRuleCatalog.Register(ScenicLandmarkRuleSet);

        return new TerrainGenerationProfile(
            world?.Seed ?? Seed,
            Mathf.Max(16.0f, world?.ChunkSize ?? ChunkSize),
            Mathf.Clamp(world?.BaseResolution ?? BaseResolution, 8, 256),
            Mathf.Clamp(streaming?.StreamRadiusChunks ?? StreamRadiusChunks, 1, 32),
            Mathf.Clamp(streaming?.CollisionRadiusChunks ?? CollisionRadiusChunks, 0, 16),
            Mathf.Clamp(streaming?.MaxLod ?? MaxLod, 0, 8),
            shape?.HeightScale ?? HeightScale,
            shape?.SeaLevel ?? SeaLevel,
            Mathf.Max(128.0f, shape?.ContinentScale ?? ContinentScale),
            Mathf.Max(64.0f, shape?.MountainScale ?? MountainScale),
            Mathf.Clamp(shape?.MountainWeight ?? MountainWeight, 0.0f, 1.0f),
            Mathf.Clamp(shape?.ValleyWeight ?? ValleyWeight, 0.0f, 1.0f),
            Mathf.Clamp(shape?.DetailWeight ?? DetailWeight, 0.0f, 1.0f),
            Mathf.Clamp(gameplay?.VistaFrequency ?? VistaFrequency, 0.0f, 1.0f),
            Mathf.Clamp(gameplay?.RiverStrength ?? RiverStrength, 0.0f, 1.0f),
            gameplay?.RiverCarveDepth ?? RiverCarveDepth,
            gameplay?.TerraceStrength ?? TerraceStrength,
            rendering?.SkirtDepth ?? SkirtDepth,
            Mathf.Max(1, streaming?.MaxCompletedTilesPerFrame ?? MaxCompletedTilesPerFrame),
            Mathf.Max(1, streaming?.MaxQueuedTileJobs ?? MaxQueuedTileJobs),
            Mathf.Clamp(streaming?.MaxCachedTileData ?? MaxCachedTileData, 0, 2048),
            streaming?.GenerateCollision ?? GenerateCollision,
            streaming?.UseNativeSamplerWhenAvailable ?? UseNativeSamplerWhenAvailable,
            scenicLandmarkRuleSetHash);
    }
}

/// <summary>Immutable snapshot of all parameters needed to generate terrain. Produced by <see cref="TerrainSettings.Snapshot"/>.</summary>
public readonly record struct TerrainGenerationProfile(
    int Seed,
    float ChunkSize,
    int BaseResolution,
    int StreamRadiusChunks,
    int CollisionRadiusChunks,
    int MaxLod,
    float HeightScale,
    float SeaLevel,
    float ContinentScale,
    float MountainScale,
    float MountainWeight,
    float ValleyWeight,
    float DetailWeight,
    float VistaFrequency,
    float RiverStrength,
    float RiverCarveDepth,
    float TerraceStrength,
    float SkirtDepth,
    int MaxCompletedTilesPerFrame,
    int MaxQueuedTileJobs,
    int MaxCachedTileData,
    bool GenerateCollision,
    bool UseNativeSamplerWhenAvailable,
    string ScenicLandmarkRuleSetHash = "")
{
    /// <summary>Computes the stable content identity hash for this generation profile.</summary>
    public string StableHash()
    {
        return TerrainProfileHash.Compute(this);
    }

    /// <summary>Returns the tile vertex resolution for a given LOD (halved each step, minimum 8).</summary>
    public int ResolutionForLod(int lod)
    {
        int safeLod = Mathf.Clamp(lod, 0, MaxLod);
        return Mathf.Max(8, BaseResolution >> safeLod);
    }
}
