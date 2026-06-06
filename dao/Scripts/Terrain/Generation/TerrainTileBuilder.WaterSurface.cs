using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static TerrainWaterSurfaceData BuildWaterSurface(
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        TerrainPointFootprintSample[] footprintSamples,
        TerrainSettlementLayoutSample[] settlementLayoutSamples,
        CancellationToken cancellationToken)
    {
        return TerrainTileWaterSurfaceBuilderService.BuildWaterSurface(
            profile,
            resolution,
            vertexCountPerSide,
            step,
            heights,
            fields,
            footprintSamples,
            settlementLayoutSamples,
            cancellationToken);
    }
}
