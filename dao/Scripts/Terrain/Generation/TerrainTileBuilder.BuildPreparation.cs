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
        int nativeFieldSampleCount = vertexCount * TerrainWorldFieldSampler.NativeFieldGridStride;
        float[] nativeFieldSamples = [];
        bool returnNativeFieldSamples = false;
        bool useNativeFields = false;
        bool nativeFieldsContainDerivedData = false;
        bool useNativeTileGeneration = samplingBackendMode switch
        {
            TerrainTileSamplingBackendMode.Native => profile.UseNativeSamplerWhenAvailable && NativeTerrainBridge.IsAvailable,
            TerrainTileSamplingBackendMode.Managed => false,
            _ => ShouldUseNativeSamplerForTileGeneration(profile, lod)
        };

        if (useNativeTileGeneration)
        {
            nativeFieldSamples = ArrayPool<float>.Shared.Rent(nativeFieldSampleCount);
            returnNativeFieldSamples = true;
            useNativeFields = NativeTerrainBridge.TrySampleFieldGrid(
                coord,
                resolution,
                profile,
                nativeFieldSamples,
                nativeFieldSampleCount,
                out nativeFieldsContainDerivedData);
            if (!useNativeFields)
            {
                ArrayPool<float>.Shared.Return(nativeFieldSamples);
                nativeFieldSamples = [];
                returnNativeFieldSamples = false;
            }
        }

        float[] nativeHeights = [];
        bool useNativeHeights = !useNativeFields &&
            useNativeTileGeneration &&
            NativeTerrainBridge.TrySampleHeightGrid(coord, resolution, profile, out nativeHeights);
        float managedLandBalanceOffset = !useNativeFields && !useNativeHeights
            ? TerrainWorldFieldSampler.LandBalanceOffsetFor(profile)
            : 0.0f;
        return new TerrainTileNativeSamplingState(
            useNativeFields,
            nativeFieldsContainDerivedData,
            nativeFieldSamples,
            returnNativeFieldSamples,
            useNativeHeights,
            nativeHeights,
            managedLandBalanceOffset);
    }

    private static TerrainTileFeaturePreparation PrepareFeaturePreparation(
        TerrainTileCoord coord,
        TerrainRouteCorridorIndex routeCorridors,
        TerrainPointOfInterestIndex pointOfInterestIndex,
        TerrainGenerationProfile profile)
    {
        TerrainRouteCorridorSegment[] corridorSegments = routeCorridors.GetSegmentsUnsafe(coord);
        bool hasCorridors = corridorSegments.Length > 0;
        TerrainWorldPointOfInterest[] pointInfluences = pointOfInterestIndex.GetPointsUnsafe(coord);
        bool hasPointInfluences = pointInfluences.Length > 0;
        TerrainSettlementLayoutDescriptor[] settlementLayouts = hasPointInfluences
            ? BuildSettlementLayoutDescriptors(pointInfluences, corridorSegments, profile)
            : [];
        bool hasSettlementLayouts = settlementLayouts.Length > 0;
        return new TerrainTileFeaturePreparation(
            corridorSegments,
            hasCorridors,
            pointInfluences,
            hasPointInfluences,
            settlementLayouts,
            hasSettlementLayouts);
    }

    private static TerrainTileScratchBuffers AllocateScratchBuffers(
        int vertexCount,
        bool useSkirtedRenderMesh,
        TerrainTileFeaturePreparation featurePreparation)
    {
        Vector3[] surfaceVertices = useSkirtedRenderMesh ? ArrayPool<Vector3>.Shared.Rent(vertexCount) : new Vector3[vertexCount];
        Vector3[] surfaceNormals = useSkirtedRenderMesh ? ArrayPool<Vector3>.Shared.Rent(vertexCount) : new Vector3[vertexCount];
        Vector2[] surfaceUvs = useSkirtedRenderMesh ? ArrayPool<Vector2>.Shared.Rent(vertexCount) : new Vector2[vertexCount];
        Color[] surfaceColors = useSkirtedRenderMesh ? ArrayPool<Color>.Shared.Rent(vertexCount) : new Color[vertexCount];
        float[] heights = ArrayPool<float>.Shared.Rent(vertexCount);
        TerrainWorldField[] fields = ArrayPool<TerrainWorldField>.Shared.Rent(vertexCount);
        TerrainRouteCorridorSample[] corridorSamples = featurePreparation.HasCorridors
            ? ArrayPool<TerrainRouteCorridorSample>.Shared.Rent(vertexCount)
            : [];
        TerrainPointFootprintSample[] footprintSamples = featurePreparation.HasPointInfluences
            ? ArrayPool<TerrainPointFootprintSample>.Shared.Rent(vertexCount)
            : [];
        TerrainSettlementLayoutSample[] settlementLayoutSamples = featurePreparation.HasSettlementLayouts
            ? ArrayPool<TerrainSettlementLayoutSample>.Shared.Rent(vertexCount)
            : [];
        return new TerrainTileScratchBuffers(
            surfaceVertices,
            surfaceNormals,
            surfaceUvs,
            surfaceColors,
            heights,
            fields,
            corridorSamples,
            footprintSamples,
            settlementLayoutSamples);
    }

    private static bool TryAcquireParallelSurfaceProcessing(
        bool useNativeFields,
        bool useNativeHeights,
        int vertexCount,
        out bool releaseParallelSurfaceProcessingSlot)
    {
        releaseParallelSurfaceProcessingSlot = false;
        bool useParallelSurfaceProcessing = ShouldUseParallelSurfaceProcessing(useNativeFields, useNativeHeights, vertexCount);
        if (!useParallelSurfaceProcessing)
        {
            return false;
        }

        releaseParallelSurfaceProcessingSlot = SurfaceProcessingParallelBuildSlots.Wait(0);
        return releaseParallelSurfaceProcessingSlot;
    }

    private static void ReleaseScratchBuffers(TerrainTileScratchBuffers scratchBuffers, bool useSkirtedRenderMesh)
    {
        if (useSkirtedRenderMesh)
        {
            ReturnPooled(scratchBuffers.SurfaceVertices);
            ReturnPooled(scratchBuffers.SurfaceNormals);
            ReturnPooled(scratchBuffers.SurfaceUvs);
            ReturnPooled(scratchBuffers.SurfaceColors);
        }

        ReturnPooled(scratchBuffers.Heights);
        ReturnPooled(scratchBuffers.Fields);
        ReturnPooled(scratchBuffers.CorridorSamples);
        ReturnPooled(scratchBuffers.FootprintSamples);
        ReturnPooled(scratchBuffers.SettlementLayoutSamples);
    }

    private static void ReleaseNativeSamplingState(TerrainTileNativeSamplingState nativeSamplingState)
    {
        if (nativeSamplingState.ReturnNativeFieldSamples)
        {
            ReturnPooled(nativeSamplingState.NativeFieldSamples);
        }
    }

    private static void ReturnPooled<T>(T[] array)
    {
        if (array.Length > 0)
        {
            ArrayPool<T>.Shared.Return(array);
        }
    }
}
