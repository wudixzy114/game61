using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain.Generation;

namespace Dao.Terrain.Streaming;

internal sealed record PendingTileJob(
    TerrainTileCoord Coord,
    int Lod,
    bool IncludeCollision,
    TerrainGenerationProfile Profile,
    int TerrainFeatureKey,
    CancellationTokenSource Cancellation,
    Task<TerrainTileData> Task);

internal sealed record PendingWorldPlanJob(
    int Version,
    TerrainGenerationProfile Profile,
    float WorldSize,
    CancellationTokenSource Cancellation,
    Task<TerrainWorldPlan> Task);

internal readonly record struct TerrainTileRequest(int Lod, bool IncludeCollision);

internal readonly record struct TerrainTileCacheKey(
    TerrainTileCoord Coord,
    int Lod,
    bool IncludeCollision,
    TerrainGenerationProfile Profile,
    int TerrainFeatureKey);
