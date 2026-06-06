namespace Dao.Terrain.Generation;

/// <summary>Threshold bounds used to validate world planning quality.</summary>
public readonly record struct TerrainWorldPlanningThresholds(
    int MinPointsOfInterest,
    int MinPointOfInterestKinds,
    int MinRoutes,
    int MinRouteKinds,
    float MinConnectedPointRatio,
    float MinConnectedSettlementRatio,
    int MinSettlementRoutes,
    float MinPointOfInterestWorldCoverage,
    float MinRouteWorldCoverage,
    float MinAverageRouteTraversability,
    float MinAverageRouteScenicPotential,
    int MinVillages,
    int MinTowns,
    int MinOasisHubs)
{
    public static TerrainWorldPlanningThresholds OpenWorldDefault { get; } = new(
        MinPointsOfInterest: 18,
        MinPointOfInterestKinds: 5,
        MinRoutes: 48,
        MinRouteKinds: 3,
        MinConnectedPointRatio: 0.95f,
        MinConnectedSettlementRatio: 0.95f,
        MinSettlementRoutes: 8,
        MinPointOfInterestWorldCoverage: 0.70f,
        MinRouteWorldCoverage: 0.70f,
        MinAverageRouteTraversability: 0.34f,
        MinAverageRouteScenicPotential: 0.20f,
        MinVillages: 2,
        MinTowns: 2,
        MinOasisHubs: 1);
}

/// <summary>Detailed statistics from analyzing a world plan's points of interest and routes.</summary>
public readonly record struct TerrainWorldPlanningReport(
    int PointOfInterestCount,
    int DistinctPointOfInterestKinds,
    int RouteCount,
    int DistinctRouteKinds,
    float ConnectedPointRatio,
    float ConnectedSettlementRatio,
    int SettlementRouteCount,
    float PointOfInterestWorldCoverage,
    float RouteWorldCoverage,
    float AveragePointScore,
    float AverageRouteCost,
    float AverageRouteScenicPotential,
    float AverageRouteTraversability,
    int SettlementCandidateCount,
    int VistaCount,
    int RiverCrossingCount,
    int MountainPassCount,
    int CoastalLandingCount,
    int ResourceGroveCount,
    int AncientSiteCount,
    int CanyonOverlookCount,
    int OasisCount,
    int VillageCount,
    int TownCount,
    int OasisHubCount,
    int PrimaryTrailCount,
    int RiverRoadCount,
    int RidgePassCount,
    int CoastalPathCount,
    int ScenicTrailCount);

/// <summary>Result of validating a world plan against planning thresholds.</summary>
public readonly record struct TerrainWorldPlanningGateResult(
    bool Passed,
    TerrainWorldPlanningReport Report,
    string Summary);
