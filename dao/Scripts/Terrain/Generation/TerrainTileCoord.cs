using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Identifies a terrain tile by its integer grid coordinates (X, Z).</summary>
public readonly record struct TerrainTileCoord(int X, int Z)
{
    /// <summary>Creates a <see cref="TerrainTileCoord"/> from a 3D world position and chunk size.</summary>
    public static TerrainTileCoord FromWorldPosition(Vector3 worldPosition, float chunkSize)
    {
        return new TerrainTileCoord(
            Mathf.FloorToInt(worldPosition.X / chunkSize),
            Mathf.FloorToInt(worldPosition.Z / chunkSize));
    }

    /// <summary>Returns the world-space origin (bottom-left corner) of this tile.</summary>
    public Vector2 Origin(float chunkSize)
    {
        return new Vector2(X * chunkSize, Z * chunkSize);
    }

    /// <summary>Computes the Chebyshev (chessboard) distance to another tile coordinate.</summary>
    public int ChebyshevDistanceTo(TerrainTileCoord other)
    {
        return Mathf.Max(Mathf.Abs(X - other.X), Mathf.Abs(Z - other.Z));
    }

    /// <summary>Returns the squared distance from the tile center to a world position.</summary>
    public float CenterDistanceSquaredTo(Vector3 worldPosition, float chunkSize)
    {
        float centerX = (X + 0.5f) * chunkSize;
        float centerZ = (Z + 0.5f) * chunkSize;
        float dx = centerX - worldPosition.X;
        float dz = centerZ - worldPosition.Z;
        return (dx * dx) + (dz * dz);
    }

    public override string ToString()
    {
        return $"{X},{Z}";
    }
}
