using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Rendering;

/// <summary>Creates Godot ArrayMesh instances from generated terrain tile data.</summary>
public static class TerrainMeshBuilder
{
    /// <summary>Creates an ArrayMesh from the vertices, normals, UVs, colors, and indices in the tile data.</summary>
    public static ArrayMesh CreateMesh(TerrainTileData data)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Vertices;
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals;
        arrays[(int)Mesh.ArrayType.TexUV] = data.Uvs;
        arrays[(int)Mesh.ArrayType.Color] = data.Colors;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.CustomAabb = new Aabb(
            new Vector3(0.0f, data.MinHeight - 2.0f, 0.0f),
            new Vector3(data.ChunkSize, data.MaxHeight - data.MinHeight + 4.0f, data.ChunkSize));

        return mesh;
    }

    /// <summary>Creates an ArrayMesh for generated local river, lake, and oasis water surfaces.</summary>
    public static ArrayMesh CreateWaterMesh(TerrainTileData data)
    {
        TerrainWaterSurfaceData water = data.WaterSurface;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = water.Vertices;
        arrays[(int)Mesh.ArrayType.Normal] = water.Normals;
        arrays[(int)Mesh.ArrayType.TexUV] = water.Uvs;
        arrays[(int)Mesh.ArrayType.Color] = water.Colors;
        arrays[(int)Mesh.ArrayType.Index] = water.Indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.CustomAabb = new Aabb(
            new Vector3(0.0f, water.MinHeight - 0.5f, 0.0f),
            new Vector3(data.ChunkSize, water.MaxHeight - water.MinHeight + 1.0f, data.ChunkSize));

        return mesh;
    }
}
