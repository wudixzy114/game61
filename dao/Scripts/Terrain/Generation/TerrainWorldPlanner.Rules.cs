namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static class TerrainWorldPlannerRules
    {
        public static readonly TerrainPoiThresholds PoiThresholds = new(
            SettlementCandidate: 0.58f,
            Vista: 0.64f,
            RiverCrossing: 0.62f,
            MountainPass: 0.54f,
            CoastalLanding: 0.50f,
            ResourceGrove: 0.58f,
            AncientSite: 0.70f,
            CanyonOverlook: 0.58f,
            Oasis: 0.54f);

        public static readonly TerrainPoiScoringWeights PoiScoring = new(
            SettlementStableFlatLand: 0.55f,
            SettlementMoisture: 0.12f,
            SettlementTemperature: 0.12f,
            SettlementRiver: 0.09f,
            SettlementScenic: 0.12f,
            SettlementPlainsGrassBonus: 0.10f,
            SettlementOasisBonus: 0.16f,
            VistaScenic: 0.82f,
            VistaElevation: 0.14f,
            VistaRarity: 0.04f,
            CrossingRiver: 0.55f,
            CrossingTraversability: 0.30f,
            CrossingLand: 0.15f,
            PassElevation: 0.30f,
            PassTraversability: 0.36f,
            PassScenic: 0.28f,
            PassRarity: 0.06f,
            CoastLand: 0.30f,
            CoastTraversability: 0.30f,
            CoastScenic: 0.28f,
            CoastRarity: 0.12f,
            ResourceMoisture: 0.34f,
            ResourceTraversability: 0.24f,
            ResourceLowElevation: 0.16f,
            ResourceRiver: 0.12f,
            ResourceRarity: 0.14f,
            AncientScenic: 0.50f,
            AncientElevation: 0.18f,
            AncientStableFlatLand: 0.16f,
            AncientRarity: 0.16f,
            CanyonScenic: 0.50f,
            CanyonRiver: 0.26f,
            CanyonElevation: 0.12f,
            CanyonRarity: 0.12f,
            OasisNaturalResource: 0.38f,
            OasisNaturalTraversability: 0.20f,
            OasisNaturalRiver: 0.18f,
            OasisNaturalScenic: 0.14f,
            OasisNaturalRarity: 0.10f,
            OasisStrategicWaterAccess: 0.30f,
            OasisStrategicResource: 0.26f,
            OasisStrategicTraversability: 0.18f,
            OasisStrategicScenic: 0.12f,
            OasisStrategicRarity: 0.14f,
            CandidateRarityLift: 0.025f);

        public static readonly TerrainPoiSelectionPolicy PoiSelection = new(
            MinPerKindLimit: 3,
            PerKindLimitRatio: 0.28f,
            MinDistanceCellMultiplier: 2.2f,
            MinDistanceChunkMultiplier: 0.70f,
            RequiredKindDistanceFactor: 0.36f,
            KindSweepDistanceFactor: 0.48f,
            CoverageAnchorTargetRatio: 0.44f,
            CoverageGainWeight: 12.0f,
            DistanceNoveltyWeight: 0.42f,
            CandidateScoreWeight: 0.30f,
            ExoticBiomeBonus: 0.08f);

        public static readonly TerrainSecondaryRoutePolicy SecondaryRoutes = new(
            MinDistanceChunks: 2.0f,
            IdealDistanceChunks: 18.0f,
            MaxDistanceChunks: 42.0f,
            MinCandidateTests: 64,
            CandidateTestMultiplier: 10);

        public static readonly TerrainSecondaryRoutePolicy SettlementRoutes = new(
            MinDistanceChunks: 1.5f,
            IdealDistanceChunks: 14.0f,
            MaxDistanceChunks: 38.0f,
            MinCandidateTests: 32,
            CandidateTestMultiplier: 8);

        public static readonly TerrainRouteScoreWeights SettlementRouteScoring = new(
            Endpoint: 0.22f,
            Scenic: 0.0f,
            Traversal: 0.18f,
            UnderConnected: 0.24f,
            KindVariety: 0.0f,
            TierImportance: 0.20f,
            TierVariety: 0.08f,
            Distance: 0.08f,
            SettlementBonus: 0.0f);

        public static readonly TerrainRouteScoreWeights SecondaryRouteScoring = new(
            Endpoint: 0.28f,
            Scenic: 0.26f,
            Traversal: 0.16f,
            UnderConnected: 0.18f,
            KindVariety: 0.06f,
            TierImportance: 0.0f,
            TierVariety: 0.0f,
            Distance: 0.06f,
            SettlementBonus: 0.08f);

        public static readonly TerrainPathCostPolicy PathCost = new(
            ImpassableWaterDepthHeightScaleRatio: 0.62f,
            DiagonalBaseCost: 1.4142f,
            OrthogonalBaseCost: 1.0f,
            TraversabilityPenaltyWeight: 4.5f,
            HeightDeltaPenaltyHeightScaleRatio: 0.18f,
            HeightDeltaPenaltyMax: 4.0f,
            RiverHighPenaltyThreshold: 0.72f,
            RiverHighPenalty: 1.4f,
            RiverPenaltyWeight: 0.38f,
            WaterPenaltyStart: 4.0f,
            WaterPenaltyBase: 5.8f,
            WaterPenaltyDepthScale: 90.0f,
            WaterPenaltyDepthMax: 5.5f,
            ScenicBonusWeight: 0.18f,
            MinimumScaledCost: 0.35f);

        public static readonly TerrainRouteClassificationPolicy RouteClassification = new(
            WaterPathThreshold: 0.12f,
            CoastPathThreshold: 0.32f,
            RiverRoadPrimaryThreshold: 0.55f,
            RidgePassPrimaryThreshold: 0.55f,
            ScenicTrailThreshold: 0.62f,
            RiverRoadSecondaryThreshold: 0.34f,
            RidgePassSecondaryThreshold: 0.34f);
    }

    private readonly record struct TerrainPoiThresholds(
        float SettlementCandidate,
        float Vista,
        float RiverCrossing,
        float MountainPass,
        float CoastalLanding,
        float ResourceGrove,
        float AncientSite,
        float CanyonOverlook,
        float Oasis);

    private readonly record struct TerrainPoiScoringWeights(
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

    private readonly record struct TerrainPoiSelectionPolicy(
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

    private readonly record struct TerrainSecondaryRoutePolicy(
        float MinDistanceChunks,
        float IdealDistanceChunks,
        float MaxDistanceChunks,
        int MinCandidateTests,
        int CandidateTestMultiplier);

    private readonly record struct TerrainRouteScoreWeights(
        float Endpoint,
        float Scenic,
        float Traversal,
        float UnderConnected,
        float KindVariety,
        float TierImportance,
        float TierVariety,
        float Distance,
        float SettlementBonus);

    private readonly record struct TerrainPathCostPolicy(
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

    private readonly record struct TerrainRouteClassificationPolicy(
        float WaterPathThreshold,
        float CoastPathThreshold,
        float RiverRoadPrimaryThreshold,
        float RidgePassPrimaryThreshold,
        float ScenicTrailThreshold,
        float RiverRoadSecondaryThreshold,
        float RidgePassSecondaryThreshold);
}
