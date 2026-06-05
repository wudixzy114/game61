using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Optional data-driven POI planning rule set for thresholds, scoring, and coverage selection.</summary>
[GlobalClass]
public partial class TerrainPointOfInterestRuleSet : Resource
{
    [ExportGroup("Thresholds")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float SettlementCandidateThreshold { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float VistaThreshold { get; set; } = 0.64f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float RiverCrossingThreshold { get; set; } = 0.62f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float MountainPassThreshold { get; set; } = 0.54f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float CoastalLandingThreshold { get; set; } = 0.50f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ResourceGroveThreshold { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float AncientSiteThreshold { get; set; } = 0.70f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float CanyonOverlookThreshold { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float OasisThreshold { get; set; } = 0.54f;

    [ExportGroup("Settlement Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementStableFlatLandWeight { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementMoistureWeight { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementTemperatureWeight { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementRiverWeight { get; set; } = 0.09f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementScenicWeight { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementPlainsGrassBonus { get; set; } = 0.10f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementOasisBonus { get; set; } = 0.16f;

    [ExportGroup("Vista Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float VistaScenicWeight { get; set; } = 0.82f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float VistaElevationWeight { get; set; } = 0.14f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float VistaRarityWeight { get; set; } = 0.04f;

    [ExportGroup("Crossing Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float CrossingRiverWeight { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float CrossingTraversabilityWeight { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float CrossingLandWeight { get; set; } = 0.15f;

    [ExportGroup("Mountain Pass Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float PassElevationWeight { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float PassTraversabilityWeight { get; set; } = 0.36f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float PassScenicWeight { get; set; } = 0.28f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float PassRarityWeight { get; set; } = 0.06f;

    [ExportGroup("Coastal Landing Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float CoastLandWeight { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float CoastTraversabilityWeight { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float CoastScenicWeight { get; set; } = 0.28f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float CoastRarityWeight { get; set; } = 0.12f;

    [ExportGroup("Resource Grove Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float ResourceMoistureWeight { get; set; } = 0.34f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float ResourceTraversabilityWeight { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float ResourceLowElevationWeight { get; set; } = 0.16f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float ResourceRiverWeight { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float ResourceRarityWeight { get; set; } = 0.14f;

    [ExportGroup("Ancient Site Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float AncientScenicWeight { get; set; } = 0.50f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float AncientElevationWeight { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float AncientStableFlatLandWeight { get; set; } = 0.16f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float AncientRarityWeight { get; set; } = 0.16f;

    [ExportGroup("Canyon Overlook Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float CanyonScenicWeight { get; set; } = 0.50f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float CanyonRiverWeight { get; set; } = 0.26f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float CanyonElevationWeight { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float CanyonRarityWeight { get; set; } = 0.12f;

    [ExportGroup("Oasis Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisNaturalResourceWeight { get; set; } = 0.38f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisNaturalTraversabilityWeight { get; set; } = 0.20f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisNaturalRiverWeight { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisNaturalScenicWeight { get; set; } = 0.14f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisNaturalRarityWeight { get; set; } = 0.10f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisStrategicWaterAccessWeight { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisStrategicResourceWeight { get; set; } = 0.26f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisStrategicTraversabilityWeight { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisStrategicScenicWeight { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float OasisStrategicRarityWeight { get; set; } = 0.14f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float CandidateRarityLift { get; set; } = 0.025f;

    [ExportGroup("Selection")]
    [Export(PropertyHint.Range, "0,64,1")] public int MinPerKindLimit { get; set; } = 3;
    [Export(PropertyHint.Range, "0,1,0.001")] public float PerKindLimitRatio { get; set; } = 0.28f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float MinDistanceCellMultiplier { get; set; } = 2.2f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float MinDistanceChunkMultiplier { get; set; } = 0.70f;
    [Export(PropertyHint.Range, "0,4,0.01")] public float RequiredKindDistanceFactor { get; set; } = 0.36f;
    [Export(PropertyHint.Range, "0,4,0.01")] public float KindSweepDistanceFactor { get; set; } = 0.48f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float CoverageAnchorTargetRatio { get; set; } = 0.44f;
    [Export(PropertyHint.Range, "0,32,0.01")] public float CoverageGainWeight { get; set; } = 12.0f;
    [Export(PropertyHint.Range, "0,4,0.01")] public float DistanceNoveltyWeight { get; set; } = 0.42f;
    [Export(PropertyHint.Range, "0,4,0.01")] public float CandidateScoreWeight { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,2,0.01")] public float ExoticBiomeBonus { get; set; } = 0.08f;

    [ExportGroup("Settlement Tiers")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float TownThreshold { get; set; } = 0.84f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float TownCandidateScoreWeight { get; set; } = 0.40f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float TownTraversabilityWeight { get; set; } = 0.22f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float TownResourceWeight { get; set; } = 0.20f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float TownScenicWeight { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float TownBiomeWeight { get; set; } = 0.10f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float PlainsBiomeScore { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float GrasslandBiomeScore { get; set; } = 0.92f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float OasisBiomeScore { get; set; } = 0.88f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ForestBiomeScore { get; set; } = 0.68f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float CoastBiomeScore { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float FallbackBiomeScore { get; set; } = 0.36f;

    /// <summary>Computes a stable content hash for this POI planning rule set.</summary>
    public string StableHash()
    {
        return ComputeHash(
            CreateThresholds(),
            CreateScoring(),
            CreateSelection(),
            CreateSettlementTierScoring());
    }

    internal static string ComputeHash(
        TerrainPoiThresholds thresholds,
        TerrainPoiScoringWeights scoring,
        TerrainPoiSelectionPolicy selection,
        TerrainSettlementTierScoring settlementTier)
    {
        var builder = new StringBuilder(4096);
        Append(builder, nameof(TerrainPoiThresholds), thresholds);
        Append(builder, nameof(TerrainPoiScoringWeights), scoring);
        Append(builder, nameof(TerrainPoiSelectionPolicy), selection);
        Append(builder, nameof(TerrainSettlementTierScoring), settlementTier);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal TerrainPoiThresholds CreateThresholds()
    {
        return new TerrainPoiThresholds(
            SettlementCandidateThreshold,
            VistaThreshold,
            RiverCrossingThreshold,
            MountainPassThreshold,
            CoastalLandingThreshold,
            ResourceGroveThreshold,
            AncientSiteThreshold,
            CanyonOverlookThreshold,
            OasisThreshold);
    }

    internal TerrainPoiScoringWeights CreateScoring()
    {
        return new TerrainPoiScoringWeights(
            SettlementStableFlatLandWeight,
            SettlementMoistureWeight,
            SettlementTemperatureWeight,
            SettlementRiverWeight,
            SettlementScenicWeight,
            SettlementPlainsGrassBonus,
            SettlementOasisBonus,
            VistaScenicWeight,
            VistaElevationWeight,
            VistaRarityWeight,
            CrossingRiverWeight,
            CrossingTraversabilityWeight,
            CrossingLandWeight,
            PassElevationWeight,
            PassTraversabilityWeight,
            PassScenicWeight,
            PassRarityWeight,
            CoastLandWeight,
            CoastTraversabilityWeight,
            CoastScenicWeight,
            CoastRarityWeight,
            ResourceMoistureWeight,
            ResourceTraversabilityWeight,
            ResourceLowElevationWeight,
            ResourceRiverWeight,
            ResourceRarityWeight,
            AncientScenicWeight,
            AncientElevationWeight,
            AncientStableFlatLandWeight,
            AncientRarityWeight,
            CanyonScenicWeight,
            CanyonRiverWeight,
            CanyonElevationWeight,
            CanyonRarityWeight,
            OasisNaturalResourceWeight,
            OasisNaturalTraversabilityWeight,
            OasisNaturalRiverWeight,
            OasisNaturalScenicWeight,
            OasisNaturalRarityWeight,
            OasisStrategicWaterAccessWeight,
            OasisStrategicResourceWeight,
            OasisStrategicTraversabilityWeight,
            OasisStrategicScenicWeight,
            OasisStrategicRarityWeight,
            CandidateRarityLift);
    }

    internal TerrainPoiSelectionPolicy CreateSelection()
    {
        return new TerrainPoiSelectionPolicy(
            MinPerKindLimit,
            PerKindLimitRatio,
            MinDistanceCellMultiplier,
            MinDistanceChunkMultiplier,
            RequiredKindDistanceFactor,
            KindSweepDistanceFactor,
            CoverageAnchorTargetRatio,
            CoverageGainWeight,
            DistanceNoveltyWeight,
            CandidateScoreWeight,
            ExoticBiomeBonus);
    }

    internal TerrainSettlementTierScoring CreateSettlementTierScoring()
    {
        return new TerrainSettlementTierScoring(
            TownThreshold,
            TownCandidateScoreWeight,
            TownTraversabilityWeight,
            TownResourceWeight,
            TownScenicWeight,
            TownBiomeWeight,
            PlainsBiomeScore,
            GrasslandBiomeScore,
            OasisBiomeScore,
            ForestBiomeScore,
            CoastBiomeScore,
            FallbackBiomeScore);
    }

    private static void Append(StringBuilder builder, string name, TerrainPoiThresholds value)
    {
        builder.Append(name).Append('=')
            .Append(Float(value.SettlementCandidate)).Append('|')
            .Append(Float(value.Vista)).Append('|')
            .Append(Float(value.RiverCrossing)).Append('|')
            .Append(Float(value.MountainPass)).Append('|')
            .Append(Float(value.CoastalLanding)).Append('|')
            .Append(Float(value.ResourceGrove)).Append('|')
            .Append(Float(value.AncientSite)).Append('|')
            .Append(Float(value.CanyonOverlook)).Append('|')
            .Append(Float(value.Oasis)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainPoiScoringWeights value)
    {
        builder.Append(name).Append('=')
            .Append(Float(value.SettlementStableFlatLand)).Append('|')
            .Append(Float(value.SettlementMoisture)).Append('|')
            .Append(Float(value.SettlementTemperature)).Append('|')
            .Append(Float(value.SettlementRiver)).Append('|')
            .Append(Float(value.SettlementScenic)).Append('|')
            .Append(Float(value.SettlementPlainsGrassBonus)).Append('|')
            .Append(Float(value.SettlementOasisBonus)).Append('|')
            .Append(Float(value.VistaScenic)).Append('|')
            .Append(Float(value.VistaElevation)).Append('|')
            .Append(Float(value.VistaRarity)).Append('|')
            .Append(Float(value.CrossingRiver)).Append('|')
            .Append(Float(value.CrossingTraversability)).Append('|')
            .Append(Float(value.CrossingLand)).Append('|')
            .Append(Float(value.PassElevation)).Append('|')
            .Append(Float(value.PassTraversability)).Append('|')
            .Append(Float(value.PassScenic)).Append('|')
            .Append(Float(value.PassRarity)).Append('|')
            .Append(Float(value.CoastLand)).Append('|')
            .Append(Float(value.CoastTraversability)).Append('|')
            .Append(Float(value.CoastScenic)).Append('|')
            .Append(Float(value.CoastRarity)).Append('|')
            .Append(Float(value.ResourceMoisture)).Append('|')
            .Append(Float(value.ResourceTraversability)).Append('|')
            .Append(Float(value.ResourceLowElevation)).Append('|')
            .Append(Float(value.ResourceRiver)).Append('|')
            .Append(Float(value.ResourceRarity)).Append('|')
            .Append(Float(value.AncientScenic)).Append('|')
            .Append(Float(value.AncientElevation)).Append('|')
            .Append(Float(value.AncientStableFlatLand)).Append('|')
            .Append(Float(value.AncientRarity)).Append('|')
            .Append(Float(value.CanyonScenic)).Append('|')
            .Append(Float(value.CanyonRiver)).Append('|')
            .Append(Float(value.CanyonElevation)).Append('|')
            .Append(Float(value.CanyonRarity)).Append('|')
            .Append(Float(value.OasisNaturalResource)).Append('|')
            .Append(Float(value.OasisNaturalTraversability)).Append('|')
            .Append(Float(value.OasisNaturalRiver)).Append('|')
            .Append(Float(value.OasisNaturalScenic)).Append('|')
            .Append(Float(value.OasisNaturalRarity)).Append('|')
            .Append(Float(value.OasisStrategicWaterAccess)).Append('|')
            .Append(Float(value.OasisStrategicResource)).Append('|')
            .Append(Float(value.OasisStrategicTraversability)).Append('|')
            .Append(Float(value.OasisStrategicScenic)).Append('|')
            .Append(Float(value.OasisStrategicRarity)).Append('|')
            .Append(Float(value.CandidateRarityLift)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainPoiSelectionPolicy value)
    {
        builder.Append(name).Append('=')
            .Append(Int(value.MinPerKindLimit)).Append('|')
            .Append(Float(value.PerKindLimitRatio)).Append('|')
            .Append(Float(value.MinDistanceCellMultiplier)).Append('|')
            .Append(Float(value.MinDistanceChunkMultiplier)).Append('|')
            .Append(Float(value.RequiredKindDistanceFactor)).Append('|')
            .Append(Float(value.KindSweepDistanceFactor)).Append('|')
            .Append(Float(value.CoverageAnchorTargetRatio)).Append('|')
            .Append(Float(value.CoverageGainWeight)).Append('|')
            .Append(Float(value.DistanceNoveltyWeight)).Append('|')
            .Append(Float(value.CandidateScoreWeight)).Append('|')
            .Append(Float(value.ExoticBiomeBonus)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainSettlementTierScoring value)
    {
        builder.Append(name).Append('=')
            .Append(Float(value.TownThreshold)).Append('|')
            .Append(Float(value.CandidateScoreWeight)).Append('|')
            .Append(Float(value.TraversabilityWeight)).Append('|')
            .Append(Float(value.ResourceWeight)).Append('|')
            .Append(Float(value.ScenicWeight)).Append('|')
            .Append(Float(value.BiomeWeight)).Append('|')
            .Append(Float(value.PlainsBiomeScore)).Append('|')
            .Append(Float(value.GrasslandBiomeScore)).Append('|')
            .Append(Float(value.OasisBiomeScore)).Append('|')
            .Append(Float(value.ForestBiomeScore)).Append('|')
            .Append(Float(value.CoastBiomeScore)).Append('|')
            .Append(Float(value.FallbackBiomeScore)).Append(';');
    }

    private static string Float(float value)
    {
        return value.ToString("G9", CultureInfo.InvariantCulture);
    }

    private static string Int(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
