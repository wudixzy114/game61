using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Builds terrain tile meshes, heights, scatter, and landmarks from field data, route corridors, and POI footprints.</summary>
public static partial class TerrainTileBuilder
{
    private const float SkirtEnabledThreshold = 0.001f;
    private const int ManagedSamplingParallelVertexThreshold = 2048;
    private static readonly int ManagedSamplingMaxDegreeOfParallelism = ComputeManagedSamplingMaxDegreeOfParallelism();
    private static readonly SemaphoreSlim ManagedSamplingParallelBuildSlots = new(ComputeManagedSamplingParallelBuildSlotCount());
    private static readonly ConcurrentDictionary<int, int[]> SurfaceIndexCache = new();
    private static readonly ConcurrentDictionary<int, int[]> SkirtedIndexCache = new();

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
        if (profile.UseNativeSamplerWhenAvailable)
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
            profile.UseNativeSamplerWhenAvailable &&
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
        bool releaseManagedParallelSamplingSlot = false;

        try
        {
            surfaceVertices = useSkirtedRenderMesh ? ArrayPool<Vector3>.Shared.Rent(vertexCount) : new Vector3[vertexCount];
            surfaceNormals = useSkirtedRenderMesh ? ArrayPool<Vector3>.Shared.Rent(vertexCount) : new Vector3[vertexCount];
            surfaceUvs = useSkirtedRenderMesh ? ArrayPool<Vector2>.Shared.Rent(vertexCount) : new Vector2[vertexCount];
            surfaceColors = useSkirtedRenderMesh ? ArrayPool<Color>.Shared.Rent(vertexCount) : new Color[vertexCount];
            heights = ArrayPool<float>.Shared.Rent(vertexCount);
            fields = ArrayPool<TerrainWorldField>.Shared.Rent(vertexCount);
            TerrainRouteCorridorSegment[] corridorSegments = routeCorridors.GetSegments(coord);
            bool hasCorridors = corridorSegments.Length > 0;
            corridorSamples = hasCorridors ? ArrayPool<TerrainRouteCorridorSample>.Shared.Rent(vertexCount) : [];
            TerrainWorldPointOfInterest[] pointInfluences = pointOfInterestIndex.GetPoints(coord);
            bool hasPointInfluences = pointInfluences.Length > 0;
            footprintSamples = hasPointInfluences ? ArrayPool<TerrainPointFootprintSample>.Shared.Rent(vertexCount) : [];
            TerrainSettlementLayoutDescriptor[] settlementLayouts = hasPointInfluences
                ? BuildSettlementLayoutDescriptors(pointInfluences, corridorSegments, profile)
                : [];
            bool hasSettlementLayouts = settlementLayouts.Length > 0;
            settlementLayoutSamples = hasSettlementLayouts ? ArrayPool<TerrainSettlementLayoutSample>.Shared.Rent(vertexCount) : [];
            bool useManagedParallelSampling = ShouldUseManagedParallelSampling(useNativeFields, useNativeHeights, vertexCount);
            if (useManagedParallelSampling)
            {
                releaseManagedParallelSamplingSlot = ManagedSamplingParallelBuildSlots.Wait(0);
                useManagedParallelSampling = releaseManagedParallelSamplingSlot;
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

            if (useManagedParallelSampling)
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = ManagedSamplingMaxDegreeOfParallelism
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

            if (useManagedParallelSampling)
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = ManagedSamplingMaxDegreeOfParallelism
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

            if (releaseManagedParallelSamplingSlot)
            {
                ManagedSamplingParallelBuildSlots.Release();
            }
        }
    }

    private static int ComputeManagedSamplingMaxDegreeOfParallelism()
    {
        int processors = System.Environment.ProcessorCount;
        if (processors <= 1)
        {
            return 1;
        }

        return Math.Min(4, Math.Max(2, processors / 2));
    }

    private static int ComputeManagedSamplingParallelBuildSlotCount()
    {
        int processors = System.Environment.ProcessorCount;
        int workersPerBuild = Math.Max(1, ManagedSamplingMaxDegreeOfParallelism);
        return Math.Max(1, processors / workersPerBuild);
    }

    private static bool ShouldUseManagedParallelSampling(
        bool useNativeFields,
        bool useNativeHeights,
        int vertexCount)
    {
        return !useNativeFields &&
            !useNativeHeights &&
            vertexCount >= ManagedSamplingParallelVertexThreshold &&
            ManagedSamplingMaxDegreeOfParallelism > 1;
    }

    private static void ReturnPooled<T>(T[] array)
    {
        if (array.Length > 0)
        {
            ArrayPool<T>.Shared.Return(array);
        }
    }

    private static int[] GetSurfaceIndices(int resolution)
    {
        return SurfaceIndexCache.GetOrAdd(resolution, BuildSurfaceIndices);
    }

    private static int[] GetSkirtedIndices(int resolution)
    {
        return SkirtedIndexCache.GetOrAdd(resolution, BuildSkirtedIndices);
    }

    private static int[] BuildSurfaceIndices(int resolution)
    {
        int vertexCountPerSide = resolution + 1;
        int[] indices = new int[resolution * resolution * 6];
        int cursor = 0;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i0 = Index(x, z, vertexCountPerSide);
                int i1 = Index(x + 1, z, vertexCountPerSide);
                int i2 = Index(x, z + 1, vertexCountPerSide);
                int i3 = Index(x + 1, z + 1, vertexCountPerSide);

                indices[cursor++] = i0;
                indices[cursor++] = i2;
                indices[cursor++] = i1;
                indices[cursor++] = i1;
                indices[cursor++] = i2;
                indices[cursor++] = i3;
            }
        }

        return indices;
    }

    private static int[] BuildSkirtedIndices(int resolution)
    {
        int vertexCountPerSide = resolution + 1;
        int surfaceVertexCount = vertexCountPerSide * vertexCountPerSide;
        int[] surfaceIndices = GetSurfaceIndices(resolution);
        var indices = new int[surfaceIndices.Length + resolution * 4 * 6];
        surfaceIndices.CopyTo(indices, 0);

        int vertexCursor = surfaceVertexCount;
        int indexCursor = surfaceIndices.Length;
        AddSkirtEdgeIndices(
            edge: 0,
            resolution,
            vertexCountPerSide,
            ref vertexCursor,
            ref indexCursor,
            indices);
        AddSkirtEdgeIndices(
            edge: 1,
            resolution,
            vertexCountPerSide,
            ref vertexCursor,
            ref indexCursor,
            indices);
        AddSkirtEdgeIndices(
            edge: 2,
            resolution,
            vertexCountPerSide,
            ref vertexCursor,
            ref indexCursor,
            indices);
        AddSkirtEdgeIndices(
            edge: 3,
            resolution,
            vertexCountPerSide,
            ref vertexCursor,
            ref indexCursor,
            indices);

        return indices;
    }

    private static Vector3[] BuildCollisionFaces(Vector3[] vertices, int[] indices)
    {
        var faces = new Vector3[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            faces[i] = vertices[indices[i]];
        }

        return faces;
    }

    private static void BuildSkirtedRenderMesh(
        int resolution,
        int vertexCountPerSide,
        int surfaceVertexCount,
        float skirtDepth,
        Vector3[] surfaceVertices,
        Vector3[] surfaceNormals,
        Vector2[] surfaceUvs,
        Color[] surfaceColors,
        out Vector3[] vertices,
        out Vector3[] normals,
        out Vector2[] uvs,
        out Color[] colors,
        out int[] indices)
    {
        float safeSkirtDepth = Mathf.Max(0.0f, skirtDepth);
        if (safeSkirtDepth <= SkirtEnabledThreshold)
        {
            vertices = surfaceVertices;
            normals = surfaceNormals;
            uvs = surfaceUvs;
            colors = surfaceColors;
            indices = GetSurfaceIndices(resolution);
            return;
        }

        int edgeVertexCount = vertexCountPerSide * 4;
        vertices = new Vector3[surfaceVertexCount + edgeVertexCount];
        normals = new Vector3[surfaceVertexCount + edgeVertexCount];
        uvs = new Vector2[surfaceVertexCount + edgeVertexCount];
        colors = new Color[surfaceVertexCount + edgeVertexCount];

        Array.Copy(surfaceVertices, vertices, surfaceVertexCount);
        Array.Copy(surfaceNormals, normals, surfaceVertexCount);
        Array.Copy(surfaceUvs, uvs, surfaceVertexCount);
        Array.Copy(surfaceColors, colors, surfaceVertexCount);

        int vertexCursor = surfaceVertexCount;

        AddSkirtEdgeVertices(
            edge: 0,
            resolution,
            vertexCountPerSide,
            safeSkirtDepth,
            ref vertexCursor,
            vertices,
            normals,
            uvs,
            colors);
        AddSkirtEdgeVertices(
            edge: 1,
            resolution,
            vertexCountPerSide,
            safeSkirtDepth,
            ref vertexCursor,
            vertices,
            normals,
            uvs,
            colors);
        AddSkirtEdgeVertices(
            edge: 2,
            resolution,
            vertexCountPerSide,
            safeSkirtDepth,
            ref vertexCursor,
            vertices,
            normals,
            uvs,
            colors);
        AddSkirtEdgeVertices(
            edge: 3,
            resolution,
            vertexCountPerSide,
            safeSkirtDepth,
            ref vertexCursor,
            vertices,
            normals,
            uvs,
            colors);

        indices = GetSkirtedIndices(resolution);
    }

    private static void AddSkirtEdgeVertices(
        int edge,
        int resolution,
        int vertexCountPerSide,
        float skirtDepth,
        ref int vertexCursor,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        Color[] colors)
    {
        for (int i = 0; i <= resolution; i++)
        {
            int surfaceIndex = SurfaceIndexForEdge(edge, i, resolution, vertexCountPerSide);
            vertices[vertexCursor] = vertices[surfaceIndex] - new Vector3(0.0f, skirtDepth, 0.0f);
            normals[vertexCursor] = normals[surfaceIndex];
            uvs[vertexCursor] = uvs[surfaceIndex];
            colors[vertexCursor] = colors[surfaceIndex].Darkened(0.22f);
            vertexCursor++;
        }
    }

    private static void AddSkirtEdgeIndices(
        int edge,
        int resolution,
        int vertexCountPerSide,
        ref int vertexCursor,
        ref int indexCursor,
        int[] indices)
    {
        int firstSkirtVertex = vertexCursor;

        for (int i = 0; i < resolution; i++)
        {
            int top0 = SurfaceIndexForEdge(edge, i, resolution, vertexCountPerSide);
            int top1 = SurfaceIndexForEdge(edge, i + 1, resolution, vertexCountPerSide);
            int bottom0 = firstSkirtVertex + i;
            int bottom1 = firstSkirtVertex + i + 1;

            indices[indexCursor++] = top0;
            indices[indexCursor++] = bottom0;
            indices[indexCursor++] = top1;
            indices[indexCursor++] = top1;
            indices[indexCursor++] = bottom0;
            indices[indexCursor++] = bottom1;
        }

        vertexCursor += vertexCountPerSide;
    }

    private static int SurfaceIndexForEdge(int edge, int offset, int resolution, int vertexCountPerSide)
    {
        return edge switch
        {
            0 => Index(offset, 0, vertexCountPerSide),
            1 => Index(resolution, offset, vertexCountPerSide),
            2 => Index(resolution - offset, resolution, vertexCountPerSide),
            3 => Index(0, resolution - offset, vertexCountPerSide),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Terrain skirt edge must be between 0 and 3.")
        };
    }

    private static void BuildTerrainFeatures(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals,
        TerrainRouteCorridorIndex routeCorridors,
        TerrainRouteCorridorSegment[] corridorSegments,
        TerrainPointOfInterestIndex pointOfInterestIndex,
        CancellationToken cancellationToken,
        out TerrainScatterInstance[] scatterInstances,
        out TerrainLandmarkData[] landmarks)
    {
        var scatter = new List<TerrainScatterInstance>(160);
        var landmarkList = new List<TerrainLandmarkData>(4);
        Vector2 origin = coord.Origin(profile.ChunkSize);
        bool hasCorridors = corridorSegments.Length > 0;
        TerrainWorldPointOfInterest[] plannedPoints = pointOfInterestIndex.GetPoints(coord);

        if (lod <= 2)
        {
            int cells = lod == 0 ? 14 : lod == 1 ? 9 : 5;
            for (int z = 0; z < cells; z++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int x = 0; x < cells; x++)
                {
                    float jx = Hash01(coord.X, coord.Z, x * 193 + z * 389, profile.Seed);
                    float jz = Hash01(coord.X, coord.Z, x * 557 + z * 263, profile.Seed + 17);
                    float localX = (x + 0.18f + jx * 0.64f) / cells * profile.ChunkSize;
                    float localZ = (z + 0.18f + jz * 0.64f) / cells * profile.ChunkSize;
                    float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
                    Vector2 world = new(origin.X + localX, origin.Y + localZ);
                    TerrainRouteCorridorSample corridor = hasCorridors
                        ? routeCorridors.Sample(world, corridorSegments)
                        : TerrainRouteCorridorSample.None;

                    Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
                    float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
                    TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
                    bool isTidalMangroveFlat = IsMangroveTidalFlat(height, slope, field, profile);
                    if (height < profile.SeaLevel + 6.0f &&
                        !isTidalMangroveFlat &&
                        (!corridor.HasInfluence || corridor.CoreStrength < 0.32f))
                    {
                        continue;
                    }

                    float roll = Hash01(coord.X, coord.Z, x * 881 + z * 977, profile.Seed + 31);

                    if (IsInsidePointFootprint(world, plannedPoints, profile, minimumInfluence: 0.08f))
                    {
                        continue;
                    }

                    if (lod <= 1 && corridor.HasInfluence)
                    {
                        AddRouteCorridorScatter(
                            coord,
                            profile,
                            x,
                            z,
                            localX,
                            localZ,
                            height,
                            slope,
                            field,
                            corridor,
                            scatter);
                    }

                    if (corridor.HasInfluence && (corridor.CoreStrength > 0.04f || corridor.Influence > 0.58f))
                    {
                        continue;
                    }

                    bool placedNaturalScatter = false;
                    if (slope < 0.30f &&
                        field.Moisture > 0.47f &&
                        field.Temperature > 0.24f &&
                        field.River < 0.78f &&
                        field.Traversability > 0.35f &&
                        field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Lowland or TerrainLandscapeKind.RiverValley or TerrainLandscapeKind.Wetland &&
                        roll < 0.44f)
                    {
                        float scale = 2.2f + Hash01(coord.X, coord.Z, x * 1237 + z * 2011, profile.Seed + 43) * 3.4f;
                        float rotation = Hash01(coord.X, coord.Z, x * 719 + z * 911, profile.Seed + 59) * Mathf.Pi * 2.0f;
                        Color tint = new Color(0.22f, 0.44f, 0.19f).Lerp(new Color(0.08f, 0.25f, 0.12f), field.Moisture);
                        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Tree, new Vector3(localX, height, localZ), rotation, scale, tint));
                        placedNaturalScatter = true;
                    }
                    else if ((slope > 0.35f ||
                            height > profile.SeaLevel + 360.0f ||
                            field.HazardPotential > 0.56f ||
                            field.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif) &&
                        roll < 0.38f)
                    {
                        float scale = 1.3f + Hash01(coord.X, coord.Z, x * 4567 + z * 3461, profile.Seed + 61) * 3.1f;
                        float rotation = Hash01(coord.X, coord.Z, x * 2467 + z * 6421, profile.Seed + 67) * Mathf.Pi * 2.0f;
                        Color tint = new Color(0.36f, 0.35f, 0.32f).Lerp(new Color(0.55f, 0.54f, 0.49f), Mathf.Clamp(slope, 0.0f, 1.0f));
                        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Rock, new Vector3(localX, height, localZ), rotation, scale, tint));
                        placedNaturalScatter = true;
                    }

                    AddBiomeSurfaceScatter(
                        coord,
                        profile,
                        x,
                        z,
                        localX,
                        localZ,
                        height,
                        slope,
                        field,
                        placedNaturalScatter,
                        scatter);

                    if (lod <= 1)
                    {
                        AddGameplayScatter(
                            coord,
                            profile,
                            x,
                            z,
                            localX,
                            localZ,
                            height,
                            slope,
                            field,
                            scatter);
                    }
                }
            }
        }

        if (lod <= 1)
        {
            AddPlannedPoiLandmarks(coord, profile, resolution, vertexCountPerSide, step, heights, fields, normals, plannedPoints, corridorSegments, scatter, landmarkList);
            if (landmarkList.Count == 0)
            {
                AddBestLandmark(coord, profile, resolution, vertexCountPerSide, step, heights, fields, normals, cancellationToken, scatter, landmarkList);
                AddScenicNaturalLandmarks(coord, profile, resolution, vertexCountPerSide, step, heights, fields, normals, cancellationToken, scatter, landmarkList);
            }
        }

        scatterInstances = scatter.ToArray();
        landmarks = landmarkList.ToArray();
    }

    private static float SampleHeightBilinear(float localX, float localZ, int resolution, float step, float[] heights, int vertexCountPerSide)
    {
        float gx = Mathf.Clamp(localX / step, 0.0f, resolution);
        float gz = Mathf.Clamp(localZ / step, 0.0f, resolution);
        int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, resolution);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, resolution);
        int x1 = Mathf.Min(resolution, x0 + 1);
        int z1 = Mathf.Min(resolution, z0 + 1);
        float tx = gx - x0;
        float tz = gz - z0;

        float a = heights[Index(x0, z0, vertexCountPerSide)];
        float b = heights[Index(x1, z0, vertexCountPerSide)];
        float c = heights[Index(x0, z1, vertexCountPerSide)];
        float d = heights[Index(x1, z1, vertexCountPerSide)];
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static TerrainWorldField SampleFieldBilinear(
        float localX,
        float localZ,
        int resolution,
        float step,
        TerrainWorldField[] fields,
        int vertexCountPerSide)
    {
        float gx = Mathf.Clamp(localX / step, 0.0f, resolution);
        float gz = Mathf.Clamp(localZ / step, 0.0f, resolution);
        int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, resolution);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, resolution);
        int x1 = Mathf.Min(resolution, x0 + 1);
        int z1 = Mathf.Min(resolution, z0 + 1);
        float tx = gx - x0;
        float tz = gz - z0;

        TerrainWorldField a = fields[Index(x0, z0, vertexCountPerSide)];
        TerrainWorldField b = fields[Index(x1, z0, vertexCountPerSide)];
        TerrainWorldField c = fields[Index(x0, z1, vertexCountPerSide)];
        TerrainWorldField d = fields[Index(x1, z1, vertexCountPerSide)];

        float height = Bilinear(a.Height, b.Height, c.Height, d.Height, tx, tz);
        float river = Bilinear(a.River, b.River, c.River, d.River, tx, tz);
        float moisture = Bilinear(a.Moisture, b.Moisture, c.Moisture, d.Moisture, tx, tz);
        float temperature = Bilinear(a.Temperature, b.Temperature, c.Temperature, d.Temperature, tx, tz);
        float scenicPotential = Bilinear(a.ScenicPotential, b.ScenicPotential, c.ScenicPotential, d.ScenicPotential, tx, tz);
        float traversability = Bilinear(a.Traversability, b.Traversability, c.Traversability, d.Traversability, tx, tz);
        float exposure = Bilinear(a.Exposure, b.Exposure, c.Exposure, d.Exposure, tx, tz);
        float resourcePotential = Bilinear(a.ResourcePotential, b.ResourcePotential, c.ResourcePotential, d.ResourcePotential, tx, tz);
        float hazardPotential = Bilinear(a.HazardPotential, b.HazardPotential, c.HazardPotential, d.HazardPotential, tx, tz);
        float encounterPotential = Bilinear(a.EncounterPotential, b.EncounterPotential, c.EncounterPotential, d.EncounterPotential, tx, tz);

        TerrainWorldField nearest = fields[Index(
            Mathf.Clamp(Mathf.RoundToInt(gx), 0, resolution),
            Mathf.Clamp(Mathf.RoundToInt(gz), 0, resolution),
            vertexCountPerSide)];

        return nearest with
        {
            Height = height,
            River = river,
            Moisture = moisture,
            Temperature = temperature,
            ScenicPotential = scenicPotential,
            Traversability = traversability,
            Exposure = exposure,
            ResourcePotential = resourcePotential,
            HazardPotential = hazardPotential,
            EncounterPotential = encounterPotential
        };
    }

    private static Vector3 SampleNearestNormal(float localX, float localZ, int resolution, float step, Vector3[] normals, int vertexCountPerSide)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(localX / step), 0, resolution);
        int z = Mathf.Clamp(Mathf.RoundToInt(localZ / step), 0, resolution);
        return normals[Index(x, z, vertexCountPerSide)];
    }

    private static float Bilinear(float a, float b, float c, float d, float tx, float tz)
    {
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static float Hash01(int x, int z, int salt, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B9u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)z * 0x85EBCA6Bu;
            h = (h << 17) | (h >> 15);
            h ^= (uint)salt * 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215.0f;
        }
    }

    private static Vector3 CalculateGridNormal(
        int x,
        int z,
        int resolution,
        int vertexCountPerSide,
        float[] heights,
        float step)
    {
        int leftX = Mathf.Max(0, x - 1);
        int rightX = Mathf.Min(resolution, x + 1);
        int downZ = Mathf.Max(0, z - 1);
        int upZ = Mathf.Min(resolution, z + 1);

        float left = heights[Index(leftX, z, vertexCountPerSide)];
        float right = heights[Index(rightX, z, vertexCountPerSide)];
        float down = heights[Index(x, downZ, vertexCountPerSide)];
        float up = heights[Index(x, upZ, vertexCountPerSide)];

        return new Vector3(left - right, step * 2.0f, down - up).Normalized();
    }

    private static float ApplyRouteCorridorHeight(float height, TerrainRouteCorridorSample corridor)
    {
        float strength = corridor.Kind switch
        {
            TerrainRouteKind.RidgePass => corridor.CoreStrength * 0.52f + corridor.Influence * 0.18f,
            TerrainRouteKind.ScenicTrail => corridor.CoreStrength * 0.58f + corridor.Influence * 0.20f,
            TerrainRouteKind.CoastalPath => corridor.CoreStrength * 0.70f + corridor.Influence * 0.24f,
            _ => corridor.CoreStrength * 0.74f + corridor.Influence * 0.26f
        };

        strength = Mathf.Clamp(strength, 0.0f, 0.82f);
        return Mathf.Lerp(height, corridor.TargetHeight, strength);
    }

    private static Color BlendRouteSurfaceColor(Color baseColor, TerrainRouteCorridorSample corridor)
    {
        Color routeColor = corridor.Kind switch
        {
            TerrainRouteKind.RiverRoad => new Color(0.35f, 0.45f, 0.38f),
            TerrainRouteKind.RidgePass => new Color(0.44f, 0.43f, 0.39f),
            TerrainRouteKind.CoastalPath => new Color(0.55f, 0.50f, 0.36f),
            TerrainRouteKind.ScenicTrail => new Color(0.50f, 0.42f, 0.25f),
            _ => new Color(0.45f, 0.36f, 0.23f)
        };

        float blend = Mathf.Clamp(corridor.CoreStrength * 0.52f + corridor.Influence * 0.20f, 0.0f, 0.62f);
        return baseColor.Lerp(routeColor, blend);
    }

    private static int Index(int x, int z, int vertexCountPerSide)
    {
        return z * vertexCountPerSide + x;
    }

}
