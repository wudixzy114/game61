using System;
using Dao.Terrain;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanSerializer
{
    private static TerrainPlanDto ToDto(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        return TerrainWorldPlanSerializerPlanMappingService.ToDto(plan, profile);
    }

    private static TerrainWorldPlan FromDto(TerrainPlanDto dto)
    {
        return TerrainWorldPlanSerializerPlanMappingService.FromDto(dto);
    }
    private static TerrainRegionDto[] ToDtos(TerrainWorldRegion[] regions)
    {
        return TerrainWorldPlanSerializerPlanMappingService.ToDtos(regions);
    }

    private static TerrainWorldRegion[] FromDtos(TerrainRegionDto[]? regions)
    {
        return TerrainWorldPlanSerializerPlanMappingService.FromDtos(regions);
    }

    private static TerrainPointDto[] ToDtos(TerrainWorldPointOfInterest[] points)
    {
        return TerrainWorldPlanSerializerPlanMappingService.ToDtos(points);
    }

    private static TerrainWorldPointOfInterest[] FromDtos(TerrainPointDto[]? points)
    {
        return TerrainWorldPlanSerializerPlanMappingService.FromDtos(points);
    }

    private static TerrainRouteDto[] ToDtos(TerrainWorldRoute[] routes)
    {
        return TerrainWorldPlanSerializerPlanMappingService.ToDtos(routes);
    }

    private static TerrainWorldRoute[] FromDtos(TerrainRouteDto[]? routes)
    {
        return TerrainWorldPlanSerializerPlanMappingService.FromDtos(routes);
    }
}
