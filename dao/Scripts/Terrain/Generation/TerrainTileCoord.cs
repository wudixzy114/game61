using Godot;

namespace Dao.Terrain.Generation;

public readonly record struct TerrainTileCoord(int X, int Z)
{
    public static TerrainTileCoord FromWorldPosition(Vector3 worldPosition, float chunkSize)
    {
        return new TerrainTileCoord(
            Mathf.FloorToInt(worldPosition.X / chunkSize),
            Mathf.FloorToInt(worldPosition.Z / chunkSize));
    }

    public Vector2 Origin(float chunkSize)
    {
        return new Vector2(X * chunkSize, Z * chunkSize);
    }

    public int ChebyshevDistanceTo(TerrainTileCoord other)
    {
        return Mathf.Max(Mathf.Abs(X - other.X), Mathf.Abs(Z - other.Z));
    }

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
