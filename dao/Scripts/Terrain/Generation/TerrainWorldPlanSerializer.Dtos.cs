namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanSerializer
{
    private sealed class TerrainPlanDto
    {
        public string Contract { get; set; } = string.Empty;
        public string ApiContract { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public string GeneratorVersion { get; set; } = string.Empty;
        public int Seed { get; set; }
        public string ProfileHash { get; set; } = string.Empty;
        public string ScatterRuleSetHash { get; set; } = string.Empty;
        public string SettlementVisualRuleSetHash { get; set; } = string.Empty;
        public string PointOfInterestRuleSetHash { get; set; } = string.Empty;
        public string RouteRuleSetHash { get; set; } = string.Empty;
        public string ScenicLandmarkRuleSetHash { get; set; } = string.Empty;
        public TerrainVector2Dto? Center { get; set; }
        public float WorldSize { get; set; }
        public int GridResolution { get; set; }
        public TerrainRegionDto[] Regions { get; set; } = [];
        public TerrainPointDto[] PointsOfInterest { get; set; } = [];
        public TerrainRouteDto[] Routes { get; set; } = [];
        public TerrainPlanReportsDto? Reports { get; set; }
    }

    private sealed class TerrainPlanReportsDto
    {
        public TerrainQualityReportDto? Quality { get; set; }
        public TerrainPlanningReportDto? Planning { get; set; }
        public TerrainExperienceReportDto? Experience { get; set; }
    }

    private sealed class TerrainVector2Dto
    {
        public float X { get; set; }
        public float Z { get; set; }
    }

    private sealed class TerrainEnumDto
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    private sealed class TerrainRegionDto
    {
        public int GridX { get; set; }
        public int GridY { get; set; }
        public TerrainVector2Dto? World { get; set; }
        public float Height { get; set; }
        public float River { get; set; }
        public float ScenicPotential { get; set; }
        public float Traversability { get; set; }
        public float Exposure { get; set; }
        public float ResourcePotential { get; set; }
        public float HazardPotential { get; set; }
        public float EncounterPotential { get; set; }
        public TerrainEnumDto? Biome { get; set; }
        public TerrainEnumDto? Landscape { get; set; }
        public TerrainEnumDto? Region { get; set; }
    }

    private sealed class TerrainPointDto
    {
        public int Id { get; set; }
        public TerrainEnumDto? Kind { get; set; }
        public TerrainVector2Dto? World { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public float Score { get; set; }
        public float Height { get; set; }
        public float ScenicPotential { get; set; }
        public float Traversability { get; set; }
        public TerrainEnumDto? Biome { get; set; }
        public TerrainEnumDto? Landscape { get; set; }
        public TerrainEnumDto? SettlementTier { get; set; }
        public string? DebugName { get; set; }
    }

    private sealed class TerrainRouteDto
    {
        public int FromPointId { get; set; }
        public int ToPointId { get; set; }
        public TerrainEnumDto? Kind { get; set; }
        public float Cost { get; set; }
        public float AverageScenicPotential { get; set; }
        public float AverageTraversability { get; set; }
        public TerrainVector2Dto[] Waypoints { get; set; } = [];
    }

    private sealed class TerrainQualityReportDto
    {
        public int SampleCount { get; set; }
        public float WorldSize { get; set; }
        public float MinHeight { get; set; }
        public float MaxHeight { get; set; }
        public float AverageHeight { get; set; }
        public float LandRatio { get; set; }
        public float OceanRatio { get; set; }
        public float CoastRatio { get; set; }
        public float RiverRatio { get; set; }
        public float ScenicRatio { get; set; }
        public float TraversableLandRatio { get; set; }
        public int DistinctLandscapeKinds { get; set; }
        public int DistinctBiomeKinds { get; set; }
        public int OceanCount { get; set; }
        public int CoastCount { get; set; }
        public int LowlandCount { get; set; }
        public int WetlandCount { get; set; }
        public int ForestBasinCount { get; set; }
        public int RiverValleyCount { get; set; }
        public int CanyonCount { get; set; }
        public int HighlandsCount { get; set; }
        public int MountainMassifCount { get; set; }
        public int SnowfieldCount { get; set; }
        public int VistaPlateauCount { get; set; }
        public int LakeCount { get; set; }
        public int BiomeOceanCount { get; set; }
        public int BiomeCoastCount { get; set; }
        public int IslandCount { get; set; }
        public int PlainsCount { get; set; }
        public int GrasslandCount { get; set; }
        public int DesertCount { get; set; }
        public int OasisCount { get; set; }
        public int ForestCount { get; set; }
        public int BiomeWetlandCount { get; set; }
        public int HillsCount { get; set; }
        public int MountainsCount { get; set; }
        public int BiomeSnowfieldCount { get; set; }
        public int BiomeLakeCount { get; set; }
    }

    private sealed class TerrainPlanningReportDto
    {
        public int PointOfInterestCount { get; set; }
        public int DistinctPointOfInterestKinds { get; set; }
        public int RouteCount { get; set; }
        public int DistinctRouteKinds { get; set; }
        public float ConnectedPointRatio { get; set; }
        public float ConnectedSettlementRatio { get; set; }
        public int SettlementRouteCount { get; set; }
        public float PointOfInterestWorldCoverage { get; set; }
        public float RouteWorldCoverage { get; set; }
        public float AveragePointScore { get; set; }
        public float AverageRouteCost { get; set; }
        public float AverageRouteScenicPotential { get; set; }
        public float AverageRouteTraversability { get; set; }
        public int SettlementCandidateCount { get; set; }
        public int VistaCount { get; set; }
        public int RiverCrossingCount { get; set; }
        public int MountainPassCount { get; set; }
        public int CoastalLandingCount { get; set; }
        public int ResourceGroveCount { get; set; }
        public int AncientSiteCount { get; set; }
        public int CanyonOverlookCount { get; set; }
        public int OasisCount { get; set; }
        public int VillageCount { get; set; }
        public int TownCount { get; set; }
        public int OasisHubCount { get; set; }
        public int PrimaryTrailCount { get; set; }
        public int RiverRoadCount { get; set; }
        public int RidgePassCount { get; set; }
        public int CoastalPathCount { get; set; }
        public int ScenicTrailCount { get; set; }
    }

    private sealed class TerrainExperienceReportDto
    {
        public int RegionCount { get; set; }
        public float EncounterRichRegionRatio { get; set; }
        public float ResourceRichRegionRatio { get; set; }
        public float HazardRichRegionRatio { get; set; }
        public float AverageExposure { get; set; }
        public float AverageResourcePotential { get; set; }
        public float AverageHazardPotential { get; set; }
        public float AverageEncounterPotential { get; set; }
        public float RouteRhythmScore { get; set; }
        public float PointOfInterestValue { get; set; }
        public float RiskRewardBalance { get; set; }
        public float ScenicAnchorRatio { get; set; }
    }
}
