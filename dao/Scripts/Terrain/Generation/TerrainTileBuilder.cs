using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
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
        return Build(
            coord,
            lod,
            profile,
            includeCollision,
            routeCorridors,
            pointOfInterestIndex,
            cancellationToken,
            TerrainTileSamplingBackendMode.Adaptive);
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
        CancellationToken cancellationToken,
        TerrainTileSamplingBackendMode samplingBackendMode)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int resolution = profile.ResolutionForLod(lod);
        int vertexCountPerSide = resolution + 1;
        int vertexCount = vertexCountPerSide * vertexCountPerSide;
        float step = profile.ChunkSize / resolution;
        Vector2 origin = coord.Origin(profile.ChunkSize);
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
        bool useSkirtedRenderMesh = Mathf.Max(0.0f, profile.SkirtDepth) > SkirtEnabledThreshold;
        Vector3[] surfaceVertices = [];
        Vector3[] surfaceNormals = [];
        Vector2[] surfaceUvs = [];
        Color[] surfaceColors = [];
        float[] heights = [];
        TerrainWorldField[] fields = [];
        TerrainRouteCorridorSample[] corridorSamples = [];
        TerrainPointFootprintSample[] footprintSamples = [];
        TerrainSettlementLayoutSample[] settlementLayoutSamples = [];
        bool releaseParallelSurfaceProcessingSlot = false;

        try
        {
            surfaceVertices = useSkirtedRenderMesh ? ArrayPool<Vector3>.Shared.Rent(vertexCount) : new Vector3[vertexCount];
            surfaceNormals = useSkirtedRenderMesh ? ArrayPool<Vector3>.Shared.Rent(vertexCount) : new Vector3[vertexCount];
            surfaceUvs = useSkirtedRenderMesh ? ArrayPool<Vector2>.Shared.Rent(vertexCount) : new Vector2[vertexCount];
            surfaceColors = useSkirtedRenderMesh ? ArrayPool<Color>.Shared.Rent(vertexCount) : new Color[vertexCount];
            heights = ArrayPool<float>.Shared.Rent(vertexCount);
            fields = ArrayPool<TerrainWorldField>.Shared.Rent(vertexCount);
            TerrainRouteCorridorSegment[] corridorSegments = routeCorridors.GetSegmentsUnsafe(coord);
            bool hasCorridors = corridorSegments.Length > 0;
            corridorSamples = hasCorridors ? ArrayPool<TerrainRouteCorridorSample>.Shared.Rent(vertexCount) : [];
            TerrainWorldPointOfInterest[] pointInfluences = pointOfInterestIndex.GetPointsUnsafe(coord);
            bool hasPointInfluences = pointInfluences.Length > 0;
            footprintSamples = hasPointInfluences ? ArrayPool<TerrainPointFootprintSample>.Shared.Rent(vertexCount) : [];
            TerrainSettlementLayoutDescriptor[] settlementLayouts = hasPointInfluences
                ? BuildSettlementLayoutDescriptors(pointInfluences, corridorSegments, profile)
                : [];
            bool hasSettlementLayouts = settlementLayouts.Length > 0;
            settlementLayoutSamples = hasSettlementLayouts ? ArrayPool<TerrainSettlementLayoutSample>.Shared.Rent(vertexCount) : [];
            bool useParallelSurfaceProcessing = ShouldUseParallelSurfaceProcessing(useNativeFields, useNativeHeights, vertexCount);
            if (useParallelSurfaceProcessing)
            {
                releaseParallelSurfaceProcessingSlot = SurfaceProcessingParallelBuildSlots.Wait(0);
                useParallelSurfaceProcessing = releaseParallelSurfaceProcessingSlot;
            }

            void SampleSurfaceVertex(int z, int x)
            {
                int index = Index(x, z, vertexCountPerSide);
                float localX = x * step;
                float localZ = z * step;
                Vector2 world = new(origin.X + localX, origin.Y + localZ);
                TerrainWorldField field = useNativeFields
                    ? TerrainWorldFieldSampler.SampleNativeFieldGrid(world, profile, nativeFieldSamples, index, nativeFieldsContainDerivedData)
                    : useNativeHeights
                    ? TerrainWorldFieldSampler.SampleKnownHeight(world, profile, nativeHeights[index])
                    : TerrainWorldFieldSampler.Sample(world, profile, managedLandBalanceOffset);
                float height = field.Height;
                TerrainRouteCorridorSample corridor = TerrainRouteCorridorSample.None;
                if (hasCorridors)
                {
                    corridor = routeCorridors.Sample(world, corridorSegments);
                    corridorSamples[index] = corridor;
                }

                if (corridor.HasInfluence)
                {
                    height = ApplyRouteCorridorHeight(height, corridor);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.86f, corridor.CoreStrength))
                    };
                }

                TerrainPointFootprintSample footprint = TerrainPointFootprintSample.None;
                if (hasPointInfluences)
                {
                    footprint = SamplePointFootprint(world, pointInfluences, profile);
                    footprintSamples[index] = footprint;
                }

                if (footprint.HasInfluence)
                {
                    height = ApplyPointFootprintHeight(height, footprint);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.92f, footprint.CoreStrength)),
                        EncounterPotential = Mathf.Max(field.EncounterPotential, Mathf.Lerp(field.EncounterPotential, 0.62f, footprint.CoreStrength * 0.60f))
                    };
                }

                TerrainSettlementLayoutSample settlementLayout = TerrainSettlementLayoutSample.None;
                if (hasSettlementLayouts)
                {
                    settlementLayout = SampleSettlementLayout(world, settlementLayouts);
                    settlementLayoutSamples[index] = settlementLayout;
                }

                if (settlementLayout.HasInfluence)
                {
                    height = ApplySettlementLayoutHeight(height, settlementLayout);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.95f, settlementLayout.CoreStrength)),
                        EncounterPotential = Mathf.Max(field.EncounterPotential, Mathf.Lerp(field.EncounterPotential, 0.66f, settlementLayout.Influence * 0.45f))
                    };
                }

                surfaceVertices[index] = new Vector3(localX, height, localZ);
                surfaceUvs[index] = new Vector2(
                    world.X / profile.ChunkSize,
                    world.Y / profile.ChunkSize);
                heights[index] = height;
                fields[index] = field;
            }

            if (useParallelSurfaceProcessing)
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = SurfaceProcessingMaxDegreeOfParallelism
                };
                Parallel.For(0, resolution + 1, parallelOptions, z =>
                {
                    for (int x = 0; x <= resolution; x++)
                    {
                        SampleSurfaceVertex(z, x);
                    }
                });
            }
            else
            {
                for (int z = 0; z <= resolution; z++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    for (int x = 0; x <= resolution; x++)
                    {
                        SampleSurfaceVertex(z, x);
                    }
                }
            }

            float minHeight = float.PositiveInfinity;
            float maxHeight = float.NegativeInfinity;
            for (int i = 0; i < vertexCount; i++)
            {
                float height = heights[i];
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }

            void ColorSurfaceVertex(int z, int x)
            {
                int index = Index(x, z, vertexCountPerSide);
                Vector3 normal = CalculateGridNormal(x, z, resolution, vertexCountPerSide, heights, step);
                float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
                surfaceNormals[index] = normal;
                surfaceColors[index] = TerrainSampler.ColorForSurface(fields[index], profile, slope);

                if (heights[index] < profile.SeaLevel + 3.0f)
                {
                    surfaceColors[index] = surfaceColors[index].Lerp(new Color(0.10f, 0.24f, 0.31f), 0.35f);
                }

                if (hasCorridors && corridorSamples[index].HasInfluence)
                {
                    surfaceColors[index] = BlendRouteSurfaceColor(surfaceColors[index], corridorSamples[index]);
                }

                if (hasPointInfluences && footprintSamples[index].HasInfluence)
                {
                    surfaceColors[index] = BlendPointFootprintColor(surfaceColors[index], footprintSamples[index]);
                }

                if (hasSettlementLayouts && settlementLayoutSamples[index].HasInfluence)
                {
                    surfaceColors[index] = BlendSettlementLayoutColor(surfaceColors[index], settlementLayoutSamples[index]);
                }
            }

            if (useParallelSurfaceProcessing)
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = SurfaceProcessingMaxDegreeOfParallelism
                };
                Parallel.For(0, resolution + 1, parallelOptions, z =>
                {
                    for (int x = 0; x <= resolution; x++)
                    {
                        ColorSurfaceVertex(z, x);
                    }
                });
            }
            else
            {
                for (int z = 0; z <= resolution; z++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    for (int x = 0; x <= resolution; x++)
                    {
                        ColorSurfaceVertex(z, x);
                    }
                }
            }

            int[] surfaceIndices = GetSurfaceIndices(resolution);
            Vector3[] collisionFaces = includeCollision ? BuildCollisionFaces(surfaceVertices, surfaceIndices) : [];
            cancellationToken.ThrowIfCancellationRequested();
            BuildSkirtedRenderMesh(
                resolution,
                vertexCountPerSide,
                vertexCount,
                profile.SkirtDepth,
                surfaceVertices,
                surfaceNormals,
                surfaceUvs,
                surfaceColors,
                out Vector3[] renderVertices,
                out Vector3[] renderNormals,
                out Vector2[] renderUvs,
                out Color[] renderColors,
                out int[] renderIndices);
            BuildTerrainFeatures(
                coord,
                lod,
                profile,
                resolution,
                vertexCountPerSide,
                step,
                heights,
                fields,
                surfaceNormals,
                routeCorridors,
                corridorSegments,
                pointOfInterestIndex,
                cancellationToken,
                out TerrainScatterInstance[] scatterInstances,
                out TerrainLandmarkData[] landmarks);
            TerrainWaterSurfaceData waterSurface = BuildWaterSurface(
                profile,
                resolution,
                vertexCountPerSide,
                step,
                heights,
                fields,
                footprintSamples,
                settlementLayoutSamples,
                cancellationToken);

            float renderMinHeight = minHeight - Mathf.Max(0.0f, profile.SkirtDepth);

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
                maxHeight);
        }
        finally
        {
            if (useSkirtedRenderMesh)
            {
                ReturnPooled(surfaceVertices);
                ReturnPooled(surfaceNormals);
                ReturnPooled(surfaceUvs);
                ReturnPooled(surfaceColors);
            }

            ReturnPooled(heights);
            ReturnPooled(fields);
            ReturnPooled(corridorSamples);
            ReturnPooled(footprintSamples);
            ReturnPooled(settlementLayoutSamples);

            if (returnNativeFieldSamples)
            {
                ReturnPooled(nativeFieldSamples);
            }

            if (releaseParallelSurfaceProcessingSlot)
            {
                SurfaceProcessingParallelBuildSlots.Release();
            }
        }
    }

    private static void ReturnPooled<T>(T[] array)
    {
        if (array.Length > 0)
        {
            ArrayPool<T>.Shared.Return(array);
        }
    }

    private enum TerrainTileSamplingBackendMode
    {
        Adaptive,
        Managed,
        Native
    }

    private enum TerrainWaterSurfaceKind
    {
        None,
        Lake,
        River,
        Oasis
    }

    private readonly record struct TerrainTileSamplingDecisionKey(
        TerrainGenerationProfile Profile,
        int Lod,
        int Resolution);

    private readonly record struct TerrainTileSamplingDecision(
        bool UseNative,
        double ManagedMillisecondsPerTile,
        double NativeMillisecondsPerTile,
        double Speedup,
        int Resolution,
        string Reason)
    {
        public static TerrainTileSamplingDecision Managed(string reason)
        {
            return new TerrainTileSamplingDecision(
                false,
                0.0,
                0.0,
                0.0,
                0,
                reason);
        }
    }

}
