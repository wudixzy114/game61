using Godot;

namespace Dao.Terrain.Generation;

public sealed class TerrainTileData
{
    public TerrainTileData(
        TerrainTileCoord coord,
        int lod,
        int resolution,
        float chunkSize,
        Vector2 origin,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        Color[] colors,
        int[] indices,
        Vector3[] collisionFaces,
        TerrainScatterInstance[] scatterInstances,
        TerrainLandmarkData[] landmarks,
        float minHeight,
        float maxHeight)
    {
        Coord = coord;
        Lod = lod;
        Resolution = resolution;
        ChunkSize = chunkSize;
        Origin = origin;
        Vertices = vertices;
        Normals = normals;
        Uvs = uvs;
        Colors = colors;
        Indices = indices;
        CollisionFaces = collisionFaces;
        ScatterInstances = scatterInstances;
        Landmarks = landmarks;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
    }

    public TerrainTileCoord Coord { get; }
    public int Lod { get; }
    public int Resolution { get; }
    public float ChunkSize { get; }
    public Vector2 Origin { get; }
    public Vector3[] Vertices { get; }
    public Vector3[] Normals { get; }
    public Vector2[] Uvs { get; }
    public Color[] Colors { get; }
    public int[] Indices { get; }
    public Vector3[] CollisionFaces { get; }
    public TerrainScatterInstance[] ScatterInstances { get; }
    public TerrainLandmarkData[] Landmarks { get; }
    public float MinHeight { get; }
    public float MaxHeight { get; }
}
