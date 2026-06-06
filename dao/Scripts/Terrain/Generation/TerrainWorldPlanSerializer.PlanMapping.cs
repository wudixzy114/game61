using System;
using Dao.Terrain;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanSerializer
{
    private static TerrainPlanDto ToDto(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        return new TerrainPlanDto
        {
            Contract = Contract,
            ApiContract = TerrainApiVersion.Contract,
            ApiVersion = TerrainApiVersion.Version,
            GeneratorVersion = GeneratorVersion,
            Seed = profile.Seed,
            ProfileHash = profile.StableHash(),
            ScatterRuleSetHash = TerrainRuleSetHashNormalizer.NormalizeScatterRuleSetHash(profile.ScatterRuleSetHash),
            SettlementVisualRuleSetHash = TerrainRuleSetHashNormalizer.NormalizeSettlementVisualRuleSetHash(profile.SettlementVisualRuleSetHash),
            PointOfInterestRuleSetHash = TerrainRuleSetHashNormalizer.NormalizePointOfInterestRuleSetHash(profile.PointOfInterestRuleSetHash),
            RouteRuleSetHash = TerrainRuleSetHashNormalizer.NormalizeRouteRuleSetHash(profile.RouteRuleSetHash),
            ScenicLandmarkRuleSetHash = TerrainRuleSetHashNormalizer.NormalizeScenicRuleSetHash(profile.ScenicLandmarkRuleSetHash),
            Center = ToDto(plan.Center),
            WorldSize = plan.WorldSize,
            GridResolution = plan.GridResolution,
            Regions = ToDtos(plan.Regions),
            PointsOfInterest = ToDtos(plan.PointsOfInterest),
            Routes = ToDtos(plan.Routes),
            Reports = new TerrainPlanReportsDto
            {
                Quality = ToDto(plan.QualityReport),
                Planning = ToDto(plan.PlanningReport),
                Experience = ToDto(plan.ExperienceReport)
            }
        };
    }

    private static TerrainWorldPlan FromDto(TerrainPlanDto dto)
    {
        if (dto.Center is null)
        {
            throw new InvalidOperationException("terrain plan JSON is missing center");
        }

        if (dto.Reports?.Quality is null || dto.Reports.Planning is null || dto.Reports.Experience is null)
        {
            throw new InvalidOperationException("terrain plan JSON is missing reports");
        }

        return new TerrainWorldPlan(
            FromDto(dto.Center),
            dto.WorldSize,
            dto.GridResolution,
            FromDtos(dto.Regions),
            FromDtos(dto.PointsOfInterest),
            FromDtos(dto.Routes),
            FromDto(dto.Reports.Quality),
            FromDto(dto.Reports.Planning),
            FromDto(dto.Reports.Experience));
    }
    private static TerrainRegionDto[] ToDtos(TerrainWorldRegion[] regions)
    {
        var values = new TerrainRegionDto[regions.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            TerrainWorldRegion region = regions[i];
            values[i] = new TerrainRegionDto
            {
                GridX = region.GridX,
                GridY = region.GridY,
                World = ToDto(region.WorldPosition),
                Height = region.Height,
                River = region.River,
                ScenicPotential = region.ScenicPotential,
                Traversability = region.Traversability,
                Exposure = region.Exposure,
                ResourcePotential = region.ResourcePotential,
                HazardPotential = region.HazardPotential,
                EncounterPotential = region.EncounterPotential,
                Biome = ToDto(region.BiomeKind),
                Landscape = ToDto(region.LandscapeKind),
                Region = ToDto(region.RegionKind)
            };
        }

        return values;
    }

    private static TerrainWorldRegion[] FromDtos(TerrainRegionDto[]? regions)
    {
        if (regions is null || regions.Length == 0)
        {
            return [];
        }

        var values = new TerrainWorldRegion[regions.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            TerrainRegionDto region = regions[i];
            values[i] = new TerrainWorldRegion(
                region.GridX,
                region.GridY,
                FromDto(region.World),
                region.Height,
                region.River,
                region.ScenicPotential,
                region.Traversability,
                region.Exposure,
                region.ResourcePotential,
                region.HazardPotential,
                region.EncounterPotential,
                EnumValue<TerrainBiomeKind>(region.Biome),
                EnumValue<TerrainLandscapeKind>(region.Landscape),
                EnumValue<TerrainWorldRegionKind>(region.Region));
        }

        return values;
    }

    private static TerrainPointDto[] ToDtos(TerrainWorldPointOfInterest[] points)
    {
        var values = new TerrainPointDto[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            TerrainWorldPointOfInterest point = points[i];
            values[i] = new TerrainPointDto
            {
                Id = point.Id,
                Kind = ToDto(point.Kind),
                World = ToDto(point.WorldPosition),
                GridX = point.GridX,
                GridY = point.GridY,
                Score = point.Score,
                Height = point.Height,
                ScenicPotential = point.ScenicPotential,
                Traversability = point.Traversability,
                Biome = ToDto(point.BiomeKind),
                Landscape = ToDto(point.LandscapeKind),
                SettlementTier = ToDto(point.SettlementTier),
                DebugName = point.DebugName
            };
        }

        return values;
    }

    private static TerrainWorldPointOfInterest[] FromDtos(TerrainPointDto[]? points)
    {
        if (points is null || points.Length == 0)
        {
            return [];
        }

        var values = new TerrainWorldPointOfInterest[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            TerrainPointDto point = points[i];
            values[i] = new TerrainWorldPointOfInterest(
                point.Id,
                EnumValue<TerrainPointOfInterestKind>(point.Kind),
                FromDto(point.World),
                point.GridX,
                point.GridY,
                point.Score,
                point.Height,
                point.ScenicPotential,
                point.Traversability,
                EnumValue<TerrainBiomeKind>(point.Biome),
                EnumValue<TerrainLandscapeKind>(point.Landscape),
                EnumValue<TerrainSettlementTier>(point.SettlementTier),
                point.DebugName ?? string.Empty);
        }

        return values;
    }

    private static TerrainRouteDto[] ToDtos(TerrainWorldRoute[] routes)
    {
        var values = new TerrainRouteDto[routes.Length];
        for (int i = 0; i < routes.Length; i++)
        {
            TerrainWorldRoute route = routes[i];
            values[i] = new TerrainRouteDto
            {
                FromPointId = route.FromPointId,
                ToPointId = route.ToPointId,
                Kind = ToDto(route.Kind),
                Cost = route.Cost,
                AverageScenicPotential = route.AverageScenicPotential,
                AverageTraversability = route.AverageTraversability,
                Waypoints = ToDtos(route.Waypoints)
            };
        }

        return values;
    }

    private static TerrainWorldRoute[] FromDtos(TerrainRouteDto[]? routes)
    {
        if (routes is null || routes.Length == 0)
        {
            return [];
        }

        var values = new TerrainWorldRoute[routes.Length];
        for (int i = 0; i < routes.Length; i++)
        {
            TerrainRouteDto route = routes[i];
            values[i] = new TerrainWorldRoute(
                route.FromPointId,
                route.ToPointId,
                EnumValue<TerrainRouteKind>(route.Kind),
                route.Cost,
                route.AverageScenicPotential,
                route.AverageTraversability,
                FromDtos(route.Waypoints));
        }

        return values;
    }
}
