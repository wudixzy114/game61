using Godot;

namespace Dao.Terrain.Generation;

internal readonly record struct TerrainGameplayScatterRule(
    float MaxSlope,
    float MinPrimary,
    float MinSecondary,
    float MinTemperature,
    float ProbabilityLow,
    float ProbabilityHigh,
    float BaseScale,
    float ScaleJitter,
    Color TintLow,
    Color TintHigh);

internal readonly record struct TerrainWaterZoneScatterRule(
    float MaxSlope,
    float MinHeightOffset,
    float MaxHeightValue,
    float MinPrimary,
    float MinSecondary,
    float Threshold,
    float ShorelineHeightOffset);

internal readonly record struct TerrainScatterVariantRule(
    float ProbabilityLow,
    float ProbabilityHigh,
    float BaseScale,
    float ScaleJitterFactor,
    Color TintLow,
    Color TintHigh);

internal readonly record struct TerrainSurfaceNaturalScatterRule(
    float MaxSlope,
    float MinMoisture,
    float MinTemperature,
    float MaxRiver,
    float MinTraversability,
    float Probability,
    float BaseScale,
    float ScaleJitter,
    Color TintLow,
    Color TintHigh);

internal readonly record struct TerrainSurfaceRockScatterRule(
    float MinSlope,
    float MinHeightAboveSea,
    float MinHazardPotential,
    float Probability,
    float BaseScale,
    float ScaleJitter,
    Color TintLow,
    Color TintHigh);

internal readonly record struct TerrainScatterRuleSetSnapshot(
    float NaturalDensityPenalty,
    float BaseDensityPenalty,
    TerrainSurfaceNaturalScatterRule Tree,
    TerrainSurfaceRockScatterRule Rock,
    TerrainGameplayScatterRule Understory,
    TerrainGameplayScatterRule ResourceNode,
    TerrainGameplayScatterRule HazardOutcrop,
    TerrainWaterZoneScatterRule TidalMangroveFlat,
    TerrainWaterZoneScatterRule LakeScatterZone,
    TerrainScatterVariantRule TidalMangroveRoot,
    TerrainScatterVariantRule LakeWaterLily,
    TerrainScatterVariantRule LakeReed,
    TerrainScatterVariantRule GrassTuft,
    TerrainScatterVariantRule CoastalMangroveRoot,
    TerrainScatterVariantRule CoastalPalm,
    TerrainScatterVariantRule Driftwood,
    TerrainScatterVariantRule OasisReed,
    TerrainScatterVariantRule DesertCactus,
    TerrainScatterVariantRule DesertShrub,
    TerrainScatterVariantRule WetlandMangroveRoot,
    TerrainScatterVariantRule WetlandReed,
    TerrainScatterVariantRule SnowfieldAlpinePine,
    TerrainScatterVariantRule SnowClump,
    TerrainScatterVariantRule MountainAlpinePine)
{
    public string StableHash()
    {
        return Dao.Terrain.TerrainScatterRuleSet.ComputeHash(this);
    }
}

internal static class TerrainTileBuilderSurfaceScatterDefaults
{
    public static readonly TerrainScatterVariantRule TidalMangroveRoot = new(0.26f, 0.58f, 0.86f, 0.86f, new Color(0.18f, 0.25f, 0.14f), new Color(0.36f, 0.44f, 0.22f));
    public static readonly TerrainScatterVariantRule LakeWaterLily = new(0.12f, 0.34f, 0.58f, 0.86f, new Color(0.12f, 0.36f, 0.24f), new Color(0.62f, 0.78f, 0.56f));
    public static readonly TerrainScatterVariantRule LakeReed = new(0.12f, 0.36f, 0.74f, 0.86f, new Color(0.20f, 0.42f, 0.26f), new Color(0.52f, 0.48f, 0.24f));
    public static readonly TerrainScatterVariantRule GrassTuft = new(0.10f, 0.32f, 0.52f, 0.86f, new Color(0.34f, 0.46f, 0.20f), new Color(0.55f, 0.50f, 0.24f));
    public static readonly TerrainScatterVariantRule CoastalMangroveRoot = new(0.08f, 0.22f, 0.82f, 0.86f, new Color(0.22f, 0.28f, 0.16f), new Color(0.36f, 0.42f, 0.22f));
    public static readonly TerrainScatterVariantRule CoastalPalm = new(0.07f, 0.24f, 1.10f, 0.86f, new Color(0.16f, 0.42f, 0.23f), new Color(0.48f, 0.40f, 0.20f));
    public static readonly TerrainScatterVariantRule Driftwood = new(0.08f, 0.26f, 0.70f, 0.86f, new Color(0.46f, 0.36f, 0.24f), new Color(0.66f, 0.58f, 0.42f));
    public static readonly TerrainScatterVariantRule OasisReed = new(0.08f, 0.30f, 0.62f, 0.86f, new Color(0.22f, 0.48f, 0.30f), new Color(0.52f, 0.46f, 0.24f));
    public static readonly TerrainScatterVariantRule DesertCactus = new(0.06f, 0.18f, 0.88f, 0.86f, new Color(0.20f, 0.36f, 0.22f), new Color(0.44f, 0.50f, 0.26f));
    public static readonly TerrainScatterVariantRule DesertShrub = new(0.08f, 0.30f, 0.56f, 0.86f, new Color(0.46f, 0.38f, 0.20f), new Color(0.70f, 0.56f, 0.30f));
    public static readonly TerrainScatterVariantRule WetlandMangroveRoot = new(0.08f, 0.24f, 0.82f, 0.86f, new Color(0.20f, 0.27f, 0.15f), new Color(0.36f, 0.42f, 0.22f));
    public static readonly TerrainScatterVariantRule WetlandReed = new(0.16f, 0.42f, 0.70f, 0.86f, new Color(0.20f, 0.40f, 0.24f), new Color(0.48f, 0.42f, 0.22f));
    public static readonly TerrainScatterVariantRule SnowfieldAlpinePine = new(0.05f, 0.18f, 0.94f, 0.86f, new Color(0.10f, 0.26f, 0.18f), new Color(0.62f, 0.72f, 0.70f));
    public static readonly TerrainScatterVariantRule SnowClump = new(0.10f, 0.34f, 0.72f, 0.86f, new Color(0.74f, 0.80f, 0.82f), Colors.White);
    public static readonly TerrainScatterVariantRule MountainAlpinePine = new(0.04f, 0.16f, 1.00f, 0.86f, new Color(0.10f, 0.24f, 0.17f), new Color(0.36f, 0.42f, 0.32f));

    public static TerrainScatterRuleSetSnapshot CreateDefault()
    {
        return new TerrainScatterRuleSetSnapshot(
            NaturalDensityPenalty: 0.42f,
            BaseDensityPenalty: 1.0f,
            Tree: new TerrainSurfaceNaturalScatterRule(
                MaxSlope: 0.30f,
                MinMoisture: 0.47f,
                MinTemperature: 0.24f,
                MaxRiver: 0.78f,
                MinTraversability: 0.35f,
                Probability: 0.44f,
                BaseScale: 2.2f,
                ScaleJitter: 3.4f,
                TintLow: new Color(0.22f, 0.44f, 0.19f),
                TintHigh: new Color(0.08f, 0.25f, 0.12f)),
            Rock: new TerrainSurfaceRockScatterRule(
                MinSlope: 0.35f,
                MinHeightAboveSea: 360.0f,
                MinHazardPotential: 0.56f,
                Probability: 0.38f,
                BaseScale: 1.3f,
                ScaleJitter: 3.1f,
                TintLow: new Color(0.36f, 0.35f, 0.32f),
                TintHigh: new Color(0.55f, 0.54f, 0.49f)),
            Understory: new TerrainGameplayScatterRule(
                MaxSlope: 0.22f,
                MinPrimary: 0.42f,
                MinSecondary: 0.50f,
                MinTemperature: 0.24f,
                ProbabilityLow: 0.08f,
                ProbabilityHigh: 0.46f,
                BaseScale: 0.55f,
                ScaleJitter: 0.95f,
                TintLow: new Color(0.18f, 0.36f, 0.16f),
                TintHigh: new Color(0.34f, 0.50f, 0.22f)),
            ResourceNode: new TerrainGameplayScatterRule(
                MaxSlope: 0.30f,
                MinPrimary: 0.62f,
                MinSecondary: 0.34f,
                MinTemperature: 0.0f,
                ProbabilityLow: 0.04f,
                ProbabilityHigh: 0.24f,
                BaseScale: 0.95f,
                ScaleJitter: 1.45f,
                TintLow: new Color(0.28f, 0.48f, 0.22f),
                TintHigh: new Color(0.62f, 0.54f, 0.30f)),
            HazardOutcrop: new TerrainGameplayScatterRule(
                MaxSlope: float.PositiveInfinity,
                MinPrimary: 0.48f,
                MinSecondary: 0.40f,
                MinTemperature: 0.0f,
                ProbabilityLow: 0.05f,
                ProbabilityHigh: 0.30f,
                BaseScale: 0.85f,
                ScaleJitter: 1.80f,
                TintLow: new Color(0.38f, 0.30f, 0.27f),
                TintHigh: new Color(0.64f, 0.58f, 0.50f)),
            TidalMangroveFlat: new TerrainWaterZoneScatterRule(
                MaxSlope: 0.24f,
                MinHeightOffset: -8.0f,
                MaxHeightValue: 34.0f,
                MinPrimary: 0.50f,
                MinSecondary: 0.28f,
                Threshold: 0.26f,
                ShorelineHeightOffset: 12.0f),
            LakeScatterZone: new TerrainWaterZoneScatterRule(
                MaxSlope: 0.22f,
                MinHeightOffset: 6.0f,
                MaxHeightValue: 0.72f,
                MinPrimary: 0.30f,
                MinSecondary: 0.58f,
                Threshold: 0.34f,
                ShorelineHeightOffset: 0.0f),
            TidalMangroveRoot: TidalMangroveRoot,
            LakeWaterLily: LakeWaterLily,
            LakeReed: LakeReed,
            GrassTuft: GrassTuft,
            CoastalMangroveRoot: CoastalMangroveRoot,
            CoastalPalm: CoastalPalm,
            Driftwood: Driftwood,
            OasisReed: OasisReed,
            DesertCactus: DesertCactus,
            DesertShrub: DesertShrub,
            WetlandMangroveRoot: WetlandMangroveRoot,
            WetlandReed: WetlandReed,
            SnowfieldAlpinePine: SnowfieldAlpinePine,
            SnowClump: SnowClump,
            MountainAlpinePine: MountainAlpinePine);
    }
}

public static partial class TerrainTileBuilder
{
    private static TerrainScatterRuleSetSnapshot ResolveScatterRules(TerrainGenerationProfile profile)
    {
        return TerrainScatterRuleCatalog.Resolve(profile.ScatterRuleSetHash);
    }
}
