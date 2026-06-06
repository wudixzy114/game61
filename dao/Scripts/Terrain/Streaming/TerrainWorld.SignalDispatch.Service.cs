using Dao.Terrain.Generation;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
    private static class TerrainWorldSignalDispatchService
    {
        internal static void MarkStreamingSnapshotDirty(TerrainWorld world)
        {
            world._streamingStateRevision++;
        }

        internal static void EmitPlanReadySignalIfReady(TerrainWorld world)
        {
            if (!world._isReady)
            {
                return;
            }

            world.EmitSignal(PlanReadySignalName);
        }

        internal static void EmitPlanClearedSignalIfReady(TerrainWorld world)
        {
            if (!world._isReady)
            {
                return;
            }

            world.EmitSignal(PlanClearedSignalName);
        }

        internal static void EmitChunkLoadedSignalIfReady(TerrainWorld world, TerrainTileData data)
        {
            if (!world._isReady)
            {
                return;
            }

            world.EmitSignal(ChunkLoadedSignalName, data.Coord.X, data.Coord.Z, data.Lod, data.CollisionFaces.Length > 0);
        }

        internal static void EmitChunkUnloadedSignalIfReady(TerrainWorld world, TerrainChunk chunk)
        {
            if (!world._isReady)
            {
                return;
            }

            world.EmitSignal(ChunkUnloadedSignalName, chunk.Coord.X, chunk.Coord.Z, chunk.Lod, chunk.HasCollision);
        }

        internal static void EmitStreamingSnapshotChangedSignalIfNeeded(TerrainWorld world)
        {
            if (!world._isReady || world._emittedStreamingStateRevision == world._streamingStateRevision)
            {
                return;
            }

            world._emittedStreamingStateRevision = world._streamingStateRevision;
            world.EmitSignal(StreamingSnapshotChangedSignalName);
        }
    }
}
