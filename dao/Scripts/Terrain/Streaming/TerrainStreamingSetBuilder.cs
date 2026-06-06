using System.Collections.Generic;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

internal static class TerrainStreamingSetBuilder
{
    internal static bool RebuildDesiredSet(
        HashSet<TerrainTileCoord> desiredCoords,
        TerrainTileCoord center,
        int radius)
    {
        TerrainTileCoord[] before = desiredCoords.Count == 0
            ? System.Array.Empty<TerrainTileCoord>()
            : [.. desiredCoords];
        desiredCoords.Clear();
        int radiusSquared = radius * radius;

        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if ((x * x) + (z * z) > radiusSquared)
                {
                    continue;
                }

                desiredCoords.Add(new TerrainTileCoord(center.X + x, center.Z + z));
            }
        }

        if (before.Length != desiredCoords.Count)
        {
            return true;
        }

        foreach (TerrainTileCoord coord in before)
        {
            if (!desiredCoords.Contains(coord))
            {
                return true;
            }
        }

        return false;
    }

    internal static TerrainTileRequest GetDesiredRequest(
        TerrainTileCoord coord,
        TerrainTileCoord center,
        TerrainGenerationProfile profile)
    {
        int distance = coord.ChebyshevDistanceTo(center);
        bool includeCollision = profile.GenerateCollision && distance <= profile.CollisionRadiusChunks;
        int lod = includeCollision ? 0 : Mathf.Clamp((distance - 1) / 2, 0, profile.MaxLod);
        return new TerrainTileRequest(lod, includeCollision);
    }
}
