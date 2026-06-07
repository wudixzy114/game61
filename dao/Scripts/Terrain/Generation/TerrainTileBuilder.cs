using System;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Builds terrain tile meshes, heights, scatter, and landmarks from field data, route corridors, and POI footprints.</summary>
public static partial class TerrainTileBuilder
{
    private const float SkirtEnabledThreshold = 0.001f;
    private const int ParallelSurfaceProcessingVertexThreshold = 2048;
    private const int NativeSamplerSelectionMinResolution = 32;
    private const int NativeSamplerSelectionMeasurementPasses = 2;
    private const float NativeSamplerSelectionMinSpeedup = 1.08f;
    private static readonly int SurfaceProcessingMaxDegreeOfParallelism = ComputeSurfaceProcessingMaxDegreeOfParallelism();
    private static readonly SemaphoreSlim SurfaceProcessingParallelBuildSlots = new(ComputeSurfaceProcessingParallelBuildSlotCount());
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, int[]> SurfaceIndexCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, int[]> SkirtedIndexCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<TerrainTileSamplingDecisionKey, Lazy<TerrainTileSamplingDecision>> NativeSamplerSelectionCache = new();
    private static double NativeSamplerSelectionMeasurementSink;

    /// <summary>Builds a terrain tile without route or POI data.</summary>
    public static TerrainTileData Build(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        bool includeCollision,
        CancellationToken cancellationToken = default)
    {
        return Build(coord, lod, profile, includeCollision, TerrainRouteCorridorIndex.Empty, cancellationToken);
    }

    /// <summary>Builds a terrain tile with route corridor data.</summary>
    public static TerrainTileData Build(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        bool includeCollision,
        TerrainRouteCorridorIndex routeCorridors,
        CancellationToken cancellationToken = default)
    {
        return Build(coord, lod, profile, includeCollision, routeCorridors, TerrainPointOfInterestIndex.Empty, cancellationToken);
    }

    /// <summary>Builds a terrain tile with both route corridor data and POI footprint data.</summary>
    public static TerrainTileData Build(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        bool includeCollision,
        TerrainRouteCorridorIndex routeCorridors,
        TerrainPointOfInterestIndex pointOfInterestIndex,
        CancellationToken cancellationToken = default)
    {
        return BuildWithModification(
            coord,
            lod,
            profile,
            includeCollision,
            routeCorridors,
            pointOfInterestIndex,
            TerrainModificationLayer.Empty,
            cancellationToken);
    }

    internal static TerrainTileData BuildWithModification(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        bool includeCollision,
        TerrainRouteCorridorIndex routeCorridors,
        TerrainPointOfInterestIndex pointOfInterestIndex,
        TerrainModificationLayer modificationLayer,
        CancellationToken cancellationToken = default)
    {
        return Build(
            coord,
            lod,
            profile,
            includeCollision,
            routeCorridors,
            pointOfInterestIndex,
            modificationLayer,
            cancellationToken,
            TerrainTileSamplingBackendMode.Adaptive);
    }

    /// <summary>Builds a terrain tile with deterministic modification overlay applied to surface and feature materialization.</summary>
    public static TerrainTileData BuildWithOverlay(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        bool includeCollision,
        TerrainRouteCorridorIndex routeCorridors,
        TerrainPointOfInterestIndex pointOfInterestIndex,
        TerrainModificationLayer modificationLayer,
        CancellationToken cancellationToken = default)
    {
        return BuildWithModification(
            coord,
            lod,
            profile,
            includeCollision,
            routeCorridors,
            pointOfInterestIndex,
            modificationLayer,
            cancellationToken);
    }

    /// <summary>Returns whether the adaptive tile builder will use the native sampler for this profile and LOD.</summary>
    public static bool ShouldUseNativeSamplerForTileGeneration(TerrainGenerationProfile profile, int lod)
    {
        if (!profile.UseNativeSamplerWhenAvailable ||
            !NativeTerrainBridge.SupportsFieldGridSampler)
        {
            return false;
        }

        int safeLod = Mathf.Clamp(lod, 0, profile.MaxLod);
        int resolution = profile.ResolutionForLod(safeLod);
        if (resolution < NativeSamplerSelectionMinResolution)
        {
            return false;
        }

        var key = new TerrainTileSamplingDecisionKey(profile, safeLod, resolution);
        try
        {
            return NativeSamplerSelectionCache.GetOrAdd(
                key,
                static samplingKey => new Lazy<TerrainTileSamplingDecision>(
                    () => CalibrateNativeSamplerSelection(samplingKey.Profile, samplingKey.Lod, samplingKey.Resolution),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value.UseNative;
        }
        catch
        {
            return false;
        }
    }

    private static TerrainTileData Build(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        bool includeCollision,
        TerrainRouteCorridorIndex routeCorridors,
        TerrainPointOfInterestIndex pointOfInterestIndex,
        TerrainModificationLayer modificationLayer,
        CancellationToken cancellationToken,
        TerrainTileSamplingBackendMode samplingBackendMode)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int resolution = profile.ResolutionForLod(lod);
        int vertexCountPerSide = resolution + 1;
        int vertexCount = vertexCountPerSide * vertexCountPerSide;
        float step = profile.ChunkSize / resolution;
        Vector2 origin = coord.Origin(profile.ChunkSize);
        TerrainTileNativeSamplingState nativeSamplingState = PrepareNativeSamplingState(
            coord,
            lod,
            resolution,
            vertexCount,
            profile,
            samplingBackendMode);
        bool useSkirtedRenderMesh = Mathf.Max(0.0f, profile.SkirtDepth) > SkirtEnabledThreshold;
        TerrainTileFeaturePreparation featurePreparation = PrepareFeaturePreparation(
            coord,
            routeCorridors,
            pointOfInterestIndex,
            profile);
        TerrainTileScratchBuffers scratchBuffers = AllocateScratchBuffers(
            vertexCount,
            useSkirtedRenderMesh,
            featurePreparation);
        bool releaseParallelSurfaceProcessingSlot = false;

        try
        {
            bool useParallelSurfaceProcessing = TryAcquireParallelSurfaceProcessing(
                nativeSamplingState.UseNativeFields,
                nativeSamplingState.UseNativeHeights,
                vertexCount,
                out releaseParallelSurfaceProcessingSlot);
            TerrainTileSurfaceBuildContext surfaceBuildContext = new(
                profile,
                modificationLayer,
                resolution,
                vertexCountPerSide,
                vertexCount,
                step,
                origin,
                nativeSamplingState.UseNativeFields,
                nativeSamplingState.NativeFieldsContainDerivedData,
                nativeSamplingState.NativeFieldSamples,
                nativeSamplingState.UseNativeHeights,
                nativeSamplingState.NativeHeights,
                nativeSamplingState.ManagedLandBalanceOffset,
                featurePreparation.HasCorridors,
                routeCorridors,
                featurePreparation.CorridorSegments,
                featurePreparation.HasPointInfluences,
                featurePreparation.PointInfluences,
                featurePreparation.HasSettlementLayouts,
                featurePreparation.SettlementLayouts,
                useParallelSurfaceProcessing,
                scratchBuffers.SurfaceVertices,
                scratchBuffers.SurfaceNormals,
                scratchBuffers.SurfaceUvs,
                scratchBuffers.SurfaceColors,
                scratchBuffers.Heights,
                scratchBuffers.Fields,
                scratchBuffers.CorridorSamples,
                scratchBuffers.FootprintSamples,
                scratchBuffers.SettlementLayoutSamples,
                cancellationToken);
            TerrainTileSurfaceHeightRange surfaceHeightRange = BuildSurfaceGeometry(surfaceBuildContext);

            int[] surfaceIndices = GetSurfaceIndices(resolution);
            Vector3[] collisionFaces = includeCollision ? BuildCollisionFaces(scratchBuffers.SurfaceVertices, surfaceIndices) : [];
            cancellationToken.ThrowIfCancellationRequested();
            BuildSkirtedRenderMesh(
                resolution,
                vertexCountPerSide,
                vertexCount,
                profile.SkirtDepth,
                scratchBuffers.SurfaceVertices,
                scratchBuffers.SurfaceNormals,
                scratchBuffers.SurfaceUvs,
                scratchBuffers.SurfaceColors,
                out Vector3[] renderVertices,
                out Vector3[] renderNormals,
                out Vector2[] renderUvs,
                out Color[] renderColors,
                out int[] renderIndices);
            BuildTerrainFeatures(
                coord,
                lod,
                profile,
                modificationLayer,
                resolution,
                vertexCountPerSide,
                step,
                scratchBuffers.Heights,
                scratchBuffers.Fields,
                scratchBuffers.SurfaceNormals,
                routeCorridors,
                featurePreparation.CorridorSegments,
                pointOfInterestIndex,
                cancellationToken,
                out TerrainScatterInstance[] scatterInstances,
                out TerrainLandmarkData[] landmarks);
            TerrainWaterSurfaceData waterSurface = BuildWaterSurface(
                profile,
                resolution,
                vertexCountPerSide,
                step,
                scratchBuffers.Heights,
                scratchBuffers.Fields,
                scratchBuffers.FootprintSamples,
                scratchBuffers.SettlementLayoutSamples,
                cancellationToken);

            float renderMinHeight = surfaceHeightRange.MinHeight - Mathf.Max(0.0f, profile.SkirtDepth);

            return new TerrainTileData(
                coord,
                lod,
                resolution,
                profile.ChunkSize,
                origin,
                renderVertices,
                renderNormals,
                renderUvs,
                renderColors,
                renderIndices,
                waterSurface,
                collisionFaces,
                scatterInstances,
                landmarks,
                renderMinHeight,
                surfaceHeightRange.MaxHeight);
        }
        finally
        {
            ReleaseScratchBuffers(scratchBuffers, useSkirtedRenderMesh);
            ReleaseNativeSamplingState(nativeSamplingState);

            if (releaseParallelSurfaceProcessingSlot)
            {
                SurfaceProcessingParallelBuildSlots.Release();
            }
        }
    }

}
