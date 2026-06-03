using System;
using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public sealed class TerrainPointOfInterestIndex
{
    private static readonly TerrainWorldPointOfInterest[] NoPoints = [];

    private readonly Dictionary<TerrainTileCoord, TerrainWorldPointOfInterest[]> _pointsByCoord;

    private TerrainPointOfInterestIndex(
        int cacheKey,
        Dictionary<TerrainTileCoord, TerrainWorldPointOfInterest[]> pointsByCoord)
    {
        CacheKey = cacheKey;
        _pointsByCoord = pointsByCoord;
    }

    public static TerrainPointOfInterestIndex Empty { get; } = new(0, []);

    public int CacheKey { get; }
    public bool HasPoints => _pointsByCoord.Count > 0;

    public static TerrainPointOfInterestIndex FromPlan(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        if (plan.PointsOfInterest.Length == 0)
        {
            return Empty;
        }

        var buckets = new Dictionary<TerrainTileCoord, List<TerrainWorldPointOfInterest>>();
        int hash = StartHash(profile, plan);

        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            float footprintRadius = FootprintRadiusFor(point, profile);
            int minX = Mathf.FloorToInt((point.WorldPosition.X - footprintRadius) / profile.ChunkSize);
            int maxX = Mathf.FloorToInt((point.WorldPosition.X + footprintRadius) / profile.ChunkSize);
            int minZ = Mathf.FloorToInt((point.WorldPosition.Y - footprintRadius) / profile.ChunkSize);
            int maxZ = Mathf.FloorToInt((point.WorldPosition.Y + footprintRadius) / profile.ChunkSize);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    TerrainTileCoord coord = new(x, z);
                    if (!buckets.TryGetValue(coord, out List<TerrainWorldPointOfInterest>? points))
                    {
                        points = new List<TerrainWorldPointOfInterest>(2);
                        buckets.Add(coord, points);
                    }

                    points.Add(point);
                }
            }

            hash = AppendPointHash(hash, point);
        }

        var immutableBuckets = new Dictionary<TerrainTileCoord, TerrainWorldPointOfInterest[]>(buckets.Count);
        foreach ((TerrainTileCoord coord, List<TerrainWorldPointOfInterest> points) in buckets)
        {
            immutableBuckets.Add(coord, points.ToArray());
        }

        return new TerrainPointOfInterestIndex(hash == 0 ? 1 : hash, immutableBuckets);
    }

    public TerrainWorldPointOfInterest[] GetPoints(TerrainTileCoord coord)
    {
        return _pointsByCoord.TryGetValue(coord, out TerrainWorldPointOfInterest[]? points)
            ? points
            : NoPoints;
    }

    public static float FootprintRadiusFor(TerrainWorldPointOfInterest point, TerrainGenerationProfile profile)
    {
        float chunkSize = profile.ChunkSize;
        return point.SettlementTier switch
        {
            TerrainSettlementTier.Town => chunkSize * 0.42f,
            TerrainSettlementTier.Village => chunkSize * 0.32f,
            TerrainSettlementTier.OasisHub => chunkSize * 0.38f,
            _ => point.Kind switch
            {
                TerrainPointOfInterestKind.Oasis => chunkSize * 0.30f,
                TerrainPointOfInterestKind.SettlementCandidate => chunkSize * 0.26f,
                TerrainPointOfInterestKind.CoastalLanding => chunkSize * 0.20f,
                TerrainPointOfInterestKind.RiverCrossing => chunkSize * 0.18f,
                _ => chunkSize * 0.16f
            }
        };
    }

    private static int StartHash(TerrainGenerationProfile profile, TerrainWorldPlan plan)
    {
        unchecked
        {
            int hash = 23;
            hash = (hash * 397) ^ profile.Seed;
            hash = (hash * 397) ^ FloatHash(profile.ChunkSize);
            hash = (hash * 397) ^ FloatHash(plan.Center.X);
            hash = (hash * 397) ^ FloatHash(plan.Center.Y);
            hash = (hash * 397) ^ FloatHash(plan.WorldSize);
            hash = (hash * 397) ^ plan.PointsOfInterest.Length;
            return hash;
        }
    }

    private static int AppendPointHash(int hash, TerrainWorldPointOfInterest point)
    {
        unchecked
        {
            hash = (hash * 397) ^ point.Id;
            hash = (hash * 397) ^ (int)point.Kind;
            hash = (hash * 397) ^ FloatHash(point.WorldPosition.X);
            hash = (hash * 397) ^ FloatHash(point.WorldPosition.Y);
            hash = (hash * 397) ^ FloatHash(point.Score);
            hash = (hash * 397) ^ (int)point.SettlementTier;
            return hash;
        }
    }

    private static int FloatHash(float value)
    {
        return unchecked((int)BitConverter.SingleToUInt32Bits(value));
    }
}
