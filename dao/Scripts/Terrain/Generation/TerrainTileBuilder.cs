using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static class TerrainTileBuilder
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
        float[] nativeHeights = [];
        bool useNativeHeights = profile.UseNativeSamplerWhenAvailable &&
            NativeTerrainBridge.TrySampleHeightGrid(coord, resolution, profile, out nativeHeights);

        var surfaceVertices = new Vector3[vertexCount];
        var surfaceNormals = new Vector3[vertexCount];
        var surfaceUvs = new Vector2[vertexCount];
        var surfaceColors = new Color[vertexCount];
        var heights = new float[vertexCount];
        var fields = new TerrainWorldField[vertexCount];
        TerrainRouteCorridorSegment[] corridorSegments = routeCorridors.GetSegments(coord);
        bool hasCorridors = corridorSegments.Length > 0;
        TerrainRouteCorridorSample[] corridorSamples = hasCorridors ? new TerrainRouteCorridorSample[vertexCount] : [];

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
                TerrainWorldField field = useNativeHeights
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

                    if (height < profile.SeaLevel + 6.0f)
                    {
                        continue;
                    }

                    Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
                    float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
                    TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
                    float roll = Hash01(coord.X, coord.Z, x * 881 + z * 977, profile.Seed + 31);
                    Vector2 world = new(origin.X + localX, origin.Y + localZ);
                    TerrainRouteCorridorSample corridor = hasCorridors
                        ? routeCorridors.Sample(world, corridorSegments)
                        : TerrainRouteCorridorSample.None;

                    if (IsNearPlannedPoint(world, plannedPoints, profile.ChunkSize * 0.28f))
                    {
                        continue;
                    }

                    if (corridor.HasInfluence && (corridor.CoreStrength > 0.04f || corridor.Influence > 0.58f))
                    {
                        continue;
                    }

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
                    }

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
            AddPlannedPoiLandmarks(coord, profile, resolution, vertexCountPerSide, step, heights, fields, normals, plannedPoints, scatter, landmarkList);
            if (landmarkList.Count == 0)
            {
                AddBestLandmark(coord, profile, resolution, vertexCountPerSide, step, heights, fields, normals, cancellationToken, scatter, landmarkList);
            }
        }

        scatterInstances = scatter.ToArray();
        landmarks = landmarkList.ToArray();
    }

    private static void AddGameplayScatter(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int cellX,
        int cellZ,
        float localX,
        float localZ,
        float height,
        float slope,
        TerrainWorldField field,
        List<TerrainScatterInstance> scatter)
    {
        float understoryRoll = Hash01(coord.X, coord.Z, cellX * 2711 + cellZ * 2797, profile.Seed + 149);
        if (slope < 0.22f &&
            field.ResourcePotential > 0.42f &&
            field.Moisture > 0.50f &&
            field.Temperature > 0.24f &&
            field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Wetland or TerrainLandscapeKind.RiverValley &&
            understoryRoll < Mathf.Lerp(0.08f, 0.46f, field.ResourcePotential))
        {
            float scale = 0.55f + Hash01(coord.X, coord.Z, cellX * 3253 + cellZ * 3307, profile.Seed + 151) * 0.95f;
            float rotation = Hash01(coord.X, coord.Z, cellX * 3533 + cellZ * 3581, profile.Seed + 157) * Mathf.Pi * 2.0f;
            Color tint = new Color(0.18f, 0.36f, 0.16f).Lerp(new Color(0.34f, 0.50f, 0.22f), field.ResourcePotential);
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Understory, new Vector3(localX, height, localZ), rotation, scale, tint));
        }

        float resourceRoll = Hash01(coord.X, coord.Z, cellX * 3761 + cellZ * 3851, profile.Seed + 163);
        if (field.ResourcePotential > 0.62f &&
            field.Traversability > 0.34f &&
            slope < 0.30f &&
            resourceRoll < Mathf.Lerp(0.04f, 0.24f, field.ResourcePotential))
        {
            float scale = 0.95f + Hash01(coord.X, coord.Z, cellX * 4001 + cellZ * 4027, profile.Seed + 167) * 1.45f;
            float rotation = Hash01(coord.X, coord.Z, cellX * 4211 + cellZ * 4241, profile.Seed + 173) * Mathf.Pi * 2.0f;
            Color tint = new Color(0.28f, 0.48f, 0.22f).Lerp(new Color(0.62f, 0.54f, 0.30f), Mathf.Clamp(field.ResourcePotential, 0.0f, 1.0f));
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.ResourceNode, new Vector3(localX, height, localZ), rotation, scale, tint));
        }

        float hazardRoll = Hash01(coord.X, coord.Z, cellX * 4441 + cellZ * 4481, profile.Seed + 181);
        if (field.HazardPotential > 0.48f &&
            field.EncounterPotential > 0.40f &&
            (slope > 0.24f || field.Exposure > 0.46f) &&
            hazardRoll < Mathf.Lerp(0.05f, 0.30f, field.HazardPotential))
        {
            float scale = 0.85f + Hash01(coord.X, coord.Z, cellX * 4651 + cellZ * 4721, profile.Seed + 191) * 1.80f;
            float rotation = Hash01(coord.X, coord.Z, cellX * 4861 + cellZ * 4931, profile.Seed + 193) * Mathf.Pi * 2.0f;
            Color tint = new Color(0.38f, 0.30f, 0.27f).Lerp(new Color(0.64f, 0.58f, 0.50f), Mathf.Clamp(field.Exposure, 0.0f, 1.0f));
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.HazardOutcrop, new Vector3(localX, height, localZ), rotation, scale, tint));
        }
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
            TerrainLandmarkKind kind = LandmarkKindFor(point.Kind);
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

    private static bool IsNearPlannedPoint(Vector2 world, TerrainWorldPointOfInterest[] plannedPoints, float radius)
    {
        if (plannedPoints.Length == 0)
        {
            return false;
        }

        float radiusSquared = radius * radius;
        foreach (TerrainWorldPointOfInterest point in plannedPoints)
        {
            if (world.DistanceSquaredTo(point.WorldPosition) <= radiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static TerrainLandmarkKind LandmarkKindFor(TerrainPointOfInterestKind kind)
    {
        return kind switch
        {
            TerrainPointOfInterestKind.SettlementCandidate => TerrainLandmarkKind.Settlement,
            TerrainPointOfInterestKind.Vista => TerrainLandmarkKind.Vista,
            TerrainPointOfInterestKind.RiverCrossing => TerrainLandmarkKind.RiverCrossing,
            TerrainPointOfInterestKind.MountainPass => TerrainLandmarkKind.MountainPass,
            TerrainPointOfInterestKind.CoastalLanding => TerrainLandmarkKind.CoastalLanding,
            TerrainPointOfInterestKind.ResourceGrove => TerrainLandmarkKind.ResourceGrove,
            TerrainPointOfInterestKind.CanyonOverlook => TerrainLandmarkKind.CanyonOverlook,
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
