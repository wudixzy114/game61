using System;
using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static class TerrainTileBuilder
{
    public static TerrainTileData Build(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        bool includeCollision)
    {
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

        float minHeight = float.PositiveInfinity;
        float maxHeight = float.NegativeInfinity;

        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int index = Index(x, z, vertexCountPerSide);
                float localX = x * step;
                float localZ = z * step;
                float height = useNativeHeights
                    ? nativeHeights[index]
                    : TerrainSampler.Sample(new Vector2(origin.X + localX, origin.Y + localZ), profile).Height;

                surfaceVertices[index] = new Vector3(localX, height, localZ);
                surfaceUvs[index] = new Vector2(
                    (origin.X + localX) / profile.ChunkSize,
                    (origin.Y + localZ) / profile.ChunkSize);
                heights[index] = height;

                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }
        }

        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int index = Index(x, z, vertexCountPerSide);
                Vector2 world = new(origin.X + x * step, origin.Y + z * step);
                Vector3 normal = CalculateGridNormal(x, z, resolution, vertexCountPerSide, heights, step);
                float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
                surfaceNormals[index] = normal;
                surfaceColors[index] = TerrainSampler.ColorForSurface(world, profile, heights[index], slope);

                if (heights[index] < profile.SeaLevel + 3.0f)
                {
                    surfaceColors[index] = surfaceColors[index].Lerp(new Color(0.10f, 0.24f, 0.31f), 0.35f);
                }
            }
        }

        int[] surfaceIndices = BuildIndices(resolution, vertexCountPerSide);
        Vector3[] collisionFaces = includeCollision ? BuildCollisionFaces(surfaceVertices, surfaceIndices) : [];
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
            origin,
            heights,
            surfaceNormals,
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
        Vector2 origin,
        float[] heights,
        Vector3[] normals,
        out TerrainScatterInstance[] scatterInstances,
        out TerrainLandmarkData[] landmarks)
    {
        var scatter = new List<TerrainScatterInstance>(96);
        var landmarkList = new List<TerrainLandmarkData>(4);

        if (lod <= 2)
        {
            int cells = lod == 0 ? 14 : lod == 1 ? 9 : 5;
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    float jx = Hash01(coord.X, coord.Z, x * 193 + z * 389, profile.Seed);
                    float jz = Hash01(coord.X, coord.Z, x * 557 + z * 263, profile.Seed + 17);
                    float localX = (x + 0.18f + jx * 0.64f) / cells * profile.ChunkSize;
                    float localZ = (z + 0.18f + jz * 0.64f) / cells * profile.ChunkSize;
                    Vector2 world = new(origin.X + localX, origin.Y + localZ);
                    float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);

                    if (height < profile.SeaLevel + 6.0f)
                    {
                        continue;
                    }

                    Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
                    float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
                    float moisture = EstimateMoisture(world, profile);
                    float temperature = EstimateTemperature(world, profile, height);
                    float river = EstimateRiver(world, profile);
                    float roll = Hash01(coord.X, coord.Z, x * 881 + z * 977, profile.Seed + 31);

                    if (slope < 0.30f && moisture > 0.47f && temperature > 0.24f && river < 0.78f && roll < 0.44f)
                    {
                        float scale = 2.2f + Hash01(coord.X, coord.Z, x * 1237 + z * 2011, profile.Seed + 43) * 3.4f;
                        float rotation = Hash01(coord.X, coord.Z, x * 719 + z * 911, profile.Seed + 59) * Mathf.Pi * 2.0f;
                        Color tint = new Color(0.22f, 0.44f, 0.19f).Lerp(new Color(0.08f, 0.25f, 0.12f), moisture);
                        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Tree, new Vector3(localX, height, localZ), rotation, scale, tint));
                    }
                    else if ((slope > 0.35f || height > profile.SeaLevel + 360.0f) && roll < 0.38f)
                    {
                        float scale = 1.3f + Hash01(coord.X, coord.Z, x * 4567 + z * 3461, profile.Seed + 61) * 3.1f;
                        float rotation = Hash01(coord.X, coord.Z, x * 2467 + z * 6421, profile.Seed + 67) * Mathf.Pi * 2.0f;
                        Color tint = new Color(0.36f, 0.35f, 0.32f).Lerp(new Color(0.55f, 0.54f, 0.49f), Mathf.Clamp(slope, 0.0f, 1.0f));
                        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Rock, new Vector3(localX, height, localZ), rotation, scale, tint));
                    }
                }
            }
        }

        if (lod <= 1)
        {
            AddBestLandmark(coord, profile, resolution, vertexCountPerSide, step, origin, heights, normals, scatter, landmarkList);
        }

        scatterInstances = scatter.ToArray();
        landmarks = landmarkList.ToArray();
    }

    private static void AddBestLandmark(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        Vector2 origin,
        float[] heights,
        Vector3[] normals,
        List<TerrainScatterInstance> scatter,
        List<TerrainLandmarkData> landmarks)
    {
        TerrainLandmarkData best = default;
        float bestScore = 0.0f;

        for (int i = 0; i < 8; i++)
        {
            float localX = (0.15f + Hash01(coord.X, coord.Z, i * 173, profile.Seed + 101) * 0.70f) * profile.ChunkSize;
            float localZ = (0.15f + Hash01(coord.X, coord.Z, i * 277, profile.Seed + 103) * 0.70f) * profile.ChunkSize;
            Vector2 world = new(origin.X + localX, origin.Y + localZ);
            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
            float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
            float river = EstimateRiver(world, profile);
            float flatness = 1.0f - Mathf.Clamp(slope * 2.2f, 0.0f, 1.0f);
            float heightScore = Mathf.Clamp((height - profile.SeaLevel - 140.0f) / 560.0f, 0.0f, 1.0f);
            float rarity = Hash01(coord.X, coord.Z, i * 421, profile.Seed + 107);

            TerrainLandmarkKind kind = TerrainLandmarkKind.Vista;
            float score = heightScore * 0.62f + flatness * 0.28f + rarity * 0.10f;

            if (river > 0.70f && height > profile.SeaLevel + 10.0f && slope < 0.24f)
            {
                kind = TerrainLandmarkKind.RiverCrossing;
                score = 0.72f + river * 0.18f + flatness * 0.10f;
            }
            else if (height > profile.SeaLevel + 320.0f && slope is > 0.16f and < 0.42f)
            {
                kind = TerrainLandmarkKind.MountainPass;
                score = 0.55f + heightScore * 0.25f + (1.0f - Mathf.Abs(slope - 0.28f) * 2.0f) * 0.20f;
            }
            else if (rarity > 0.92f && slope < 0.26f)
            {
                kind = TerrainLandmarkKind.AncientStone;
                score = 0.78f + flatness * 0.12f + heightScore * 0.10f;
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
        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Landmark, best.LocalPosition, rotation, scale, tint));
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

    private static Vector3 SampleNearestNormal(float localX, float localZ, int resolution, float step, Vector3[] normals, int vertexCountPerSide)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(localX / step), 0, resolution);
        int z = Mathf.Clamp(Mathf.RoundToInt(localZ / step), 0, resolution);
        return normals[Index(x, z, vertexCountPerSide)];
    }

    private static float EstimateMoisture(Vector2 world, TerrainGenerationProfile profile)
    {
        float river = EstimateRiver(world, profile);
        return Mathf.Clamp(
            (ProceduralNoise.Fbm(world.X / 950.0f, world.Y / 950.0f, profile.Seed + 83, 4) * 0.5f) + 0.5f + river * 0.45f,
            0.0f,
            1.0f);
    }

    private static float EstimateTemperature(Vector2 world, TerrainGenerationProfile profile, float height)
    {
        float latitude = Mathf.Abs(Mathf.Sin(world.Y / 9000.0f));
        return Mathf.Clamp(1.0f - latitude - Mathf.Max(0.0f, height) / (profile.HeightScale * 1.7f), 0.0f, 1.0f);
    }

    private static float EstimateRiver(Vector2 world, TerrainGenerationProfile profile)
    {
        float canyonNoise = ProceduralNoise.Ridged(
            (world.X + 811.0f) / (profile.MountainScale * 0.82f),
            (world.Y - 347.0f) / (profile.MountainScale * 0.82f),
            profile.Seed + 53,
            4);
        return (1.0f - Mathf.SmoothStep(0.02f, 0.135f, Mathf.Abs(canyonNoise - 0.52f))) * profile.RiverStrength;
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

    private static int Index(int x, int z, int vertexCountPerSide)
    {
        return z * vertexCountPerSide + x;
    }
}
