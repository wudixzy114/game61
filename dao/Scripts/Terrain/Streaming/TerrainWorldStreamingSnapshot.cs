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
}
