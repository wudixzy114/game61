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
}
