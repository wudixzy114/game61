using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Optional data-driven route planning rule set for connectivity, path cost, and route classification.</summary>
[GlobalClass]
public partial class TerrainRouteRuleSet : Resource
{
    [ExportGroup("Secondary Routes")]
    [Export(PropertyHint.Range, "0,64,0.1")] public float SecondaryMinDistanceChunks { get; set; } = 2.0f;
    [Export(PropertyHint.Range, "0,64,0.1")] public float SecondaryIdealDistanceChunks { get; set; } = 18.0f;
    [Export(PropertyHint.Range, "0,128,0.1")] public float SecondaryMaxDistanceChunks { get; set; } = 42.0f;
    [Export(PropertyHint.Range, "0,512,1")] public int SecondaryMinCandidateTests { get; set; } = 64;
    [Export(PropertyHint.Range, "0,128,1")] public int SecondaryCandidateTestMultiplier { get; set; } = 10;

    [ExportGroup("Settlement Routes")]
    [Export(PropertyHint.Range, "0,64,0.1")] public float SettlementMinDistanceChunks { get; set; } = 1.5f;
    [Export(PropertyHint.Range, "0,64,0.1")] public float SettlementIdealDistanceChunks { get; set; } = 14.0f;
    [Export(PropertyHint.Range, "0,128,0.1")] public float SettlementMaxDistanceChunks { get; set; } = 38.0f;
    [Export(PropertyHint.Range, "0,512,1")] public int SettlementMinCandidateTests { get; set; } = 32;
    [Export(PropertyHint.Range, "0,128,1")] public int SettlementCandidateTestMultiplier { get; set; } = 8;
    [Export(PropertyHint.Range, "0,64,1")] public int MinimumSettlementConnectorRoutes { get; set; } = 8;

    [ExportGroup("Settlement Route Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementEndpointWeight { get; set; } = 0.22f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementScenicWeight { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementTraversalWeight { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementUnderConnectedWeight { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementKindVarietyWeight { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementTierImportanceWeight { get; set; } = 0.20f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementTierVarietyWeight { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementDistanceWeight { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SettlementBonusWeight { get; set; } = 0.0f;

    [ExportGroup("Secondary Route Scoring")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondaryEndpointWeight { get; set; } = 0.28f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondaryScenicWeight { get; set; } = 0.26f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondaryTraversalWeight { get; set; } = 0.16f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondaryUnderConnectedWeight { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondaryKindVarietyWeight { get; set; } = 0.06f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondaryTierImportanceWeight { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondaryTierVarietyWeight { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondaryDistanceWeight { get; set; } = 0.06f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float SecondarySettlementBonusWeight { get; set; } = 0.08f;

    [ExportGroup("Path Cost")]
    [Export(PropertyHint.Range, "0,2,0.001")] public float ImpassableWaterDepthHeightScaleRatio { get; set; } = 0.62f;
    [Export(PropertyHint.Range, "0,8,0.0001")] public float DiagonalBaseCost { get; set; } = 1.4142f;
    [Export(PropertyHint.Range, "0,8,0.0001")] public float OrthogonalBaseCost { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0,16,0.001")] public float TraversabilityPenaltyWeight { get; set; } = 4.5f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float HeightDeltaPenaltyHeightScaleRatio { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0,16,0.001")] public float HeightDeltaPenaltyMax { get; set; } = 4.0f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float RiverHighPenaltyThreshold { get; set; } = 0.72f;
    [Export(PropertyHint.Range, "0,8,0.001")] public float RiverHighPenalty { get; set; } = 1.4f;
    [Export(PropertyHint.Range, "0,8,0.001")] public float RiverPenaltyWeight { get; set; } = 0.38f;
    [Export(PropertyHint.Range, "0,64,0.01")] public float WaterPenaltyStart { get; set; } = 4.0f;
    [Export(PropertyHint.Range, "0,64,0.01")] public float WaterPenaltyBase { get; set; } = 5.8f;
    [Export(PropertyHint.Range, "0,256,0.01")] public float WaterPenaltyDepthScale { get; set; } = 90.0f;
    [Export(PropertyHint.Range, "0,16,0.001")] public float WaterPenaltyDepthMax { get; set; } = 5.5f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float ScenicBonusWeight { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0,2,0.001")] public float MinimumScaledCost { get; set; } = 0.35f;

    [ExportGroup("Classification")]
    [Export(PropertyHint.Range, "0,1,0.001")] public float WaterPathThreshold { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float CoastPathThreshold { get; set; } = 0.32f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float RiverRoadPrimaryThreshold { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float RidgePassPrimaryThreshold { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ScenicTrailThreshold { get; set; } = 0.62f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float RiverRoadSecondaryThreshold { get; set; } = 0.34f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float RidgePassSecondaryThreshold { get; set; } = 0.34f;

    /// <summary>Computes a stable content hash for this route planning rule set.</summary>
    public string StableHash()
    {
        return ComputeHash(
            CreateSecondaryRoutePolicy(),
            CreateSettlementRoutePolicy(),
            CreateSettlementRouteScoring(),
            CreateSecondaryRouteScoring(),
            CreatePathCostPolicy(),
            CreateClassificationPolicy(),
            MinimumSettlementConnectorRoutes);
    }

    internal static string ComputeHash(
        TerrainSecondaryRoutePolicy secondaryRoutes,
        TerrainSecondaryRoutePolicy settlementRoutes,
        TerrainRouteScoreWeights settlementRouteScoring,
        TerrainRouteScoreWeights secondaryRouteScoring,
        TerrainPathCostPolicy pathCost,
        TerrainRouteClassificationPolicy routeClassification,
        int minimumSettlementConnectorRoutes)
    {
        var builder = new StringBuilder(4096);
        Append(builder, nameof(TerrainSecondaryRoutePolicy) + ".Secondary", secondaryRoutes);
        Append(builder, nameof(TerrainSecondaryRoutePolicy) + ".Settlement", settlementRoutes);
        Append(builder, nameof(TerrainRouteScoreWeights) + ".Settlement", settlementRouteScoring);
        Append(builder, nameof(TerrainRouteScoreWeights) + ".Secondary", secondaryRouteScoring);
        Append(builder, nameof(TerrainPathCostPolicy), pathCost);
        Append(builder, nameof(TerrainRouteClassificationPolicy), routeClassification);
        builder.Append(nameof(MinimumSettlementConnectorRoutes)).Append('=').Append(Int(minimumSettlementConnectorRoutes)).Append(';');
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal TerrainSecondaryRoutePolicy CreateSecondaryRoutePolicy()
    {
        return new TerrainSecondaryRoutePolicy(
            SecondaryMinDistanceChunks,
            SecondaryIdealDistanceChunks,
            SecondaryMaxDistanceChunks,
            SecondaryMinCandidateTests,
            SecondaryCandidateTestMultiplier);
    }

    internal TerrainSecondaryRoutePolicy CreateSettlementRoutePolicy()
    {
        return new TerrainSecondaryRoutePolicy(
            SettlementMinDistanceChunks,
            SettlementIdealDistanceChunks,
            SettlementMaxDistanceChunks,
            SettlementMinCandidateTests,
            SettlementCandidateTestMultiplier);
    }

    internal TerrainRouteScoreWeights CreateSettlementRouteScoring()
    {
        return new TerrainRouteScoreWeights(
            SettlementEndpointWeight,
            SettlementScenicWeight,
            SettlementTraversalWeight,
            SettlementUnderConnectedWeight,
            SettlementKindVarietyWeight,
            SettlementTierImportanceWeight,
            SettlementTierVarietyWeight,
            SettlementDistanceWeight,
            SettlementBonusWeight);
    }

    internal TerrainRouteScoreWeights CreateSecondaryRouteScoring()
    {
        return new TerrainRouteScoreWeights(
            SecondaryEndpointWeight,
            SecondaryScenicWeight,
            SecondaryTraversalWeight,
            SecondaryUnderConnectedWeight,
            SecondaryKindVarietyWeight,
            SecondaryTierImportanceWeight,
            SecondaryTierVarietyWeight,
            SecondaryDistanceWeight,
            SecondarySettlementBonusWeight);
    }

    internal TerrainPathCostPolicy CreatePathCostPolicy()
    {
        return new TerrainPathCostPolicy(
            ImpassableWaterDepthHeightScaleRatio,
            DiagonalBaseCost,
            OrthogonalBaseCost,
            TraversabilityPenaltyWeight,
            HeightDeltaPenaltyHeightScaleRatio,
            HeightDeltaPenaltyMax,
            RiverHighPenaltyThreshold,
            RiverHighPenalty,
            RiverPenaltyWeight,
            WaterPenaltyStart,
            WaterPenaltyBase,
            WaterPenaltyDepthScale,
            WaterPenaltyDepthMax,
            ScenicBonusWeight,
            MinimumScaledCost);
    }

    internal TerrainRouteClassificationPolicy CreateClassificationPolicy()
    {
        return new TerrainRouteClassificationPolicy(
            WaterPathThreshold,
            CoastPathThreshold,
            RiverRoadPrimaryThreshold,
            RidgePassPrimaryThreshold,
            ScenicTrailThreshold,
            RiverRoadSecondaryThreshold,
            RidgePassSecondaryThreshold);
    }

    private static void Append(StringBuilder builder, string name, TerrainSecondaryRoutePolicy value)
    {
        builder.Append(name).Append('=')
            .Append(Float(value.MinDistanceChunks)).Append('|')
            .Append(Float(value.IdealDistanceChunks)).Append('|')
            .Append(Float(value.MaxDistanceChunks)).Append('|')
            .Append(Int(value.MinCandidateTests)).Append('|')
            .Append(Int(value.CandidateTestMultiplier)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainRouteScoreWeights value)
    {
        builder.Append(name).Append('=')
            .Append(Float(value.Endpoint)).Append('|')
            .Append(Float(value.Scenic)).Append('|')
            .Append(Float(value.Traversal)).Append('|')
            .Append(Float(value.UnderConnected)).Append('|')
            .Append(Float(value.KindVariety)).Append('|')
            .Append(Float(value.TierImportance)).Append('|')
            .Append(Float(value.TierVariety)).Append('|')
            .Append(Float(value.Distance)).Append('|')
            .Append(Float(value.SettlementBonus)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainPathCostPolicy value)
    {
        builder.Append(name).Append('=')
            .Append(Float(value.ImpassableWaterDepthHeightScaleRatio)).Append('|')
            .Append(Float(value.DiagonalBaseCost)).Append('|')
            .Append(Float(value.OrthogonalBaseCost)).Append('|')
            .Append(Float(value.TraversabilityPenaltyWeight)).Append('|')
            .Append(Float(value.HeightDeltaPenaltyHeightScaleRatio)).Append('|')
            .Append(Float(value.HeightDeltaPenaltyMax)).Append('|')
            .Append(Float(value.RiverHighPenaltyThreshold)).Append('|')
            .Append(Float(value.RiverHighPenalty)).Append('|')
            .Append(Float(value.RiverPenaltyWeight)).Append('|')
            .Append(Float(value.WaterPenaltyStart)).Append('|')
            .Append(Float(value.WaterPenaltyBase)).Append('|')
            .Append(Float(value.WaterPenaltyDepthScale)).Append('|')
            .Append(Float(value.WaterPenaltyDepthMax)).Append('|')
            .Append(Float(value.ScenicBonusWeight)).Append('|')
            .Append(Float(value.MinimumScaledCost)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, TerrainRouteClassificationPolicy value)
    {
        builder.Append(name).Append('=')
            .Append(Float(value.WaterPathThreshold)).Append('|')
            .Append(Float(value.CoastPathThreshold)).Append('|')
            .Append(Float(value.RiverRoadPrimaryThreshold)).Append('|')
            .Append(Float(value.RidgePassPrimaryThreshold)).Append('|')
            .Append(Float(value.ScenicTrailThreshold)).Append('|')
            .Append(Float(value.RiverRoadSecondaryThreshold)).Append('|')
            .Append(Float(value.RidgePassSecondaryThreshold)).Append(';');
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
