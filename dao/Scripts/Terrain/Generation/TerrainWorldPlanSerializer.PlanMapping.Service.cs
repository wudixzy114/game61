using System;
using Dao.Terrain;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanSerializer
{
    private static class TerrainWorldPlanSerializerPlanMappingService
    {
        internal static TerrainPlanDto ToDto(TerrainWorldPlan plan, TerrainGenerationProfile profile)
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
                Center = TerrainWorldPlanSerializer.ToDto(plan.Center),
                WorldSize = plan.WorldSize,
                GridResolution = plan.GridResolution,
                Regions = TerrainWorldPlanSerializerPlanMappingService.ToDtos(plan.Regions),
                PointsOfInterest = TerrainWorldPlanSerializerPlanMappingService.ToDtos(plan.PointsOfInterest),
                Routes = TerrainWorldPlanSerializerPlanMappingService.ToDtos(plan.Routes),
                Reports = new TerrainPlanReportsDto
                {
                    Quality = TerrainWorldPlanSerializer.ToDto(plan.QualityReport),
                    Planning = TerrainWorldPlanSerializer.ToDto(plan.PlanningReport),
                    Experience = TerrainWorldPlanSerializer.ToDto(plan.ExperienceReport)
                }
            };
        }

        internal static TerrainWorldPlan FromDto(TerrainPlanDto dto)
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
                TerrainWorldPlanSerializer.FromDto(dto.Center),
                dto.WorldSize,
                dto.GridResolution,
                TerrainWorldPlanSerializerPlanMappingService.FromDtos(dto.Regions),
                TerrainWorldPlanSerializerPlanMappingService.FromDtos(dto.PointsOfInterest),
                TerrainWorldPlanSerializerPlanMappingService.FromDtos(dto.Routes),
                TerrainWorldPlanSerializer.FromDto(dto.Reports.Quality),
                TerrainWorldPlanSerializer.FromDto(dto.Reports.Planning),
                TerrainWorldPlanSerializer.FromDto(dto.Reports.Experience));
        }

        internal static TerrainRegionDto[] ToDtos(TerrainWorldRegion[] regions)
        {
            var values = new TerrainRegionDto[regions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                TerrainWorldRegion region = regions[i];
                values[i] = new TerrainRegionDto
                {
                    GridX = region.GridX,
                    GridY = region.GridY,
                    World = TerrainWorldPlanSerializer.ToDto(region.WorldPosition),
                    Height = region.Height,
                    River = region.River,
                    ScenicPotential = region.ScenicPotential,
                    Traversability = region.Traversability,
                    Exposure = region.Exposure,
                    ResourcePotential = region.ResourcePotential,
                    HazardPotential = region.HazardPotential,
                    EncounterPotential = region.EncounterPotential,
                    Biome = TerrainWorldPlanSerializer.ToDto(region.BiomeKind),
                    Landscape = TerrainWorldPlanSerializer.ToDto(region.LandscapeKind),
                    Region = TerrainWorldPlanSerializer.ToDto(region.RegionKind)
                };
            }

            return values;
        }

        internal static TerrainWorldRegion[] FromDtos(TerrainRegionDto[]? regions)
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
                    TerrainWorldPlanSerializer.FromDto(region.World),
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

        internal static TerrainPointDto[] ToDtos(TerrainWorldPointOfInterest[] points)
        {
            var values = new TerrainPointDto[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                TerrainWorldPointOfInterest point = points[i];
                values[i] = new TerrainPointDto
                {
                    Id = point.Id,
                    Kind = TerrainWorldPlanSerializer.ToDto(point.Kind),
                    World = TerrainWorldPlanSerializer.ToDto(point.WorldPosition),
                    GridX = point.GridX,
                    GridY = point.GridY,
                    Score = point.Score,
                    Height = point.Height,
                    ScenicPotential = point.ScenicPotential,
                    Traversability = point.Traversability,
                    Biome = TerrainWorldPlanSerializer.ToDto(point.BiomeKind),
                    Landscape = TerrainWorldPlanSerializer.ToDto(point.LandscapeKind),
                    SettlementTier = TerrainWorldPlanSerializer.ToDto(point.SettlementTier),
                    DebugName = point.DebugName
                };
            }

            return values;
        }

        internal static TerrainWorldPointOfInterest[] FromDtos(TerrainPointDto[]? points)
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
                    TerrainWorldPlanSerializer.FromDto(point.World),
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

        internal static TerrainRouteDto[] ToDtos(TerrainWorldRoute[] routes)
        {
            var values = new TerrainRouteDto[routes.Length];
            for (int i = 0; i < routes.Length; i++)
            {
                TerrainWorldRoute route = routes[i];
                values[i] = new TerrainRouteDto
                {
                    FromPointId = route.FromPointId,
                    ToPointId = route.ToPointId,
                    Kind = TerrainWorldPlanSerializer.ToDto(route.Kind),
                    Cost = route.Cost,
                    AverageScenicPotential = route.AverageScenicPotential,
                    AverageTraversability = route.AverageTraversability,
                    Waypoints = TerrainWorldPlanSerializer.ToDtos(route.Waypoints)
                };
            }

            return values;
        }

        internal static TerrainWorldRoute[] FromDtos(TerrainRouteDto[]? routes)
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
                    TerrainWorldPlanSerializer.FromDtos(route.Waypoints));
            }

            return values;
        }
    }
}
