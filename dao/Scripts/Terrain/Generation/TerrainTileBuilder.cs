using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    public static TerrainTileData Build(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        bool includeCollision,
        CancellationToken cancellationToken = default)
    {
        return Build(coord, lod, profile, includeCollision, TerrainRouteCorridorIndex.Empty, cancellationToken);
    }

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
        if (profile.UseNativeSamplerWhenAvailable)
        {
            nativeFieldSamples = ArrayPool<float>.Shared.Rent(nativeFieldSampleCount);
            returnNativeFieldSamples = true;
            useNativeFields = NativeTerrainBridge.TrySampleFieldGrid(coord, resolution, profile, nativeFieldSamples, nativeFieldSampleCount);
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

        try
        {

        var surfaceVertices = new Vector3[vertexCount];
        var surfaceNormals = new Vector3[vertexCount];
        var surfaceUvs = new Vector2[vertexCount];
        var surfaceColors = new Color[vertexCount];
        var heights = new float[vertexCount];
        var fields = new TerrainWorldField[vertexCount];
        TerrainRouteCorridorSegment[] corridorSegments = routeCorridors.GetSegments(coord);
        bool hasCorridors = corridorSegments.Length > 0;
        TerrainRouteCorridorSample[] corridorSamples = hasCorridors ? new TerrainRouteCorridorSample[vertexCount] : [];
        TerrainWorldPointOfInterest[] pointInfluences = pointOfInterestIndex.GetPoints(coord);
        bool hasPointInfluences = pointInfluences.Length > 0;
        TerrainPointFootprintSample[] footprintSamples = hasPointInfluences ? new TerrainPointFootprintSample[vertexCount] : [];
        TerrainSettlementLayoutDescriptor[] settlementLayouts = hasPointInfluences
            ? BuildSettlementLayoutDescriptors(pointInfluences, corridorSegments, profile)
            : [];
        bool hasSettlementLayouts = settlementLayouts.Length > 0;
        TerrainSettlementLayoutSample[] settlementLayoutSamples = hasSettlementLayouts ? new TerrainSettlementLayoutSample[vertexCount] : [];

        float minHeight = float.PositiveInfinity;
        float maxHeight = float.NegativeInfinity;

        for (int z = 0; z <= resolution; z++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int x = 0; x <= resolution; x++)
            {
                int index = Index(x, z, vertexCountPerSide);
                float localX = x * step;
                float localZ = z * step;
                Vector2 world = new(origin.X + localX, origin.Y + localZ);
                TerrainWorldField field = useNativeFields
                    ? TerrainWorldFieldSampler.SampleNativeFieldGrid(world, profile, nativeFieldSamples, index)
                    : useNativeHeights
                    ? TerrainWorldFieldSampler.SampleKnownHeight(world, profile, nativeHeights[index])
                    : TerrainWorldFieldSampler.Sample(world, profile);
                float height = field.Height;
                TerrainRouteCorridorSample corridor = hasCorridors
                    ? routeCorridors.Sample(world, corridorSegments)
                    : TerrainRouteCorridorSample.None;

                if (corridor.HasInfluence)
                {
                    height = ApplyRouteCorridorHeight(height, corridor);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.86f, corridor.CoreStrength))
                    };
                    corridorSamples[index] = corridor;
                }

                TerrainPointFootprintSample footprint = hasPointInfluences
                    ? SamplePointFootprint(world, pointInfluences, profile)
                    : TerrainPointFootprintSample.None;

                if (footprint.HasInfluence)
                {
                    height = ApplyPointFootprintHeight(height, footprint);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.92f, footprint.CoreStrength)),
                        EncounterPotential = Mathf.Max(field.EncounterPotential, Mathf.Lerp(field.EncounterPotential, 0.62f, footprint.CoreStrength * 0.60f))
                    };
                    footprintSamples[index] = footprint;
                }

                TerrainSettlementLayoutSample settlementLayout = hasSettlementLayouts
                    ? SampleSettlementLayout(world, settlementLayouts)
                    : TerrainSettlementLayoutSample.None;

                if (settlementLayout.HasInfluence)
                {
                    height = ApplySettlementLayoutHeight(height, settlementLayout);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.95f, settlementLayout.CoreStrength)),
                        EncounterPotential = Mathf.Max(field.EncounterPotential, Mathf.Lerp(field.EncounterPotential, 0.66f, settlementLayout.Influence * 0.45f))
                    };
                    settlementLayoutSamples[index] = settlementLayout;
                }

                surfaceVertices[index] = new Vector3(localX, height, localZ);
                surfaceUvs[index] = new Vector2(
                    world.X / profile.ChunkSize,
                    world.Y / profile.ChunkSize);
                heights[index] = height;
                fields[index] = field;

                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }
        }

        for (int z = 0; z <= resolution; z++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int x = 0; x <= resolution; x++)
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
        }

        int[] surfaceIndices = BuildIndices(resolution, vertexCountPerSide);
        Vector3[] collisionFaces = includeCollision ? BuildCollisionFaces(surfaceVertices, surfaceIndices) : [];
        cancellationToken.ThrowIfCancellationRequested();
        BuildSkirtedRenderMesh(
            resolution,
            vertexCountPerSide,
            profile.SkirtDepth,
            surfaceVertices,
            surfaceNormals,
            surfaceUvs,
            surfaceColors,
            surfaceIndices,
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
            if (returnNativeFieldSamples)
            {
                ArrayPool<float>.Shared.Return(nativeFieldSamples);
            }
        }
    }

    private static int[] BuildIndices(int resolution, int vertexCountPerSide)
    {
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
        float skirtDepth,
        Vector3[] surfaceVertices,
        Vector3[] surfaceNormals,
        Vector2[] surfaceUvs,
        Color[] surfaceColors,
        int[] surfaceIndices,
        out Vector3[] vertices,
        out Vector3[] normals,
        out Vector2[] uvs,
        out Color[] colors,
        out int[] indices)
    {
        float safeSkirtDepth = Mathf.Max(0.0f, skirtDepth);
        if (safeSkirtDepth <= 0.001f)
        {
            vertices = surfaceVertices;
            normals = surfaceNormals;
            uvs = surfaceUvs;
            colors = surfaceColors;
            indices = surfaceIndices;
            return;
        }

        int edgeVertexCount = vertexCountPerSide * 4;
        vertices = new Vector3[surfaceVertices.Length + edgeVertexCount];
        normals = new Vector3[surfaceNormals.Length + edgeVertexCount];
        uvs = new Vector2[surfaceUvs.Length + edgeVertexCount];
        colors = new Color[surfaceColors.Length + edgeVertexCount];

        surfaceVertices.CopyTo(vertices, 0);
        surfaceNormals.CopyTo(normals, 0);
        surfaceUvs.CopyTo(uvs, 0);
        surfaceColors.CopyTo(colors, 0);

        var skirtIndices = new int[surfaceIndices.Length + resolution * 4 * 6];
        surfaceIndices.CopyTo(skirtIndices, 0);

        int vertexCursor = surfaceVertices.Length;
        int indexCursor = surfaceIndices.Length;

        AddSkirtEdge(
            resolution,
            vertexCountPerSide,
            safeSkirtDepth,
            x => Index(x, 0, vertexCountPerSide),
            ref vertexCursor,
            ref indexCursor,
            vertices,
            normals,
            uvs,
            colors,
            skirtIndices);
        AddSkirtEdge(
            resolution,
            vertexCountPerSide,
            safeSkirtDepth,
            x => Index(resolution, x, vertexCountPerSide),
            ref vertexCursor,
            ref indexCursor,
            vertices,
            normals,
            uvs,
            colors,
            skirtIndices);
        AddSkirtEdge(
            resolution,
            vertexCountPerSide,
            safeSkirtDepth,
            x => Index(resolution - x, resolution, vertexCountPerSide),
            ref vertexCursor,
            ref indexCursor,
            vertices,
            normals,
            uvs,
            colors,
            skirtIndices);
        AddSkirtEdge(
            resolution,
            vertexCountPerSide,
            safeSkirtDepth,
            x => Index(0, resolution - x, vertexCountPerSide),
            ref vertexCursor,
            ref indexCursor,
            vertices,
            normals,
            uvs,
            colors,
            skirtIndices);

        indices = skirtIndices;
    }

    private static void AddSkirtEdge(
        int resolution,
        int vertexCountPerSide,
        float skirtDepth,
        Func<int, int> surfaceIndexAt,
        ref int vertexCursor,
        ref int indexCursor,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        Color[] colors,
        int[] indices)
    {
        int firstSkirtVertex = vertexCursor;

        for (int i = 0; i <= resolution; i++)
        {
            int surfaceIndex = surfaceIndexAt(i);
            vertices[vertexCursor] = vertices[surfaceIndex] - new Vector3(0.0f, skirtDepth, 0.0f);
            normals[vertexCursor] = normals[surfaceIndex];
            uvs[vertexCursor] = uvs[surfaceIndex];
            colors[vertexCursor] = colors[surfaceIndex].Darkened(0.22f);
            vertexCursor++;
        }

        for (int i = 0; i < resolution; i++)
        {
            int top0 = surfaceIndexAt(i);
            int top1 = surfaceIndexAt(i + 1);
            int bottom0 = firstSkirtVertex + i;
            int bottom1 = firstSkirtVertex + i + 1;

            indices[indexCursor++] = top0;
            indices[indexCursor++] = bottom0;
            indices[indexCursor++] = top1;
            indices[indexCursor++] = top1;
            indices[indexCursor++] = bottom0;
            indices[indexCursor++] = bottom1;
        }
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

                    if (height < profile.SeaLevel + 6.0f && (!corridor.HasInfluence || corridor.CoreStrength < 0.32f))
                    {
                        continue;
                    }

                    Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
                    float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
                    TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
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

    private static void AddRouteCorridorScatter(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int cellX,
        int cellZ,
        float localX,
        float localZ,
        float height,
        float slope,
        TerrainWorldField field,
        TerrainRouteCorridorSample corridor,
        List<TerrainScatterInstance> scatter)
    {
        if (!corridor.HasInfluence || corridor.CoreStrength < 0.48f)
        {
            return;
        }

        Vector2 direction = CorridorDirectionOrFallback(corridor, coord, cellX, cellZ, profile);
        float rotation = RouteRotation(direction);

        if (IsBridgeSpanCandidate(corridor, field, height, profile) &&
            slope < 0.48f &&
            Hash01(coord.X, coord.Z, cellX * 5501 + cellZ * 5527, profile.Seed + 229) < BridgeSpanProbability(corridor, field, height, profile))
        {
            float bridgeScale = 2.25f + corridor.CoreStrength * 1.45f;
            Color bridgeTint = RouteBridgeColor(corridor, field);
            scatter.Add(new TerrainScatterInstance(
                TerrainScatterKind.Landmark,
                new Vector3(localX, height + 0.10f, localZ),
                rotation,
                bridgeScale,
                bridgeTint,
                TerrainLandmarkKind.BridgeSpan));
            return;
        }

        if (corridor.CoreStrength < 0.62f || slope > RouteMarkerMaxSlope(corridor.Kind))
        {
            return;
        }

        float markerProbability = RouteMarkerProbability(corridor);
        if (Hash01(coord.X, coord.Z, cellX * 5639 + cellZ * 5657, profile.Seed + 233) > markerProbability)
        {
            return;
        }

        Vector2 side = new(-direction.Y, direction.X);
        float sideRoll = Hash01(coord.X, coord.Z, cellX * 5689 + cellZ * 5711, profile.Seed + 239);
        float sideSign = sideRoll < 0.5f ? -1.0f : 1.0f;
        float shoulderOffset = (2.2f + Hash01(coord.X, coord.Z, cellX * 5737 + cellZ * 5749, profile.Seed + 241) * 2.8f) * sideSign;
        Vector2 local = new(
            Mathf.Clamp(localX + side.X * shoulderOffset, 0.0f, profile.ChunkSize),
            Mathf.Clamp(localZ + side.Y * shoulderOffset, 0.0f, profile.ChunkSize));
        float markerRotation = rotation + (sideSign < 0.0f ? -0.12f : 0.12f);
        float markerScale = 1.05f + corridor.ScenicPotential * 0.36f + Hash01(coord.X, coord.Z, cellX * 5779 + cellZ * 5791, profile.Seed + 251) * 0.32f;
        Color markerTint = RouteMarkerColor(corridor, field);
        scatter.Add(new TerrainScatterInstance(
            TerrainScatterKind.Landmark,
            new Vector3(local.X, height + 0.08f, local.Y),
            markerRotation,
            markerScale,
            markerTint,
            TerrainLandmarkKind.RoadMarker));
    }

    private static Vector2 CorridorDirectionOrFallback(
        TerrainRouteCorridorSample corridor,
        TerrainTileCoord coord,
        int cellX,
        int cellZ,
        TerrainGenerationProfile profile)
    {
        if (corridor.Direction.LengthSquared() > 0.0001f)
        {
            return corridor.Direction;
        }

        float angle = Hash01(coord.X, coord.Z, cellX * 5813 + cellZ * 5821, profile.Seed + 257) * Mathf.Tau;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float RouteRotation(Vector2 direction)
    {
        return Mathf.Atan2(direction.Y, direction.X);
    }

    private static bool IsBridgeSpanCandidate(
        TerrainRouteCorridorSample corridor,
        TerrainWorldField field,
        float height,
        TerrainGenerationProfile profile)
    {
        if (corridor.CoreStrength < 0.58f)
        {
            return false;
        }

        bool riverRoad =
            corridor.Kind == TerrainRouteKind.RiverRoad &&
            (field.River > 0.62f ||
                (field.LandscapeKind == TerrainLandscapeKind.RiverValley && field.River > 0.54f) ||
                (field.LandscapeKind == TerrainLandscapeKind.Wetland && field.Moisture > 0.74f));
        bool coastalTrestle =
            corridor.Kind == TerrainRouteKind.CoastalPath &&
            height < profile.SeaLevel + 20.0f &&
            field.Moisture > 0.54f;
        bool wetlandBoardwalk =
            corridor.Kind is TerrainRouteKind.RiverRoad or TerrainRouteKind.CoastalPath &&
            field.LandscapeKind == TerrainLandscapeKind.Wetland &&
            field.Moisture > 0.78f &&
            corridor.Traversability > 0.30f;

        return riverRoad || coastalTrestle || wetlandBoardwalk;
    }

    private static float BridgeSpanProbability(
        TerrainRouteCorridorSample corridor,
        TerrainWorldField field,
        float height,
        TerrainGenerationProfile profile)
    {
        float waterProximity = Mathf.Clamp(
            field.River * 0.68f +
            field.Moisture * 0.18f +
            (1.0f - Mathf.SmoothStep(profile.SeaLevel + 4.0f, profile.SeaLevel + 32.0f, height)) * 0.14f,
            0.0f,
            1.0f);
        float baseProbability = corridor.Kind switch
        {
            TerrainRouteKind.RiverRoad => 0.08f,
            TerrainRouteKind.CoastalPath => 0.07f,
            TerrainRouteKind.ScenicTrail => 0.04f,
            _ => 0.05f
        };

        return Mathf.Clamp(baseProbability + waterProximity * 0.12f + corridor.CoreStrength * 0.04f, 0.04f, 0.24f);
    }

    private static float RouteMarkerProbability(TerrainRouteCorridorSample corridor)
    {
        float baseProbability = corridor.Kind switch
        {
            TerrainRouteKind.RiverRoad => 0.18f,
            TerrainRouteKind.RidgePass => 0.14f,
            TerrainRouteKind.CoastalPath => 0.20f,
            TerrainRouteKind.ScenicTrail => 0.18f,
            _ => 0.17f
        };

        return Mathf.Clamp(
            baseProbability +
            corridor.ScenicPotential * 0.07f +
            corridor.Traversability * 0.04f,
            0.12f,
            0.28f);
    }

    private static float RouteMarkerMaxSlope(TerrainRouteKind kind)
    {
        return kind switch
        {
            TerrainRouteKind.RidgePass => 0.56f,
            TerrainRouteKind.ScenicTrail => 0.44f,
            _ => 0.38f
        };
    }

    private static Color RouteMarkerColor(TerrainRouteCorridorSample corridor, TerrainWorldField field)
    {
        Color baseColor = corridor.Kind switch
        {
            TerrainRouteKind.RiverRoad => new Color(0.36f, 0.44f, 0.36f),
            TerrainRouteKind.RidgePass => new Color(0.50f, 0.48f, 0.42f),
            TerrainRouteKind.CoastalPath => new Color(0.62f, 0.56f, 0.38f),
            TerrainRouteKind.ScenicTrail => new Color(0.64f, 0.48f, 0.25f),
            _ => new Color(0.52f, 0.40f, 0.24f)
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp(field.ScenicPotential * 0.10f, 0.0f, 0.10f));
    }

    private static Color RouteBridgeColor(TerrainRouteCorridorSample corridor, TerrainWorldField field)
    {
        Color baseColor = corridor.Kind == TerrainRouteKind.CoastalPath
            ? new Color(0.54f, 0.48f, 0.34f)
            : new Color(0.42f, 0.34f, 0.26f);
        return baseColor.Lerp(new Color(0.56f, 0.58f, 0.52f), Mathf.Clamp(field.Moisture * 0.16f, 0.0f, 0.16f));
    }

    private static void AddPlannedPoiLandmarks(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals,
        TerrainWorldPointOfInterest[] plannedPoints,
        TerrainRouteCorridorSegment[] corridorSegments,
        List<TerrainScatterInstance> scatter,
        List<TerrainLandmarkData> landmarks)
    {
        if (plannedPoints.Length == 0)
        {
            return;
        }

        Vector2 origin = coord.Origin(profile.ChunkSize);
        foreach (TerrainWorldPointOfInterest point in plannedPoints)
        {
            AddSettlementInteriorScatter(
                coord,
                profile,
                resolution,
                vertexCountPerSide,
                step,
                heights,
                fields,
                point,
                corridorSegments,
                origin,
                scatter);

            float localX = point.WorldPosition.X - origin.X;
            float localZ = point.WorldPosition.Y - origin.Y;
            if (localX < 0.0f || localZ < 0.0f || localX > profile.ChunkSize || localZ > profile.ChunkSize)
            {
                continue;
            }

            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            if (height < profile.SeaLevel - 2.0f)
            {
                continue;
            }

            Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
            float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
            TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
            TerrainLandmarkKind kind = LandmarkKindFor(point);
            float score = Mathf.Clamp(
                point.Score * 0.70f +
                field.ScenicPotential * 0.16f +
                field.Traversability * 0.10f +
                (1.0f - Mathf.Clamp(slope * 1.8f, 0.0f, 1.0f)) * 0.04f,
                0.0f,
                1.0f);
            float rotation = Hash01(coord.X, coord.Z, point.Id * 104_729, profile.Seed + 211) * Mathf.Pi * 2.0f;
            float scale = LandmarkScaleFor(kind, point.Score);
            Color tint = LandmarkColorFor(kind, field);

            var localPosition = new Vector3(localX, height, localZ);
            landmarks.Add(new TerrainLandmarkData(kind, localPosition, score, $"POI_{point.Id:00}_{point.Kind}"));
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Landmark, localPosition, rotation, scale, tint, kind));
        }
    }

    private static void AddSettlementInteriorScatter(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        TerrainWorldPointOfInterest point,
        TerrainRouteCorridorSegment[] corridorSegments,
        Vector2 origin,
        List<TerrainScatterInstance> scatter)
    {
        if (point.SettlementTier == TerrainSettlementTier.None)
        {
            return;
        }

        float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
        if (!TileIntersectsCircle(origin, profile.ChunkSize, point.WorldPosition, radius))
        {
            return;
        }

        int count = point.SettlementTier switch
        {
            TerrainSettlementTier.Town => 14,
            TerrainSettlementTier.OasisHub => 10,
            _ => 7
        };
        Vector2 axis = SettlementLayoutAxis(point, corridorSegments, profile);
        Vector2 side = new(-axis.Y, axis.X);

        for (int i = 0; i < count; i++)
        {
            TerrainLandmarkKind kind = SettlementInteriorKind(point.SettlementTier, i);
            Vector2 offset = SettlementInteriorOffset(point, radius, axis, side, i, count);
            Vector2 world = point.WorldPosition + offset;
            float localX = world.X - origin.X;
            float localZ = world.Y - origin.Y;
            if (localX < 0.0f || localZ < 0.0f || localX > profile.ChunkSize || localZ > profile.ChunkSize)
            {
                continue;
            }

            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            if (height < profile.SeaLevel - 2.0f)
            {
                continue;
            }

            TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
            float rotation = SettlementInteriorRotation(point, axis, i, profile);
            float scale = SettlementInteriorScale(point.SettlementTier, point.Score, coord, i, profile);
            Color tint = SettlementInteriorColor(kind, field, coord, i, profile);
            scatter.Add(new TerrainScatterInstance(
                TerrainScatterKind.Landmark,
                new Vector3(localX, height, localZ),
                rotation,
                scale,
                tint,
                kind));
        }
    }

    private static TerrainLandmarkKind SettlementInteriorKind(TerrainSettlementTier tier, int index)
    {
        return tier switch
        {
            TerrainSettlementTier.Town => index % 7 == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : TerrainLandmarkKind.TownBlock,
            TerrainSettlementTier.OasisHub => index == 0
                ? TerrainLandmarkKind.OasisPool
                : index % 5 == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : TerrainLandmarkKind.OasisCanopy,
            TerrainSettlementTier.Village => index == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : TerrainLandmarkKind.VillageHouse,
            _ => TerrainLandmarkKind.Settlement
        };
    }

    private static Vector2 SettlementInteriorOffset(
        TerrainWorldPointOfInterest point,
        float radius,
        Vector2 axis,
        Vector2 side,
        int index,
        int count)
    {
        if (point.SettlementTier == TerrainSettlementTier.OasisHub && index == 0)
        {
            return Vector2.Zero;
        }

        if (point.SettlementTier == TerrainSettlementTier.Town)
        {
            const int columns = 4;
            int rows = Mathf.CeilToInt(count / (float)columns);
            int column = index % columns;
            int row = index / columns;
            float blockX = (column - (columns - 1) * 0.5f) * radius * 0.18f;
            float blockZ = (row - (rows - 1) * 0.5f) * radius * 0.20f;
            float jitterX = (Hash01(point.Id, index, 1301, 17) - 0.5f) * radius * 0.055f;
            float jitterZ = (Hash01(point.Id, index, 1303, 19) - 0.5f) * radius * 0.055f;
            return axis * (blockX + jitterX) + side * (blockZ + jitterZ);
        }

        float angle = (index / (float)Mathf.Max(1, count)) * Mathf.Tau +
            Hash01(point.Id, index, 1319, 23) * 0.38f;
        float ring = point.SettlementTier == TerrainSettlementTier.OasisHub
            ? radius * Mathf.Lerp(0.34f, 0.56f, Hash01(point.Id, index, 1321, 29))
            : radius * Mathf.Lerp(0.20f, 0.48f, Hash01(point.Id, index, 1327, 31));
        float along = Mathf.Cos(angle) * ring;
        float across = Mathf.Sin(angle) * ring;
        return axis * along + side * across;
    }

    private static Vector2 SettlementLayoutAxis(
        TerrainWorldPointOfInterest point,
        TerrainRouteCorridorSegment[] corridorSegments,
        TerrainGenerationProfile profile)
    {
        Vector2 best = Vector2.Zero;
        float bestDistanceSquared = float.PositiveInfinity;
        float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile) * 2.25f;
        float maxDistanceSquared = radius * radius;

        foreach (TerrainRouteCorridorSegment segment in corridorSegments)
        {
            float t = ClosestPointT(point.WorldPosition, segment.From, segment.To);
            Vector2 closest = segment.From.Lerp(segment.To, t);
            float distanceSquared = point.WorldPosition.DistanceSquaredTo(closest);
            if (distanceSquared >= bestDistanceSquared || distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            Vector2 direction = segment.To - segment.From;
            if (direction.LengthSquared() <= 0.001f)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            best = direction.Normalized();
        }

        if (best != Vector2.Zero)
        {
            return best;
        }

        float angle = Hash01(
            Mathf.FloorToInt(point.WorldPosition.X),
            Mathf.FloorToInt(point.WorldPosition.Y),
            point.Id * 7919,
            profile.Seed + 223) * Mathf.Tau;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float ClosestPointT(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return 0.0f;
        }

        return Mathf.Clamp((point - from).Dot(segment) / lengthSquared, 0.0f, 1.0f);
    }

    private static bool TileIntersectsCircle(Vector2 origin, float chunkSize, Vector2 center, float radius)
    {
        float nearestX = Mathf.Clamp(center.X, origin.X, origin.X + chunkSize);
        float nearestZ = Mathf.Clamp(center.Y, origin.Y, origin.Y + chunkSize);
        float dx = center.X - nearestX;
        float dz = center.Y - nearestZ;
        return (dx * dx) + (dz * dz) <= radius * radius;
    }

    private static float SettlementInteriorRotation(
        TerrainWorldPointOfInterest point,
        Vector2 axis,
        int index,
        TerrainGenerationProfile profile)
    {
        float baseRotation = Mathf.Atan2(axis.Y, axis.X);
        float jitter = (Hash01(point.Id, index, 1361, profile.Seed + 227) - 0.5f) * 0.42f;
        return point.SettlementTier == TerrainSettlementTier.OasisHub
            ? baseRotation + Mathf.Pi * 0.5f + jitter
            : baseRotation + jitter;
    }

    private static float SettlementInteriorScale(
        TerrainSettlementTier tier,
        float score,
        TerrainTileCoord coord,
        int index,
        TerrainGenerationProfile profile)
    {
        float quality = Mathf.Lerp(0.90f, 1.18f, Mathf.Clamp(score, 0.0f, 1.0f));
        float jitter = Mathf.Lerp(0.84f, 1.20f, Hash01(coord.X, coord.Z, index * 1399, profile.Seed + 229));
        float baseScale = tier switch
        {
            TerrainSettlementTier.Town => 2.95f,
            TerrainSettlementTier.OasisHub => 2.55f,
            _ => 2.30f
        };

        return baseScale * quality * jitter;
    }

    private static Color SettlementInteriorColor(
        TerrainLandmarkKind kind,
        TerrainWorldField field,
        TerrainTileCoord coord,
        int index,
        TerrainGenerationProfile profile)
    {
        Color baseColor = LandmarkColorFor(kind, field);
        Color variation = kind switch
        {
            TerrainLandmarkKind.TownBlock => new Color(0.62f, 0.42f, 0.30f),
            TerrainLandmarkKind.OasisCanopy => new Color(0.12f, 0.58f, 0.44f),
            TerrainLandmarkKind.SettlementPlaza => new Color(0.58f, 0.50f, 0.38f),
            TerrainLandmarkKind.OasisPool => new Color(0.10f, 0.36f, 0.46f),
            _ => new Color(0.58f, 0.48f, 0.31f)
        };
        float blend = Mathf.Lerp(0.18f, 0.42f, Hash01(coord.X, coord.Z, index * 1423, profile.Seed + 233));
        return baseColor.Lerp(variation, blend);
    }

    private static TerrainSettlementLayoutDescriptor[] BuildSettlementLayoutDescriptors(
        TerrainWorldPointOfInterest[] points,
        TerrainRouteCorridorSegment[] corridorSegments,
        TerrainGenerationProfile profile)
    {
        var layouts = new List<TerrainSettlementLayoutDescriptor>(points.Length);
        foreach (TerrainWorldPointOfInterest point in points)
        {
            if (point.SettlementTier == TerrainSettlementTier.None)
            {
                continue;
            }

            float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
            Vector2 axis = SettlementLayoutAxis(point, corridorSegments, profile);
            Vector2 side = new(-axis.Y, axis.X);
            layouts.Add(new TerrainSettlementLayoutDescriptor(
                point.SettlementTier,
                point.WorldPosition,
                radius,
                axis,
                side,
                TargetHeightForFootprint(point, profile)));
        }

        return layouts.ToArray();
    }

    private static TerrainSettlementLayoutSample SampleSettlementLayout(
        Vector2 world,
        TerrainSettlementLayoutDescriptor[] layouts)
    {
        TerrainSettlementLayoutSample best = TerrainSettlementLayoutSample.None;

        foreach (TerrainSettlementLayoutDescriptor layout in layouts)
        {
            Vector2 local = world - layout.Center;
            float distance = local.Length();
            if (distance > layout.Radius)
            {
                continue;
            }

            float along = local.Dot(layout.Axis);
            float across = local.Dot(layout.Side);
            float plazaStrength = 1.0f - Mathf.SmoothStep(layout.Radius * 0.08f, layout.Radius * 0.18f, distance);
            float streetStrength;
            float oasisGreenStrength = 0.0f;
            float oasisWaterStrength = 0.0f;

            if (layout.Tier == TerrainSettlementTier.Town)
            {
                float mainStreet = LineStrength(across, along, layout.Radius * 0.050f, layout.Radius * 0.68f);
                float crossStreet = LineStrength(along, across, layout.Radius * 0.046f, layout.Radius * 0.54f);
                float marketLane = LineStrength(across - layout.Radius * 0.22f, along, layout.Radius * 0.034f, layout.Radius * 0.48f) * 0.58f;
                streetStrength = Mathf.Max(Mathf.Max(mainStreet, crossStreet), marketLane);
                plazaStrength *= 1.08f;
            }
            else if (layout.Tier == TerrainSettlementTier.OasisHub)
            {
                float ring = 1.0f - Mathf.SmoothStep(layout.Radius * 0.028f, layout.Radius * 0.084f, Mathf.Abs(distance - layout.Radius * 0.42f));
                float entryPath = LineStrength(across, along, layout.Radius * 0.044f, layout.Radius * 0.62f);
                streetStrength = Mathf.Max(ring, entryPath * 0.82f);
                plazaStrength *= 0.72f;
                oasisGreenStrength = (1.0f - Mathf.SmoothStep(layout.Radius * 0.30f, layout.Radius * 0.62f, distance)) * 0.85f;
                oasisWaterStrength = 1.0f - Mathf.SmoothStep(layout.Radius * 0.12f, layout.Radius * 0.25f, distance);
            }
            else
            {
                float mainLane = LineStrength(across, along, layout.Radius * 0.040f, layout.Radius * 0.56f);
                float crossLane = LineStrength(along, across, layout.Radius * 0.032f, layout.Radius * 0.42f) * 0.62f;
                streetStrength = Mathf.Max(mainLane, crossLane);
                plazaStrength *= 0.86f;
            }

            float influence = Mathf.Clamp(Mathf.Max(Mathf.Max(Mathf.Max(streetStrength, plazaStrength), oasisGreenStrength * 0.72f), oasisWaterStrength), 0.0f, 1.0f);
            if (influence <= best.Influence)
            {
                continue;
            }

            best = new TerrainSettlementLayoutSample(
                true,
                layout.Tier,
                influence,
                Mathf.Clamp(Mathf.Max(streetStrength, plazaStrength), 0.0f, 1.0f),
                Mathf.Clamp(streetStrength, 0.0f, 1.0f),
                Mathf.Clamp(plazaStrength, 0.0f, 1.0f),
                Mathf.Clamp(oasisGreenStrength, 0.0f, 1.0f),
                Mathf.Clamp(oasisWaterStrength, 0.0f, 1.0f),
                layout.TargetHeight);
        }

        return best;
    }

    private static float LineStrength(float crossDistance, float alongDistance, float width, float halfLength)
    {
        float along = Mathf.Abs(alongDistance);
        if (along > halfLength)
        {
            return 0.0f;
        }

        float cross = Mathf.Abs(crossDistance);
        float crossStrength = 1.0f - Mathf.SmoothStep(width, width * 2.4f, cross);
        float endFade = 1.0f - Mathf.SmoothStep(halfLength * 0.78f, halfLength, along);
        return Mathf.Clamp(crossStrength * endFade, 0.0f, 1.0f);
    }

    private static void AddBestLandmark(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals,
        CancellationToken cancellationToken,
        List<TerrainScatterInstance> scatter,
        List<TerrainLandmarkData> landmarks)
    {
        if (!TileHasWaterfallPotential(profile, resolution, vertexCountPerSide, heights, fields, normals))
        {
            return;
        }

        TerrainLandmarkData best = default;
        float bestScore = 0.0f;

        for (int i = 0; i < 8; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            float localX = (0.15f + Hash01(coord.X, coord.Z, i * 173, profile.Seed + 101) * 0.70f) * profile.ChunkSize;
            float localZ = (0.15f + Hash01(coord.X, coord.Z, i * 277, profile.Seed + 103) * 0.70f) * profile.ChunkSize;
            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
            float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
            TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
            float flatness = 1.0f - Mathf.Clamp(slope * 2.2f, 0.0f, 1.0f);
            float heightScore = Mathf.Clamp((height - profile.SeaLevel - 140.0f) / 560.0f, 0.0f, 1.0f);
            float rarity = Hash01(coord.X, coord.Z, i * 421, profile.Seed + 107);

            TerrainLandmarkKind kind = TerrainLandmarkKind.Vista;
            float score =
                field.ScenicPotential * 0.52f +
                heightScore * 0.20f +
                flatness * 0.16f +
                field.Traversability * 0.08f +
                rarity * 0.04f;

            if (field.River > 0.70f && height > profile.SeaLevel + 10.0f && slope < 0.24f)
            {
                kind = TerrainLandmarkKind.RiverCrossing;
                score = 0.70f + field.River * 0.16f + flatness * 0.10f + field.Traversability * 0.04f;
            }
            else if (field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau &&
                height > profile.SeaLevel + 300.0f &&
                slope is > 0.14f and < 0.46f)
            {
                kind = TerrainLandmarkKind.MountainPass;
                score = 0.52f + field.ScenicPotential * 0.24f + heightScore * 0.14f + (1.0f - Mathf.Abs(slope - 0.28f) * 2.0f) * 0.10f;
            }
            else if (rarity > 0.92f && slope < 0.26f && field.Traversability > 0.22f)
            {
                kind = TerrainLandmarkKind.AncientStone;
                score = 0.74f + field.ScenicPotential * 0.12f + flatness * 0.10f + heightScore * 0.04f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = new TerrainLandmarkData(kind, new Vector3(localX, height, localZ), score, $"{kind}_{coord.X}_{coord.Z}");
            }
        }

        if (bestScore < 0.66f)
        {
            return;
        }

        landmarks.Add(best);
        float rotation = Hash01(coord.X, coord.Z, 8191, profile.Seed + 109) * Mathf.Pi * 2.0f;
        float scale = best.Kind == TerrainLandmarkKind.AncientStone ? 7.0f : 4.6f;
        Color tint = best.Kind == TerrainLandmarkKind.RiverCrossing
            ? new Color(0.42f, 0.48f, 0.45f)
            : new Color(0.52f, 0.50f, 0.44f);
        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Landmark, best.LocalPosition, rotation, scale, tint, best.Kind));
    }

    private static void AddScenicNaturalLandmarks(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals,
        CancellationToken cancellationToken,
        List<TerrainScatterInstance> scatter,
        List<TerrainLandmarkData> landmarks)
    {
        if (!TileHasDramaticNaturalPotential(profile, resolution, vertexCountPerSide, heights, fields, normals))
        {
            return;
        }

        TerrainLandmarkData best = default;
        float bestScore = 0.0f;

        for (int i = 0; i < 12; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            float localX = (0.10f + Hash01(coord.X, coord.Z, i * 1559, profile.Seed + 307) * 0.80f) * profile.ChunkSize;
            float localZ = (0.10f + Hash01(coord.X, coord.Z, i * 1601, profile.Seed + 311) * 0.80f) * profile.ChunkSize;
            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            if (height < profile.SeaLevel + 96.0f)
            {
                continue;
            }

            Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
            float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
            TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
            float elevation = Mathf.SmoothStep(profile.SeaLevel + 120.0f, profile.SeaLevel + profile.HeightScale * 0.70f, height);
            TerrainLandmarkKind kind = TerrainLandmarkKind.Waterfall;
            float score = ScoreWaterfallLandmark(field, slope, elevation);
            ConsiderNaturalLandmark(TerrainLandmarkKind.DuneCrest, ScoreDuneCrestLandmark(field, slope, elevation), ref kind, ref score);
            ConsiderNaturalLandmark(TerrainLandmarkKind.DesertMonolith, ScoreDesertMonolithLandmark(field, slope, elevation), ref kind, ref score);
            ConsiderNaturalLandmark(TerrainLandmarkKind.CanyonNeedle, ScoreCanyonNeedleLandmark(field, slope, elevation), ref kind, ref score);
            ConsiderNaturalLandmark(TerrainLandmarkKind.IceSpire, ScoreIceSpireLandmark(field, slope, elevation), ref kind, ref score);

            if (score > bestScore)
            {
                bestScore = score;
                best = new TerrainLandmarkData(
                    kind,
                    new Vector3(localX, height, localZ),
                    Mathf.Clamp(score, 0.0f, 1.0f),
                    $"{kind}_{coord.X}_{coord.Z}");
            }
        }

        if (bestScore < NaturalLandmarkThreshold(best.Kind))
        {
            return;
        }

        landmarks.Add(best);
        float rotation = Hash01(coord.X, coord.Z, 1621, profile.Seed + 313) * Mathf.Pi * 2.0f;
        float scale = NaturalLandmarkScale(best.Kind, best.Score);
        Color tint = NaturalLandmarkColor(best.Kind, best.Score);
        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Landmark, best.LocalPosition, rotation, scale, tint, best.Kind));
    }

    private static void ConsiderNaturalLandmark(
        TerrainLandmarkKind candidateKind,
        float candidateScore,
        ref TerrainLandmarkKind bestKind,
        ref float bestScore)
    {
        if (candidateScore > bestScore)
        {
            bestKind = candidateKind;
            bestScore = candidateScore;
        }
    }

    private static float ScoreWaterfallLandmark(TerrainWorldField field, float slope, float elevation)
    {
        float score =
            Mathf.SmoothStep(0.48f, 0.86f, field.River) * 0.38f +
            Mathf.SmoothStep(0.16f, 0.42f, slope) * 0.22f +
            elevation * 0.18f +
            field.ScenicPotential * 0.18f +
            field.Exposure * 0.04f;

        if (field.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.RiverValley)
        {
            score += 0.08f;
        }

        return score;
    }

    private static float ScoreDuneCrestLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (!IsDesertLike(field) || slope > 0.22f)
        {
            return 0.0f;
        }

        float flatness = 1.0f - Mathf.Clamp(slope * 3.4f, 0.0f, 1.0f);
        float dryness = Mathf.Clamp(1.0f - field.Moisture, 0.0f, 1.0f);
        return 0.44f +
            dryness * 0.18f +
            field.Temperature * 0.12f +
            field.ScenicPotential * 0.14f +
            field.Exposure * 0.08f +
            flatness * 0.08f +
            elevation * 0.04f;
    }

    private static float ScoreDesertMonolithLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (!IsDesertLike(field) || slope is < 0.08f or > 0.42f)
        {
            return 0.0f;
        }

        float slopeFit = 1.0f - Mathf.Clamp(Mathf.Abs(slope - 0.25f) * 3.8f, 0.0f, 1.0f);
        float dryness = Mathf.Clamp(1.0f - field.Moisture, 0.0f, 1.0f);
        return 0.36f +
            field.ScenicPotential * 0.22f +
            field.Exposure * 0.18f +
            dryness * 0.16f +
            slopeFit * 0.10f +
            elevation * 0.08f;
    }

    private static float ScoreCanyonNeedleLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (field.LandscapeKind is not (TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau) ||
            slope < 0.20f)
        {
            return 0.0f;
        }

        float slopeFit = Mathf.Clamp((slope - 0.18f) / 0.36f, 0.0f, 1.0f);
        return 0.34f +
            field.ScenicPotential * 0.24f +
            field.Exposure * 0.22f +
            elevation * 0.16f +
            slopeFit * 0.12f;
    }

    private static float ScoreIceSpireLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (field.BiomeKind != TerrainBiomeKind.Snowfield && field.LandscapeKind != TerrainLandscapeKind.Snowfield)
        {
            return 0.0f;
        }

        float slopeFit = 1.0f - Mathf.Clamp(Mathf.Abs(slope - 0.24f) * 3.0f, 0.0f, 1.0f);
        return 0.38f +
            field.ScenicPotential * 0.20f +
            field.Exposure * 0.20f +
            elevation * 0.18f +
            slopeFit * 0.10f +
            Mathf.Clamp(1.0f - field.Temperature, 0.0f, 1.0f) * 0.06f;
    }

    private static bool IsDesertLike(TerrainWorldField field)
    {
        return field.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis &&
            field.Temperature > 0.34f &&
            field.Moisture < 0.62f;
    }

    private static float NaturalLandmarkThreshold(TerrainLandmarkKind kind)
    {
        return kind switch
        {
            TerrainLandmarkKind.Waterfall => 0.74f,
            TerrainLandmarkKind.DuneCrest => 0.68f,
            TerrainLandmarkKind.DesertMonolith => 0.66f,
            TerrainLandmarkKind.CanyonNeedle => 0.70f,
            TerrainLandmarkKind.IceSpire => 0.66f,
            _ => 0.72f
        };
    }

    private static float NaturalLandmarkScale(TerrainLandmarkKind kind, float score)
    {
        return kind switch
        {
            TerrainLandmarkKind.Waterfall => 4.8f + score * 3.2f,
            TerrainLandmarkKind.DuneCrest => 4.4f + score * 2.6f,
            TerrainLandmarkKind.DesertMonolith => 3.6f + score * 2.8f,
            TerrainLandmarkKind.CanyonNeedle => 4.2f + score * 3.0f,
            TerrainLandmarkKind.IceSpire => 3.6f + score * 2.4f,
            _ => 4.8f + score * 2.0f
        };
    }

    private static Color NaturalLandmarkColor(TerrainLandmarkKind kind, float score)
    {
        Color baseColor = kind switch
        {
            TerrainLandmarkKind.Waterfall => new Color(0.30f, 0.62f, 0.82f),
            TerrainLandmarkKind.DuneCrest => new Color(0.76f, 0.58f, 0.30f),
            TerrainLandmarkKind.DesertMonolith => new Color(0.62f, 0.42f, 0.24f),
            TerrainLandmarkKind.CanyonNeedle => new Color(0.58f, 0.36f, 0.24f),
            TerrainLandmarkKind.IceSpire => new Color(0.62f, 0.76f, 0.86f),
            _ => new Color(0.52f, 0.50f, 0.44f)
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp(score * 0.18f, 0.0f, 0.18f));
    }

    private static bool TileHasWaterfallPotential(
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals)
    {
        int stride = Mathf.Max(1, resolution / 4);
        for (int z = 0; z <= resolution; z += stride)
        {
            for (int x = 0; x <= resolution; x += stride)
            {
                int index = Index(x, z, vertexCountPerSide);
                float height = heights[index];
                if (height < profile.SeaLevel + 80.0f)
                {
                    continue;
                }

                TerrainWorldField field = fields[index];
                if (field.River < 0.36f || field.ScenicPotential < 0.24f)
                {
                    continue;
                }

                float slope = 1.0f - Mathf.Clamp(normals[index].Y, 0.0f, 1.0f);
                float elevation = Mathf.SmoothStep(profile.SeaLevel + 96.0f, profile.SeaLevel + profile.HeightScale * 0.70f, height);
                float potential =
                    field.River * 0.34f +
                    field.ScenicPotential * 0.28f +
                    elevation * 0.18f +
                    slope * 0.14f +
                    field.Exposure * 0.06f;

                if (field.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.RiverValley)
                {
                    potential += 0.10f;
                }

                if (potential >= 0.54f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TileHasDramaticNaturalPotential(
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals)
    {
        int stride = Mathf.Max(1, resolution / 4);
        for (int z = 0; z <= resolution; z += stride)
        {
            for (int x = 0; x <= resolution; x += stride)
            {
                int index = Index(x, z, vertexCountPerSide);
                float height = heights[index];
                if (height < profile.SeaLevel + 24.0f)
                {
                    continue;
                }

                TerrainWorldField field = fields[index];
                float slope = 1.0f - Mathf.Clamp(normals[index].Y, 0.0f, 1.0f);
                float elevation = Mathf.SmoothStep(profile.SeaLevel + 120.0f, profile.SeaLevel + profile.HeightScale * 0.70f, height);

                if (ScoreWaterfallLandmark(field, slope, elevation) >= 0.54f ||
                    ScoreDuneCrestLandmark(field, slope, elevation) >= 0.56f ||
                    ScoreDesertMonolithLandmark(field, slope, elevation) >= 0.56f ||
                    ScoreCanyonNeedleLandmark(field, slope, elevation) >= 0.58f ||
                    ScoreIceSpireLandmark(field, slope, elevation) >= 0.56f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInsidePointFootprint(
        Vector2 world,
        TerrainWorldPointOfInterest[] plannedPoints,
        TerrainGenerationProfile profile,
        float minimumInfluence)
    {
        if (plannedPoints.Length == 0)
        {
            return false;
        }

        foreach (TerrainWorldPointOfInterest point in plannedPoints)
        {
            float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
            float distance = world.DistanceTo(point.WorldPosition);
            if (distance > radius)
            {
                continue;
            }

            float coreRadius = radius * 0.46f;
            float influence = 1.0f - Mathf.SmoothStep(coreRadius, radius, distance);
            if (influence >= minimumInfluence)
            {
                return true;
            }
        }

        return false;
    }

    private static TerrainLandmarkKind LandmarkKindFor(TerrainWorldPointOfInterest point)
    {
        if (point.SettlementTier == TerrainSettlementTier.Town)
        {
            return TerrainLandmarkKind.Town;
        }

        if (point.SettlementTier == TerrainSettlementTier.Village)
        {
            return TerrainLandmarkKind.Village;
        }

        if (point.SettlementTier == TerrainSettlementTier.OasisHub)
        {
            return TerrainLandmarkKind.OasisHub;
        }

        return point.Kind switch
        {
            TerrainPointOfInterestKind.SettlementCandidate => TerrainLandmarkKind.Settlement,
            TerrainPointOfInterestKind.Vista => TerrainLandmarkKind.Vista,
            TerrainPointOfInterestKind.RiverCrossing => TerrainLandmarkKind.RiverCrossing,
            TerrainPointOfInterestKind.MountainPass => TerrainLandmarkKind.MountainPass,
            TerrainPointOfInterestKind.CoastalLanding => TerrainLandmarkKind.CoastalLanding,
            TerrainPointOfInterestKind.ResourceGrove => TerrainLandmarkKind.ResourceGrove,
            TerrainPointOfInterestKind.CanyonOverlook => TerrainLandmarkKind.CanyonOverlook,
            TerrainPointOfInterestKind.Oasis => TerrainLandmarkKind.Oasis,
            _ => TerrainLandmarkKind.AncientStone
        };
    }

    private static float LandmarkScaleFor(TerrainLandmarkKind kind, float score)
    {
        float quality = Mathf.Lerp(0.88f, 1.24f, Mathf.Clamp(score, 0.0f, 1.0f));
        float baseScale = kind switch
        {
            TerrainLandmarkKind.Settlement => 7.8f,
            TerrainLandmarkKind.Vista => 6.6f,
            TerrainLandmarkKind.RiverCrossing => 6.2f,
            TerrainLandmarkKind.MountainPass => 7.0f,
            TerrainLandmarkKind.CoastalLanding => 7.4f,
            TerrainLandmarkKind.ResourceGrove => 6.8f,
            TerrainLandmarkKind.CanyonOverlook => 7.2f,
            TerrainLandmarkKind.Oasis => 7.6f,
            TerrainLandmarkKind.Village => 8.4f,
            TerrainLandmarkKind.Town => 10.8f,
            TerrainLandmarkKind.OasisHub => 9.4f,
            TerrainLandmarkKind.VillageHouse => 2.6f,
            TerrainLandmarkKind.TownBlock => 3.4f,
            TerrainLandmarkKind.OasisCanopy => 3.0f,
            TerrainLandmarkKind.SettlementPlaza => 3.2f,
            TerrainLandmarkKind.OasisPool => 3.4f,
            TerrainLandmarkKind.Waterfall => 7.2f,
            TerrainLandmarkKind.RoadMarker => 2.0f,
            TerrainLandmarkKind.BridgeSpan => 4.4f,
            TerrainLandmarkKind.DuneCrest => 5.4f,
            TerrainLandmarkKind.DesertMonolith => 5.8f,
            TerrainLandmarkKind.CanyonNeedle => 6.2f,
            TerrainLandmarkKind.IceSpire => 5.6f,
            _ => 7.0f
        };

        return baseScale * quality;
    }

    private static Color LandmarkColorFor(TerrainLandmarkKind kind, TerrainWorldField field)
    {
        Color baseColor = kind switch
        {
            TerrainLandmarkKind.Settlement => new Color(0.70f, 0.52f, 0.32f),
            TerrainLandmarkKind.Vista => new Color(0.86f, 0.74f, 0.30f),
            TerrainLandmarkKind.RiverCrossing => new Color(0.42f, 0.48f, 0.45f),
            TerrainLandmarkKind.MountainPass => new Color(0.56f, 0.54f, 0.62f),
            TerrainLandmarkKind.CoastalLanding => new Color(0.46f, 0.58f, 0.64f),
            TerrainLandmarkKind.ResourceGrove => new Color(0.28f, 0.54f, 0.28f),
            TerrainLandmarkKind.CanyonOverlook => new Color(0.66f, 0.38f, 0.24f),
            TerrainLandmarkKind.Oasis => new Color(0.18f, 0.58f, 0.42f),
            TerrainLandmarkKind.Village => new Color(0.74f, 0.56f, 0.30f),
            TerrainLandmarkKind.Town => new Color(0.78f, 0.44f, 0.24f),
            TerrainLandmarkKind.OasisHub => new Color(0.16f, 0.66f, 0.50f),
            TerrainLandmarkKind.VillageHouse => new Color(0.68f, 0.54f, 0.34f),
            TerrainLandmarkKind.TownBlock => new Color(0.72f, 0.46f, 0.31f),
            TerrainLandmarkKind.OasisCanopy => new Color(0.14f, 0.58f, 0.42f),
            TerrainLandmarkKind.SettlementPlaza => new Color(0.62f, 0.54f, 0.40f),
            TerrainLandmarkKind.OasisPool => new Color(0.08f, 0.34f, 0.46f),
            TerrainLandmarkKind.Waterfall => new Color(0.30f, 0.62f, 0.82f),
            TerrainLandmarkKind.RoadMarker => new Color(0.56f, 0.44f, 0.28f),
            TerrainLandmarkKind.BridgeSpan => new Color(0.44f, 0.34f, 0.25f),
            TerrainLandmarkKind.DuneCrest => new Color(0.76f, 0.58f, 0.30f),
            TerrainLandmarkKind.DesertMonolith => new Color(0.62f, 0.42f, 0.24f),
            TerrainLandmarkKind.CanyonNeedle => new Color(0.58f, 0.36f, 0.24f),
            TerrainLandmarkKind.IceSpire => new Color(0.62f, 0.76f, 0.86f),
            _ => new Color(0.52f, 0.50f, 0.44f)
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp(field.ScenicPotential * 0.12f, 0.0f, 0.12f));
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

    private static TerrainPointFootprintSample SamplePointFootprint(
        Vector2 world,
        TerrainWorldPointOfInterest[] points,
        TerrainGenerationProfile profile)
    {
        TerrainPointFootprintSample best = TerrainPointFootprintSample.None;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
            float distance = world.DistanceTo(point.WorldPosition);
            if (distance > radius)
            {
                continue;
            }

            float coreRadius = radius * 0.46f;
            float coreStrength = 1.0f - Mathf.SmoothStep(0.0f, coreRadius, distance);
            float influence = 1.0f - Mathf.SmoothStep(coreRadius, radius, distance);
            if (coreStrength > 0.0f)
            {
                influence = Mathf.Max(influence, coreStrength);
            }

            if (influence <= best.Influence)
            {
                continue;
            }

            float targetHeight = TargetHeightForFootprint(point, profile);
            best = new TerrainPointFootprintSample(point.Kind, point.SettlementTier, influence, coreStrength, targetHeight);
        }

        return best;
    }

    private static float TargetHeightForFootprint(TerrainWorldPointOfInterest point, TerrainGenerationProfile profile)
    {
        float landHeight = Mathf.Max(point.Height, profile.SeaLevel + 8.0f);
        return point.SettlementTier switch
        {
            TerrainSettlementTier.Town => landHeight + 1.2f,
            TerrainSettlementTier.Village => landHeight + 0.6f,
            TerrainSettlementTier.OasisHub => Mathf.Max(point.Height - 1.5f, profile.SeaLevel + 4.0f),
            _ => point.Kind == TerrainPointOfInterestKind.Oasis
                ? Mathf.Max(point.Height - 2.0f, profile.SeaLevel + 3.0f)
                : landHeight
        };
    }

    private static float ApplyPointFootprintHeight(float height, TerrainPointFootprintSample footprint)
    {
        float strength = footprint.SettlementTier switch
        {
            TerrainSettlementTier.Town => footprint.CoreStrength * 0.86f + footprint.Influence * 0.28f,
            TerrainSettlementTier.Village => footprint.CoreStrength * 0.76f + footprint.Influence * 0.24f,
            TerrainSettlementTier.OasisHub => footprint.CoreStrength * 0.62f + footprint.Influence * 0.30f,
            _ => footprint.Kind == TerrainPointOfInterestKind.Oasis
                ? footprint.CoreStrength * 0.54f + footprint.Influence * 0.28f
                : footprint.CoreStrength * 0.48f + footprint.Influence * 0.18f
        };

        return Mathf.Lerp(height, footprint.TargetHeight, Mathf.Clamp(strength, 0.0f, 0.88f));
    }

    private static float ApplySettlementLayoutHeight(float height, TerrainSettlementLayoutSample layout)
    {
        float strength = layout.Tier switch
        {
            TerrainSettlementTier.Town => layout.StreetStrength * 0.60f + layout.PlazaStrength * 0.78f,
            TerrainSettlementTier.OasisHub => layout.StreetStrength * 0.52f + layout.PlazaStrength * 0.44f + layout.OasisGreenStrength * 0.18f + layout.OasisWaterStrength * 0.72f,
            _ => layout.StreetStrength * 0.48f + layout.PlazaStrength * 0.58f
        };
        float targetHeight = layout.TargetHeight + layout.PlazaStrength * 0.18f - layout.OasisGreenStrength * 0.08f - layout.OasisWaterStrength * 0.62f;
        return Mathf.Lerp(height, targetHeight, Mathf.Clamp(strength, 0.0f, 0.84f));
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

    private static Color BlendPointFootprintColor(Color baseColor, TerrainPointFootprintSample footprint)
    {
        Color footprintColor = footprint.SettlementTier switch
        {
            TerrainSettlementTier.Town => new Color(0.54f, 0.40f, 0.27f),
            TerrainSettlementTier.Village => new Color(0.48f, 0.38f, 0.24f),
            TerrainSettlementTier.OasisHub => new Color(0.18f, 0.54f, 0.42f),
            _ => footprint.Kind == TerrainPointOfInterestKind.Oasis
                ? new Color(0.20f, 0.50f, 0.40f)
                : new Color(0.44f, 0.36f, 0.25f)
        };

        float blend = Mathf.Clamp(footprint.CoreStrength * 0.44f + footprint.Influence * 0.26f, 0.0f, 0.66f);
        return baseColor.Lerp(footprintColor, blend);
    }

    private static Color BlendSettlementLayoutColor(Color baseColor, TerrainSettlementLayoutSample layout)
    {
        Color streetColor = layout.Tier switch
        {
            TerrainSettlementTier.Town => new Color(0.46f, 0.38f, 0.30f),
            TerrainSettlementTier.OasisHub => new Color(0.38f, 0.48f, 0.35f),
            _ => new Color(0.42f, 0.34f, 0.22f)
        };
        Color plazaColor = layout.Tier == TerrainSettlementTier.OasisHub
            ? new Color(0.28f, 0.56f, 0.44f)
            : new Color(0.58f, 0.50f, 0.38f);
        Color result = baseColor.Lerp(streetColor, Mathf.Clamp(layout.StreetStrength * 0.64f, 0.0f, 0.68f));
        result = result.Lerp(plazaColor, Mathf.Clamp(layout.PlazaStrength * 0.62f, 0.0f, 0.66f));

        if (layout.OasisGreenStrength > 0.0f)
        {
            result = result.Lerp(new Color(0.12f, 0.54f, 0.34f), Mathf.Clamp(layout.OasisGreenStrength * 0.48f, 0.0f, 0.52f));
        }

        if (layout.OasisWaterStrength > 0.0f)
        {
            result = result.Lerp(new Color(0.05f, 0.30f, 0.42f), Mathf.Clamp(layout.OasisWaterStrength * 0.72f, 0.0f, 0.76f));
        }

        return result;
    }

    private static int Index(int x, int z, int vertexCountPerSide)
    {
        return z * vertexCountPerSide + x;
    }

    private readonly record struct TerrainPointFootprintSample(
        TerrainPointOfInterestKind Kind,
        TerrainSettlementTier SettlementTier,
        float Influence,
        float CoreStrength,
        float TargetHeight)
    {
        public static TerrainPointFootprintSample None { get; } = new(
            TerrainPointOfInterestKind.Vista,
            TerrainSettlementTier.None,
            0.0f,
            0.0f,
            0.0f);

        public bool HasInfluence => Influence > 0.0f;
    }

    private readonly record struct TerrainSettlementLayoutDescriptor(
        TerrainSettlementTier Tier,
        Vector2 Center,
        float Radius,
        Vector2 Axis,
        Vector2 Side,
        float TargetHeight);

    private readonly record struct TerrainSettlementLayoutSample(
        bool HasInfluence,
        TerrainSettlementTier Tier,
        float Influence,
        float CoreStrength,
        float StreetStrength,
        float PlazaStrength,
        float OasisGreenStrength,
        float OasisWaterStrength,
        float TargetHeight)
    {
        public static TerrainSettlementLayoutSample None { get; } = new(
            false,
            TerrainSettlementTier.None,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f);
    }
}
