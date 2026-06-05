namespace Dao.Terrain.Generation;

internal readonly record struct TerrainPoiThresholds(
    float SettlementCandidate,
    float Vista,
    float RiverCrossing,
    float MountainPass,
    float CoastalLanding,
    float ResourceGrove,
    float AncientSite,
    float CanyonOverlook,
    float Oasis);

internal readonly record struct TerrainPoiScoringWeights(
    float SettlementStableFlatLand,
    float SettlementMoisture,
    float SettlementTemperature,
    float SettlementRiver,
    float SettlementScenic,
    float SettlementPlainsGrassBonus,
    float SettlementOasisBonus,
    float VistaScenic,
    float VistaElevation,
    float VistaRarity,
    float CrossingRiver,
    float CrossingTraversability,
    float CrossingLand,
    float PassElevation,
    float PassTraversability,
    float PassScenic,
    float PassRarity,
    float CoastLand,
    float CoastTraversability,
    float CoastScenic,
    float CoastRarity,
    float ResourceMoisture,
    float ResourceTraversability,
    float ResourceLowElevation,
    float ResourceRiver,
    float ResourceRarity,
    float AncientScenic,
    float AncientElevation,
    float AncientStableFlatLand,
    float AncientRarity,
    float CanyonScenic,
    float CanyonRiver,
    float CanyonElevation,
    float CanyonRarity,
    float OasisNaturalResource,
    float OasisNaturalTraversability,
    float OasisNaturalRiver,
    float OasisNaturalScenic,
    float OasisNaturalRarity,
    float OasisStrategicWaterAccess,
    float OasisStrategicResource,
    float OasisStrategicTraversability,
    float OasisStrategicScenic,
    float OasisStrategicRarity,
    float CandidateRarityLift);

internal readonly record struct TerrainPoiSelectionPolicy(
    int MinPerKindLimit,
    float PerKindLimitRatio,
    float MinDistanceCellMultiplier,
    float MinDistanceChunkMultiplier,
    float RequiredKindDistanceFactor,
    float KindSweepDistanceFactor,
    float CoverageAnchorTargetRatio,
    float CoverageGainWeight,
    float DistanceNoveltyWeight,
    float CandidateScoreWeight,
    float ExoticBiomeBonus);

internal readonly record struct TerrainSettlementTierScoring(
    float TownThreshold,
    float CandidateScoreWeight,
    float TraversabilityWeight,
    float ResourceWeight,
    float ScenicWeight,
    float BiomeWeight,
    float PlainsBiomeScore,
    float GrasslandBiomeScore,
    float OasisBiomeScore,
    float ForestBiomeScore,
    float CoastBiomeScore,
    float FallbackBiomeScore);

internal readonly record struct TerrainPointOfInterestRuleSetSnapshot(
    TerrainPoiThresholds Thresholds,
    TerrainPoiScoringWeights Scoring,
    TerrainPoiSelectionPolicy Selection,
    TerrainSettlementTierScoring SettlementTier)
{
    public string StableHash()
    {
        return Dao.Terrain.TerrainPointOfInterestRuleSet.ComputeHash(
            Thresholds,
            Scoring,
            Selection,
            SettlementTier);
    }
}

internal readonly record struct TerrainSecondaryRoutePolicy(
    float MinDistanceChunks,
    float IdealDistanceChunks,
    float MaxDistanceChunks,
    int MinCandidateTests,
    int CandidateTestMultiplier);

internal readonly record struct TerrainRouteScoreWeights(
    float Endpoint,
    float Scenic,
    float Traversal,
    float UnderConnected,
    float KindVariety,
    float TierImportance,
    float TierVariety,
    float Distance,
    float SettlementBonus);

internal readonly record struct TerrainPathCostPolicy(
    float ImpassableWaterDepthHeightScaleRatio,
    float DiagonalBaseCost,
    float OrthogonalBaseCost,
    float TraversabilityPenaltyWeight,
    float HeightDeltaPenaltyHeightScaleRatio,
    float HeightDeltaPenaltyMax,
    float RiverHighPenaltyThreshold,
    float RiverHighPenalty,
    float RiverPenaltyWeight,
    float WaterPenaltyStart,
    float WaterPenaltyBase,
    float WaterPenaltyDepthScale,
    float WaterPenaltyDepthMax,
    float ScenicBonusWeight,
    float MinimumScaledCost);

internal readonly record struct TerrainRouteClassificationPolicy(
    float WaterPathThreshold,
    float CoastPathThreshold,
    float RiverRoadPrimaryThreshold,
    float RidgePassPrimaryThreshold,
    float ScenicTrailThreshold,
    float RiverRoadSecondaryThreshold,
    float RidgePassSecondaryThreshold);

internal readonly record struct TerrainRouteRuleSetSnapshot(
    TerrainSecondaryRoutePolicy SecondaryRoutes,
    TerrainSecondaryRoutePolicy SettlementRoutes,
    TerrainRouteScoreWeights SettlementRouteScoring,
    TerrainRouteScoreWeights SecondaryRouteScoring,
    TerrainPathCostPolicy PathCost,
    TerrainRouteClassificationPolicy RouteClassification,
    int MinimumSettlementConnectorRoutes)
{
    public string StableHash()
    {
        return Dao.Terrain.TerrainRouteRuleSet.ComputeHash(
            SecondaryRoutes,
            SettlementRoutes,
            SettlementRouteScoring,
            SecondaryRouteScoring,
            PathCost,
            RouteClassification,
            MinimumSettlementConnectorRoutes);
    }
}
