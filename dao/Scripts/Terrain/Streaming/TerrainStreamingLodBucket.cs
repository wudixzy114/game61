namespace Dao.Terrain.Streaming;

/// <summary>Per-LOD streaming diagnostics bucket for loaded chunks, desired chunks, and queued jobs.</summary>
public readonly record struct TerrainStreamingLodBucket(
    int Lod,
    int DesiredChunkCount,
    int LoadedChunkCount,
    int QueuedTileJobCount);
