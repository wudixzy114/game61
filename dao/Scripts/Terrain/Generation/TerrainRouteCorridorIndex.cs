using System;
using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public readonly record struct TerrainRouteCorridorSample(
    bool HasInfluence,
    TerrainRouteKind Kind,
    float Influence,
    float CoreStrength,
    float Distance,
    float TargetHeight,
    float ScenicPotential,
    float Traversability)
{
    public static TerrainRouteCorridorSample None { get; } = new(false, TerrainRouteKind.PrimaryTrail, 0.0f, 0.0f, float.PositiveInfinity, 0.0f, 0.0f, 0.0f);
}

public readonly record struct TerrainRouteCorridorSegment(
    Vector2 From,
    Vector2 To,
    float FromHeight,
    float ToHeight,
    TerrainRouteKind Kind,
    float CoreWidth,
    float ShoulderWidth,
    float ScenicPotential,
    float Traversability);

public sealed class TerrainRouteCorridorIndex
{
    private static readonly TerrainRouteCorridorSegment[] NoSegments = [];

    private readonly Dictionary<TerrainTileCoord, TerrainRouteCorridorSegment[]> _segmentsByCoord;

    private TerrainRouteCorridorIndex(
        int cacheKey,
        Dictionary<TerrainTileCoord, TerrainRouteCorridorSegment[]> segmentsByCoord)
    {
        CacheKey = cacheKey;
        _segmentsByCoord = segmentsByCoord;
    }

    public static TerrainRouteCorridorIndex Empty { get; } = new(0, []);

    public int CacheKey { get; }
    public bool HasSegments => _segmentsByCoord.Count > 0;

    public static TerrainRouteCorridorIndex FromPlan(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        if (plan.Routes.Length == 0)
        {
            return Empty;
        }

        var buckets = new Dictionary<TerrainTileCoord, List<TerrainRouteCorridorSegment>>();
        int hash = StartHash(profile, plan);

        foreach (TerrainWorldRoute route in plan.Routes)
        {
            if (route.Waypoints.Length < 2)
            {
                continue;
            }

            CorridorWidth width = WidthFor(route.Kind);
            for (int i = 1; i < route.Waypoints.Length; i++)
            {
                Vector2 from = route.Waypoints[i - 1];
                Vector2 to = route.Waypoints[i];
                if (from.DistanceSquaredTo(to) <= 1.0f)
                {
                    continue;
                }

                float fromHeight = CorridorHeight(from, profile);
                float toHeight = CorridorHeight(to, profile);
                var segment = new TerrainRouteCorridorSegment(
                    from,
                    to,
                    fromHeight,
                    toHeight,
                    route.Kind,
                    width.CoreWidth,
                    width.ShoulderWidth,
                    route.AverageScenicPotential,
                    route.AverageTraversability);

                AddSegmentToBuckets(segment, profile.ChunkSize, buckets);
                hash = AppendSegmentHash(hash, segment);
            }
        }

        if (buckets.Count == 0)
        {
            return Empty;
        }

        var immutableBuckets = new Dictionary<TerrainTileCoord, TerrainRouteCorridorSegment[]>(buckets.Count);
        foreach ((TerrainTileCoord coord, List<TerrainRouteCorridorSegment> segments) in buckets)
        {
            immutableBuckets.Add(coord, segments.ToArray());
        }

        return new TerrainRouteCorridorIndex(hash == 0 ? 1 : hash, immutableBuckets);
    }

    public TerrainRouteCorridorSegment[] GetSegments(TerrainTileCoord coord)
    {
        return _segmentsByCoord.TryGetValue(coord, out TerrainRouteCorridorSegment[]? segments)
            ? segments
            : NoSegments;
    }

    public TerrainRouteCorridorSample Sample(Vector2 world, TerrainTileCoord coord)
    {
        return Sample(world, GetSegments(coord));
    }

    public TerrainRouteCorridorSample Sample(Vector2 world, TerrainRouteCorridorSegment[] segments)
    {
        if (segments.Length == 0)
        {
            return TerrainRouteCorridorSample.None;
        }

        TerrainRouteCorridorSample best = TerrainRouteCorridorSample.None;
        for (int i = 0; i < segments.Length; i++)
        {
            TerrainRouteCorridorSegment segment = segments[i];
            float t = ClosestPointT(world, segment.From, segment.To);
            Vector2 closest = segment.From.Lerp(segment.To, t);
            float distance = world.DistanceTo(closest);
            if (distance > segment.ShoulderWidth)
            {
                continue;
            }

            float influence = 1.0f - Mathf.SmoothStep(segment.CoreWidth, segment.ShoulderWidth, distance);
            float core = 1.0f - Mathf.SmoothStep(segment.CoreWidth * 0.72f, segment.CoreWidth, distance);
            influence = Mathf.Clamp(influence, 0.0f, 1.0f);
            core = Mathf.Clamp(core, 0.0f, 1.0f);
            float targetHeight = Mathf.Lerp(segment.FromHeight, segment.ToHeight, t);

            if (influence <= best.Influence && distance >= best.Distance)
            {
                continue;
            }

            best = new TerrainRouteCorridorSample(
                true,
                segment.Kind,
                influence,
                core,
                distance,
                targetHeight,
                segment.ScenicPotential,
                segment.Traversability);
        }

        return best;
    }

    private static void AddSegmentToBuckets(
        TerrainRouteCorridorSegment segment,
        float chunkSize,
        Dictionary<TerrainTileCoord, List<TerrainRouteCorridorSegment>> buckets)
    {
        float padding = segment.ShoulderWidth + 2.0f;
        float minX = Mathf.Min(segment.From.X, segment.To.X) - padding;
        float maxX = Mathf.Max(segment.From.X, segment.To.X) + padding;
        float minZ = Mathf.Min(segment.From.Y, segment.To.Y) - padding;
        float maxZ = Mathf.Max(segment.From.Y, segment.To.Y) + padding;

        int minTileX = Mathf.FloorToInt(minX / chunkSize);
        int maxTileX = Mathf.FloorToInt(maxX / chunkSize);
        int minTileZ = Mathf.FloorToInt(minZ / chunkSize);
        int maxTileZ = Mathf.FloorToInt(maxZ / chunkSize);

        for (int z = minTileZ; z <= maxTileZ; z++)
        {
            for (int x = minTileX; x <= maxTileX; x++)
            {
                var coord = new TerrainTileCoord(x, z);
                if (!buckets.TryGetValue(coord, out List<TerrainRouteCorridorSegment>? segments))
                {
                    segments = new List<TerrainRouteCorridorSegment>(4);
                    buckets.Add(coord, segments);
                }

                segments.Add(segment);
            }
        }
    }

    private static float CorridorHeight(Vector2 world, TerrainGenerationProfile profile)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        return Mathf.Max(field.Height, profile.SeaLevel + 2.0f);
    }

    private static float ClosestPointT(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return 0.0f;
        }

        return Mathf.Clamp((point - from).Dot(segment) / lengthSquared, 0.0f, 1.0f);
    }

    private static CorridorWidth WidthFor(TerrainRouteKind kind)
    {
        return kind switch
        {
            TerrainRouteKind.RiverRoad => new CorridorWidth(18.0f, 62.0f),
            TerrainRouteKind.RidgePass => new CorridorWidth(11.0f, 42.0f),
            TerrainRouteKind.CoastalPath => new CorridorWidth(20.0f, 72.0f),
            TerrainRouteKind.ScenicTrail => new CorridorWidth(13.0f, 50.0f),
            _ => new CorridorWidth(14.0f, 54.0f)
        };
    }

    private static int StartHash(TerrainGenerationProfile profile, TerrainWorldPlan plan)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 397) ^ profile.Seed;
            hash = (hash * 397) ^ FloatHash(profile.ChunkSize);
            hash = (hash * 397) ^ FloatHash(plan.Center.X);
            hash = (hash * 397) ^ FloatHash(plan.Center.Y);
            hash = (hash * 397) ^ FloatHash(plan.WorldSize);
            hash = (hash * 397) ^ plan.Routes.Length;
            return hash;
        }
    }

    private static int AppendSegmentHash(int hash, TerrainRouteCorridorSegment segment)
    {
        unchecked
        {
            hash = (hash * 397) ^ (int)segment.Kind;
            hash = (hash * 397) ^ FloatHash(segment.From.X);
            hash = (hash * 397) ^ FloatHash(segment.From.Y);
            hash = (hash * 397) ^ FloatHash(segment.To.X);
            hash = (hash * 397) ^ FloatHash(segment.To.Y);
            hash = (hash * 397) ^ FloatHash(segment.CoreWidth);
            hash = (hash * 397) ^ FloatHash(segment.ShoulderWidth);
            return hash;
        }
    }

    private static int FloatHash(float value)
    {
        return unchecked((int)BitConverter.SingleToUInt32Bits(value));
    }

    private readonly record struct CorridorWidth(float CoreWidth, float ShoulderWidth);
}
