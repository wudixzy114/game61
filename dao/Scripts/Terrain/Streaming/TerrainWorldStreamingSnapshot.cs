using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

/// <summary>Read-only diagnostics snapshot for TerrainWorld streaming state.</summary>
public readonly record struct TerrainWorldStreamingSnapshot(
    TerrainGenerationProfile Profile,
    bool HasFocus,
    Vector3 FocusPosition,
    TerrainTileCoord FocusCoord,
    int StreamRadiusChunks,
    int DesiredChunkCount,
    TerrainTileCoord[] DesiredChunks,
    int LoadedChunkCount,
    TerrainTileCoord[] LoadedChunks,
    TerrainStreamingLodBucket[] LodBuckets,
    int QueuedTileJobCount,
    TerrainTileCoord[] QueuedTileJobs,
    int RetiredTileJobCount,
    int TileCacheCount,
    int TileCacheLimit,
    int MaxQueuedTileJobs,
    int MaxCompletedTilesPerFrame,
    bool HasWorldPlan,
    bool IsWorldPlanGenerationPending,
    bool StreamTerrainBeforeOpenWorldPlanReady)
{
    public bool TileCacheWithinLimit => TileCacheLimit <= 0 || TileCacheCount <= TileCacheLimit;
    public bool TileJobQueueWithinLimit => QueuedTileJobCount <= MaxQueuedTileJobs;
    public int LoadedLodBucketCount => LodBuckets?.Length ?? 0;
    public int HighestLoadedLod => HighestLoadedLodIn(LodBuckets);
    public int LowestLoadedLod => LowestLoadedLodIn(LodBuckets);
    public bool CanStreamTerrain => HasFocus &&
        (HasWorldPlan || StreamTerrainBeforeOpenWorldPlanReady) &&
        TileCacheWithinLimit &&
        TileJobQueueWithinLimit;
    public bool FocusTileLoaded => HasFocus && ContainsCoord(LoadedChunks, FocusCoord);
    public bool DesiredChunksLoaded => DesiredChunkCount > 0 &&
        DesiredChunks is not null &&
        LoadedChunks is not null &&
        DesiredChunks.Length == DesiredChunkCount &&
        LoadedChunks.Length == LoadedChunkCount &&
        LoadedChunkCount >= DesiredChunkCount &&
        AllCoordsPresent(DesiredChunks, LoadedChunks);
    public bool FocusAreaReady => CanStreamTerrain &&
        FocusTileLoaded &&
        DesiredChunksLoaded &&
        QueuedTileJobCount == 0 &&
        RetiredTileJobCount == 0 &&
        !IsWorldPlanGenerationPending;

    private static bool AllCoordsPresent(TerrainTileCoord[] required, TerrainTileCoord[] available)
    {
        if (required is null || available is null)
        {
            return false;
        }

        for (int i = 0; i < required.Length; i++)
        {
            if (!ContainsCoord(available, required[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsCoord(TerrainTileCoord[] coords, TerrainTileCoord coord)
    {
        if (coords is null)
        {
            return false;
        }

        for (int i = 0; i < coords.Length; i++)
        {
            if (coords[i] == coord)
            {
                return true;
            }
        }

        return false;
    }

    private static int HighestLoadedLodIn(TerrainStreamingLodBucket[]? buckets)
    {
        if (buckets is null || buckets.Length == 0)
        {
            return -1;
        }

        int highest = -1;
        for (int i = 0; i < buckets.Length; i++)
        {
            TerrainStreamingLodBucket bucket = buckets[i];
            if (bucket.LoadedChunkCount > 0 && bucket.Lod > highest)
            {
                highest = bucket.Lod;
            }
        }

        return highest;
    }

    private static int LowestLoadedLodIn(TerrainStreamingLodBucket[]? buckets)
    {
        if (buckets is null || buckets.Length == 0)
        {
            return -1;
        }

        int lowest = int.MaxValue;
        for (int i = 0; i < buckets.Length; i++)
        {
            TerrainStreamingLodBucket bucket = buckets[i];
            if (bucket.LoadedChunkCount > 0 && bucket.Lod < lowest)
            {
                lowest = bucket.Lod;
            }
        }

        return lowest == int.MaxValue ? -1 : lowest;
    }
}
