using System;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static int[] GetSurfaceIndices(int resolution)
    {
        return TerrainTileMeshAssemblyService.GetSurfaceIndices(resolution);
    }

    private static int[] GetSkirtedIndices(int resolution)
    {
        return TerrainTileMeshAssemblyService.GetSkirtedIndices(resolution);
    }

    private static Vector3[] BuildCollisionFaces(Vector3[] vertices, int[] indices)
    {
        return TerrainTileMeshAssemblyService.BuildCollisionFaces(vertices, indices);
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
        TerrainTileMeshAssemblyService.BuildSkirtedRenderMesh(
            resolution,
            vertexCountPerSide,
            surfaceVertexCount,
            skirtDepth,
            surfaceVertices,
            surfaceNormals,
            surfaceUvs,
            surfaceColors,
            out vertices,
            out normals,
            out uvs,
            out colors,
            out indices);
    }
}
