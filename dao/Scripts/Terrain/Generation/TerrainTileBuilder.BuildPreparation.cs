using System.Buffers;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static TerrainTileNativeSamplingState PrepareNativeSamplingState(
        TerrainTileCoord coord,
        int lod,
        int resolution,
        int vertexCount,
        TerrainGenerationProfile profile,
        TerrainTileSamplingBackendMode samplingBackendMode)
    {
        return TerrainTileBuildPreparationService.PrepareNativeSamplingState(
            coord,
            lod,
            resolution,
            vertexCount,
            profile,
            samplingBackendMode);
    }

    private static TerrainTileFeaturePreparation PrepareFeaturePreparation(
        TerrainTileCoord coord,
        TerrainRouteCorridorIndex routeCorridors,
        TerrainPointOfInterestIndex pointOfInterestIndex,
        TerrainGenerationProfile profile)
    {
        return TerrainTileBuildPreparationService.PrepareFeaturePreparation(
            coord,
            routeCorridors,
            pointOfInterestIndex,
            profile);
    }

    private static TerrainTileScratchBuffers AllocateScratchBuffers(
        int vertexCount,
        bool useSkirtedRenderMesh,
        TerrainTileFeaturePreparation featurePreparation)
    {
        return TerrainTileBuildPreparationService.AllocateScratchBuffers(
            vertexCount,
            useSkirtedRenderMesh,
            featurePreparation);
    }

    private static bool TryAcquireParallelSurfaceProcessing(
        bool useNativeFields,
        bool useNativeHeights,
        int vertexCount,
        out bool releaseParallelSurfaceProcessingSlot)
    {
        return TerrainTileBuildPreparationService.TryAcquireParallelSurfaceProcessing(
            useNativeFields,
            useNativeHeights,
            vertexCount,
            out releaseParallelSurfaceProcessingSlot);
    }

    private static void ReleaseScratchBuffers(TerrainTileScratchBuffers scratchBuffers, bool useSkirtedRenderMesh)
    {
        TerrainTileBuildPreparationService.ReleaseScratchBuffers(scratchBuffers, useSkirtedRenderMesh);
    }

    private static void ReleaseNativeSamplingState(TerrainTileNativeSamplingState nativeSamplingState)
    {
        TerrainTileBuildPreparationService.ReleaseNativeSamplingState(nativeSamplingState);
    }
}
