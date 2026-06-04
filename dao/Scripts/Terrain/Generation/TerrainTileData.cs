using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Generated local water surface mesh for a terrain tile, used for rivers, lakes, and oasis pools above the terrain surface.</summary>
public readonly record struct TerrainWaterSurfaceData(
    Vector3[] Vertices,
    Vector3[] Normals,
    Vector2[] Uvs,
    Color[] Colors,
    int[] Indices,
    int LakeCellCount,
    int RiverCellCount,
    int OasisCellCount,
    float MinHeight,
    float MaxHeight)
{
    public static TerrainWaterSurfaceData Empty { get; } = new(
        [],
        [],
        [],
        [],
        [],
        LakeCellCount: 0,
        RiverCellCount: 0,
        OasisCellCount: 0,
        MinHeight: 0.0f,
        MaxHeight: 0.0f);

    public bool HasSurface => Vertices.Length > 0 && Indices.Length > 0;
    public int CellCount => LakeCellCount + RiverCellCount + OasisCellCount;
}

/// <summary>Complete generated data for a single terrain tile, including mesh, collision, scatter instances, and landmarks.</summary>
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
        TerrainWaterSurfaceData waterSurface,
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
        WaterSurface = waterSurface;
        CollisionFaces = collisionFaces;
        ScatterInstances = scatterInstances;
        Landmarks = landmarks;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
    }

    /// <summary>Tile grid coordinate.</summary>
    public TerrainTileCoord Coord { get; }
    /// <summary>Level of detail (0 = highest).</summary>
    public int Lod { get; }
    /// <summary>Vertex resolution per side.</summary>
    public int Resolution { get; }
    /// <summary>World-space extent of the tile in meters.</summary>
    public float ChunkSize { get; }
    /// <summary>Bottom-left corner of the tile in world space.</summary>
    public Vector2 Origin { get; }
    /// <summary>Render mesh vertex positions (includes skirt if enabled).</summary>
    public Vector3[] Vertices { get; }
    /// <summary>Vertex normals.</summary>
    public Vector3[] Normals { get; }
    /// <summary>Vertex UV coordinates.</summary>
    public Vector2[] Uvs { get; }
    /// <summary>Vertex colors.</summary>
    public Color[] Colors { get; }
    /// <summary>Triangle index buffer.</summary>
    public int[] Indices { get; }
    /// <summary>Local rivers, lakes, and oasis pool water surface mesh data for this tile.</summary>
    public TerrainWaterSurfaceData WaterSurface { get; }
    /// <summary>Collision mesh vertex data (empty if collision not generated).</summary>
    public Vector3[] CollisionFaces { get; }
    /// <summary>Surface scatter instances (trees, rocks, gameplay elements, landmarks).</summary>
    public TerrainScatterInstance[] ScatterInstances { get; }
    /// <summary>Named landmark metadata for this tile.</summary>
    public TerrainLandmarkData[] Landmarks { get; }
    /// <summary>Minimum vertex height (used for AABB computation).</summary>
    public float MinHeight { get; }
    /// <summary>Maximum vertex height.</summary>
    public float MaxHeight { get; }
}
