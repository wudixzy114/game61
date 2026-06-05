using System;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
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
}
