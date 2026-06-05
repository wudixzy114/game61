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
}
