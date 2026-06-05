using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Optional data-driven surface scatter rule set for natural props, gameplay scatter, and biome water-zone gating.</summary>
[GlobalClass]
public partial class TerrainScatterRuleSet : Resource
{
    [ExportGroup("Density")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float NaturalDensityPenalty { get; set; } = 0.42f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float BaseDensityPenalty { get; set; } = 1.0f;

    [ExportGroup("Tree Scatter")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float TreeMaxSlope { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float TreeMinMoisture { get; set; } = 0.47f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float TreeMinTemperature { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float TreeMaxRiver { get; set; } = 0.78f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float TreeMinTraversability { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float TreeProbability { get; set; } = 0.44f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float TreeBaseScale { get; set; } = 2.2f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float TreeScaleJitter { get; set; } = 3.4f;
    [Export] public Color TreeTintLow { get; set; } = new(0.22f, 0.44f, 0.19f);
    [Export] public Color TreeTintHigh { get; set; } = new(0.08f, 0.25f, 0.12f);

    [ExportGroup("Rock Scatter")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float RockMinSlope { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "0,2000,0.1")] public float RockMinHeightAboveSea { get; set; } = 360.0f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float RockMinHazardPotential { get; set; } = 0.56f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float RockProbability { get; set; } = 0.38f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float RockBaseScale { get; set; } = 1.3f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float RockScaleJitter { get; set; } = 3.1f;
    [Export] public Color RockTintLow { get; set; } = new(0.36f, 0.35f, 0.32f);
    [Export] public Color RockTintHigh { get; set; } = new(0.55f, 0.54f, 0.49f);

    [ExportGroup("Gameplay Understory")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float UnderstoryMaxSlope { get; set; } = 0.22f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float UnderstoryMinResourcePotential { get; set; } = 0.42f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float UnderstoryMinMoisture { get; set; } = 0.50f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float UnderstoryMinTemperature { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float UnderstoryProbabilityLow { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float UnderstoryProbabilityHigh { get; set; } = 0.46f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float UnderstoryBaseScale { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float UnderstoryScaleJitter { get; set; } = 0.95f;
    [Export] public Color UnderstoryTintLow { get; set; } = new(0.18f, 0.36f, 0.16f);
    [Export] public Color UnderstoryTintHigh { get; set; } = new(0.34f, 0.50f, 0.22f);

    [ExportGroup("Gameplay Resource")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float ResourceNodeMaxSlope { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ResourceNodeMinResourcePotential { get; set; } = 0.62f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ResourceNodeMinTraversability { get; set; } = 0.34f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ResourceNodeMinTemperature { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ResourceNodeProbabilityLow { get; set; } = 0.04f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ResourceNodeProbabilityHigh { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float ResourceNodeBaseScale { get; set; } = 0.95f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float ResourceNodeScaleJitter { get; set; } = 1.45f;
    [Export] public Color ResourceNodeTintLow { get; set; } = new(0.28f, 0.48f, 0.22f);
    [Export] public Color ResourceNodeTintHigh { get; set; } = new(0.62f, 0.54f, 0.30f);

    [ExportGroup("Gameplay Hazard")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float HazardOutcropMaxSlope { get; set; } = float.PositiveInfinity;
    [Export(PropertyHint.Range, "0,1,0.001")] public float HazardOutcropMinHazardPotential { get; set; } = 0.48f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float HazardOutcropMinEncounterPotential { get; set; } = 0.40f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float HazardOutcropMinTemperature { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float HazardOutcropProbabilityLow { get; set; } = 0.05f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float HazardOutcropProbabilityHigh { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float HazardOutcropBaseScale { get; set; } = 0.85f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float HazardOutcropScaleJitter { get; set; } = 1.80f;
    [Export] public Color HazardOutcropTintLow { get; set; } = new(0.38f, 0.30f, 0.27f);
    [Export] public Color HazardOutcropTintHigh { get; set; } = new(0.64f, 0.58f, 0.50f);

    [ExportGroup("Tidal Mangrove Zone")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float TidalMangroveFlatMaxSlope { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "-128,128,0.1")] public float TidalMangroveFlatMinHeightOffset { get; set; } = -8.0f;
    [Export(PropertyHint.Range, "-128,256,0.1")] public float TidalMangroveFlatMaxHeightOffset { get; set; } = 34.0f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float TidalMangroveFlatMinMoisture { get; set; } = 0.50f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float TidalMangroveFlatMinTemperature { get; set; } = 0.28f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float TidalMangroveFlatRiverThreshold { get; set; } = 0.26f;
    [Export(PropertyHint.Range, "-128,128,0.1")] public float TidalMangroveFlatShorelineHeightOffset { get; set; } = 12.0f;

    [ExportGroup("Lake Scatter Zone")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float LakeScatterZoneMaxSlope { get; set; } = 0.22f;
    [Export(PropertyHint.Range, "-128,128,0.1")] public float LakeScatterZoneMinHeightOffset { get; set; } = 6.0f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float LakeScatterZoneMaxHeightFactor { get; set; } = 0.72f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float LakeScatterZoneMinLake { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float LakeScatterZoneMinMoisture { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float LakeScatterZoneMinResourcePotential { get; set; } = 0.34f;

    [ExportGroup("Biome Output Variants")]
    [Export] public TerrainScatterVariantRuleResource? TidalMangroveRoot { get; set; }
    [Export] public TerrainScatterVariantRuleResource? LakeWaterLily { get; set; }
    [Export] public TerrainScatterVariantRuleResource? LakeReed { get; set; }
    [Export] public TerrainScatterVariantRuleResource? GrassTuft { get; set; }
    [Export] public TerrainScatterVariantRuleResource? CoastalMangroveRoot { get; set; }
    [Export] public TerrainScatterVariantRuleResource? CoastalPalm { get; set; }
    [Export] public TerrainScatterVariantRuleResource? Driftwood { get; set; }
    [Export] public TerrainScatterVariantRuleResource? OasisReed { get; set; }
    [Export] public TerrainScatterVariantRuleResource? DesertCactus { get; set; }
    [Export] public TerrainScatterVariantRuleResource? DesertShrub { get; set; }
    [Export] public TerrainScatterVariantRuleResource? WetlandMangroveRoot { get; set; }
    [Export] public TerrainScatterVariantRuleResource? WetlandReed { get; set; }
    [Export] public TerrainScatterVariantRuleResource? SnowfieldAlpinePine { get; set; }
    [Export] public TerrainScatterVariantRuleResource? SnowClump { get; set; }
    [Export] public TerrainScatterVariantRuleResource? MountainAlpinePine { get; set; }

    /// <summary>Computes a stable content hash for this scatter rule set.</summary>
    public string StableHash()
    {
        return ComputeHash(CreateSnapshot());
    }

    internal TerrainScatterRuleSetSnapshot CreateSnapshot()
    {
        return new TerrainScatterRuleSetSnapshot(
            NaturalDensityPenalty,
            BaseDensityPenalty,
            new TerrainSurfaceNaturalScatterRule(
                TreeMaxSlope,
                TreeMinMoisture,
                TreeMinTemperature,
                TreeMaxRiver,
                TreeMinTraversability,
                TreeProbability,
                TreeBaseScale,
                TreeScaleJitter,
                TreeTintLow,
                TreeTintHigh),
            new TerrainSurfaceRockScatterRule(
                RockMinSlope,
                RockMinHeightAboveSea,
                RockMinHazardPotential,
                RockProbability,
                RockBaseScale,
                RockScaleJitter,
                RockTintLow,
                RockTintHigh),
            new TerrainGameplayScatterRule(
                UnderstoryMaxSlope,
                UnderstoryMinResourcePotential,
                UnderstoryMinMoisture,
                UnderstoryMinTemperature,
                UnderstoryProbabilityLow,
                UnderstoryProbabilityHigh,
                UnderstoryBaseScale,
                UnderstoryScaleJitter,
                UnderstoryTintLow,
                UnderstoryTintHigh),
            new TerrainGameplayScatterRule(
                ResourceNodeMaxSlope,
                ResourceNodeMinResourcePotential,
                ResourceNodeMinTraversability,
                ResourceNodeMinTemperature,
                ResourceNodeProbabilityLow,
                ResourceNodeProbabilityHigh,
                ResourceNodeBaseScale,
                ResourceNodeScaleJitter,
                ResourceNodeTintLow,
                ResourceNodeTintHigh),
            new TerrainGameplayScatterRule(
                HazardOutcropMaxSlope,
                HazardOutcropMinHazardPotential,
                HazardOutcropMinEncounterPotential,
                HazardOutcropMinTemperature,
                HazardOutcropProbabilityLow,
                HazardOutcropProbabilityHigh,
                HazardOutcropBaseScale,
                HazardOutcropScaleJitter,
                HazardOutcropTintLow,
                HazardOutcropTintHigh),
            new TerrainWaterZoneScatterRule(
                TidalMangroveFlatMaxSlope,
                TidalMangroveFlatMinHeightOffset,
                TidalMangroveFlatMaxHeightOffset,
                TidalMangroveFlatMinMoisture,
                TidalMangroveFlatMinTemperature,
                TidalMangroveFlatRiverThreshold,
                TidalMangroveFlatShorelineHeightOffset),
            new TerrainWaterZoneScatterRule(
                LakeScatterZoneMaxSlope,
                LakeScatterZoneMinHeightOffset,
                LakeScatterZoneMaxHeightFactor,
                LakeScatterZoneMinLake,
                LakeScatterZoneMinMoisture,
                LakeScatterZoneMinResourcePotential,
                0.0f),
            Variant(TidalMangroveRoot, TerrainTileBuilderSurfaceScatterDefaults.TidalMangroveRoot),
            Variant(LakeWaterLily, TerrainTileBuilderSurfaceScatterDefaults.LakeWaterLily),
            Variant(LakeReed, TerrainTileBuilderSurfaceScatterDefaults.LakeReed),
            Variant(GrassTuft, TerrainTileBuilderSurfaceScatterDefaults.GrassTuft),
            Variant(CoastalMangroveRoot, TerrainTileBuilderSurfaceScatterDefaults.CoastalMangroveRoot),
            Variant(CoastalPalm, TerrainTileBuilderSurfaceScatterDefaults.CoastalPalm),
            Variant(Driftwood, TerrainTileBuilderSurfaceScatterDefaults.Driftwood),
            Variant(OasisReed, TerrainTileBuilderSurfaceScatterDefaults.OasisReed),
            Variant(DesertCactus, TerrainTileBuilderSurfaceScatterDefaults.DesertCactus),
            Variant(DesertShrub, TerrainTileBuilderSurfaceScatterDefaults.DesertShrub),
            Variant(WetlandMangroveRoot, TerrainTileBuilderSurfaceScatterDefaults.WetlandMangroveRoot),
            Variant(WetlandReed, TerrainTileBuilderSurfaceScatterDefaults.WetlandReed),
            Variant(SnowfieldAlpinePine, TerrainTileBuilderSurfaceScatterDefaults.SnowfieldAlpinePine),
            Variant(SnowClump, TerrainTileBuilderSurfaceScatterDefaults.SnowClump),
            Variant(MountainAlpinePine, TerrainTileBuilderSurfaceScatterDefaults.MountainAlpinePine));
    }

    internal static string ComputeHash(TerrainScatterRuleSetSnapshot snapshot)
    {
        var builder = new StringBuilder(3072);
        Append(builder, nameof(snapshot.NaturalDensityPenalty), snapshot.NaturalDensityPenalty);
        Append(builder, nameof(snapshot.BaseDensityPenalty), snapshot.BaseDensityPenalty);
        Append(builder, nameof(snapshot.Tree), snapshot.Tree);
        Append(builder, nameof(snapshot.Rock), snapshot.Rock);
        Append(builder, nameof(snapshot.Understory), snapshot.Understory);
        Append(builder, nameof(snapshot.ResourceNode), snapshot.ResourceNode);
        Append(builder, nameof(snapshot.HazardOutcrop), snapshot.HazardOutcrop);
        Append(builder, nameof(snapshot.TidalMangroveFlat), snapshot.TidalMangroveFlat);
        Append(builder, nameof(snapshot.LakeScatterZone), snapshot.LakeScatterZone);
        Append(builder, nameof(snapshot.TidalMangroveRoot), snapshot.TidalMangroveRoot);
        Append(builder, nameof(snapshot.LakeWaterLily), snapshot.LakeWaterLily);
        Append(builder, nameof(snapshot.LakeReed), snapshot.LakeReed);
        Append(builder, nameof(snapshot.GrassTuft), snapshot.GrassTuft);
        Append(builder, nameof(snapshot.CoastalMangroveRoot), snapshot.CoastalMangroveRoot);
        Append(builder, nameof(snapshot.CoastalPalm), snapshot.CoastalPalm);
        Append(builder, nameof(snapshot.Driftwood), snapshot.Driftwood);
        Append(builder, nameof(snapshot.OasisReed), snapshot.OasisReed);
        Append(builder, nameof(snapshot.DesertCactus), snapshot.DesertCactus);
        Append(builder, nameof(snapshot.DesertShrub), snapshot.DesertShrub);
        Append(builder, nameof(snapshot.WetlandMangroveRoot), snapshot.WetlandMangroveRoot);
        Append(builder, nameof(snapshot.WetlandReed), snapshot.WetlandReed);
        Append(builder, nameof(snapshot.SnowfieldAlpinePine), snapshot.SnowfieldAlpinePine);
        Append(builder, nameof(snapshot.SnowClump), snapshot.SnowClump);
        Append(builder, nameof(snapshot.MountainAlpinePine), snapshot.MountainAlpinePine);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string name, float value)
    {
        builder.Append(name).Append('=').Append(value.ToString("G9", CultureInfo.InvariantCulture)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainGameplayScatterRule rule)
    {
        builder.Append(name).Append('=')
            .Append(rule.MaxSlope.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinPrimary.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinSecondary.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinTemperature.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ProbabilityLow.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ProbabilityHigh.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.BaseScale.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ScaleJitter.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.TintLow.ToHtml()).Append('|')
            .Append(rule.TintHigh.ToHtml()).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainWaterZoneScatterRule rule)
    {
        builder.Append(name).Append('=')
            .Append(rule.MaxSlope.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinHeightOffset.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MaxHeightValue.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinPrimary.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinSecondary.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.Threshold.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ShorelineHeightOffset.ToString("G9", CultureInfo.InvariantCulture)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainScatterVariantRule rule)
    {
        builder.Append(name).Append('=')
            .Append(rule.ProbabilityLow.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ProbabilityHigh.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.BaseScale.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ScaleJitterFactor.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.TintLow.ToHtml()).Append('|')
            .Append(rule.TintHigh.ToHtml()).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainSurfaceNaturalScatterRule rule)
    {
        builder.Append(name).Append('=')
            .Append(rule.MaxSlope.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinMoisture.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinTemperature.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MaxRiver.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinTraversability.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.Probability.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.BaseScale.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ScaleJitter.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.TintLow.ToHtml()).Append('|')
            .Append(rule.TintHigh.ToHtml()).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainSurfaceRockScatterRule rule)
    {
        builder.Append(name).Append('=')
            .Append(rule.MinSlope.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinHeightAboveSea.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.MinHazardPotential.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.Probability.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.BaseScale.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ScaleJitter.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.TintLow.ToHtml()).Append('|')
            .Append(rule.TintHigh.ToHtml()).Append(';');
    }

    private static TerrainScatterVariantRule Variant(
        TerrainScatterVariantRuleResource? resource,
        TerrainScatterVariantRule fallback)
    {
        if (resource is null)
        {
            return fallback;
        }

        return new TerrainScatterVariantRule(
            resource.ProbabilityLow,
            resource.ProbabilityHigh,
            resource.BaseScale,
            resource.ScaleJitterFactor,
            resource.TintLow,
            resource.TintHigh);
    }
}
