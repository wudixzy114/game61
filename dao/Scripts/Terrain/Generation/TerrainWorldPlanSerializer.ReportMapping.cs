namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanSerializer
{
    private static TerrainQualityReportDto ToDto(TerrainQualityReport report)
    {
        return new TerrainQualityReportDto
        {
            SampleCount = report.SampleCount,
            WorldSize = report.WorldSize,
            MinHeight = report.MinHeight,
            MaxHeight = report.MaxHeight,
            AverageHeight = report.AverageHeight,
            LandRatio = report.LandRatio,
            OceanRatio = report.OceanRatio,
            CoastRatio = report.CoastRatio,
            RiverRatio = report.RiverRatio,
            ScenicRatio = report.ScenicRatio,
            TraversableLandRatio = report.TraversableLandRatio,
            DistinctLandscapeKinds = report.DistinctLandscapeKinds,
            DistinctBiomeKinds = report.DistinctBiomeKinds,
            OceanCount = report.OceanCount,
            CoastCount = report.CoastCount,
            LowlandCount = report.LowlandCount,
            WetlandCount = report.WetlandCount,
            ForestBasinCount = report.ForestBasinCount,
            RiverValleyCount = report.RiverValleyCount,
            CanyonCount = report.CanyonCount,
            HighlandsCount = report.HighlandsCount,
            MountainMassifCount = report.MountainMassifCount,
            SnowfieldCount = report.SnowfieldCount,
            VistaPlateauCount = report.VistaPlateauCount,
            LakeCount = report.LakeCount,
            BiomeOceanCount = report.BiomeOceanCount,
            BiomeCoastCount = report.BiomeCoastCount,
            IslandCount = report.IslandCount,
            PlainsCount = report.PlainsCount,
            GrasslandCount = report.GrasslandCount,
            DesertCount = report.DesertCount,
            OasisCount = report.OasisCount,
            ForestCount = report.ForestCount,
            BiomeWetlandCount = report.BiomeWetlandCount,
            HillsCount = report.HillsCount,
            MountainsCount = report.MountainsCount,
            BiomeSnowfieldCount = report.BiomeSnowfieldCount,
            BiomeLakeCount = report.BiomeLakeCount
        };
    }

    private static TerrainQualityReport FromDto(TerrainQualityReportDto report)
    {
        return new TerrainQualityReport(
            report.SampleCount,
            report.WorldSize,
            report.MinHeight,
            report.MaxHeight,
            report.AverageHeight,
            report.LandRatio,
            report.OceanRatio,
            report.CoastRatio,
            report.RiverRatio,
            report.ScenicRatio,
            report.TraversableLandRatio,
            report.DistinctLandscapeKinds,
            report.DistinctBiomeKinds,
            report.OceanCount,
            report.CoastCount,
            report.LowlandCount,
            report.WetlandCount,
            report.ForestBasinCount,
            report.RiverValleyCount,
            report.CanyonCount,
            report.HighlandsCount,
            report.MountainMassifCount,
            report.SnowfieldCount,
            report.VistaPlateauCount,
            report.LakeCount,
            report.BiomeOceanCount,
            report.BiomeCoastCount,
            report.IslandCount,
            report.PlainsCount,
            report.GrasslandCount,
            report.DesertCount,
            report.OasisCount,
            report.ForestCount,
            report.BiomeWetlandCount,
            report.HillsCount,
            report.MountainsCount,
            report.BiomeSnowfieldCount,
            report.BiomeLakeCount);
    }

    private static TerrainPlanningReportDto ToDto(TerrainWorldPlanningReport report)
    {
        return new TerrainPlanningReportDto
        {
            PointOfInterestCount = report.PointOfInterestCount,
            DistinctPointOfInterestKinds = report.DistinctPointOfInterestKinds,
            RouteCount = report.RouteCount,
            DistinctRouteKinds = report.DistinctRouteKinds,
            ConnectedPointRatio = report.ConnectedPointRatio,
            ConnectedSettlementRatio = report.ConnectedSettlementRatio,
            SettlementRouteCount = report.SettlementRouteCount,
            PointOfInterestWorldCoverage = report.PointOfInterestWorldCoverage,
            RouteWorldCoverage = report.RouteWorldCoverage,
            AveragePointScore = report.AveragePointScore,
            AverageRouteCost = report.AverageRouteCost,
            AverageRouteScenicPotential = report.AverageRouteScenicPotential,
            AverageRouteTraversability = report.AverageRouteTraversability,
            SettlementCandidateCount = report.SettlementCandidateCount,
            VistaCount = report.VistaCount,
            RiverCrossingCount = report.RiverCrossingCount,
            MountainPassCount = report.MountainPassCount,
            CoastalLandingCount = report.CoastalLandingCount,
            ResourceGroveCount = report.ResourceGroveCount,
            AncientSiteCount = report.AncientSiteCount,
            CanyonOverlookCount = report.CanyonOverlookCount,
            OasisCount = report.OasisCount,
            VillageCount = report.VillageCount,
            TownCount = report.TownCount,
            OasisHubCount = report.OasisHubCount,
            PrimaryTrailCount = report.PrimaryTrailCount,
            RiverRoadCount = report.RiverRoadCount,
            RidgePassCount = report.RidgePassCount,
            CoastalPathCount = report.CoastalPathCount,
            ScenicTrailCount = report.ScenicTrailCount
        };
    }

    private static TerrainWorldPlanningReport FromDto(TerrainPlanningReportDto report)
    {
        return new TerrainWorldPlanningReport(
            report.PointOfInterestCount,
            report.DistinctPointOfInterestKinds,
            report.RouteCount,
            report.DistinctRouteKinds,
            report.ConnectedPointRatio,
            report.ConnectedSettlementRatio,
            report.SettlementRouteCount,
            report.PointOfInterestWorldCoverage,
            report.RouteWorldCoverage,
            report.AveragePointScore,
            report.AverageRouteCost,
            report.AverageRouteScenicPotential,
            report.AverageRouteTraversability,
            report.SettlementCandidateCount,
            report.VistaCount,
            report.RiverCrossingCount,
            report.MountainPassCount,
            report.CoastalLandingCount,
            report.ResourceGroveCount,
            report.AncientSiteCount,
            report.CanyonOverlookCount,
            report.OasisCount,
            report.VillageCount,
            report.TownCount,
            report.OasisHubCount,
            report.PrimaryTrailCount,
            report.RiverRoadCount,
            report.RidgePassCount,
            report.CoastalPathCount,
            report.ScenicTrailCount);
    }

    private static TerrainExperienceReportDto ToDto(TerrainExperienceReport report)
    {
        return new TerrainExperienceReportDto
        {
            RegionCount = report.RegionCount,
            EncounterRichRegionRatio = report.EncounterRichRegionRatio,
            ResourceRichRegionRatio = report.ResourceRichRegionRatio,
            HazardRichRegionRatio = report.HazardRichRegionRatio,
            AverageExposure = report.AverageExposure,
            AverageResourcePotential = report.AverageResourcePotential,
            AverageHazardPotential = report.AverageHazardPotential,
            AverageEncounterPotential = report.AverageEncounterPotential,
            RouteRhythmScore = report.RouteRhythmScore,
            PointOfInterestValue = report.PointOfInterestValue,
            RiskRewardBalance = report.RiskRewardBalance,
            ScenicAnchorRatio = report.ScenicAnchorRatio
        };
    }

    private static TerrainExperienceReport FromDto(TerrainExperienceReportDto report)
    {
        return new TerrainExperienceReport(
            report.RegionCount,
            report.EncounterRichRegionRatio,
            report.ResourceRichRegionRatio,
            report.HazardRichRegionRatio,
            report.AverageExposure,
            report.AverageResourcePotential,
            report.AverageHazardPotential,
            report.AverageEncounterPotential,
            report.RouteRhythmScore,
            report.PointOfInterestValue,
            report.RiskRewardBalance,
            report.ScenicAnchorRatio);
    }
}
