using Godot;

namespace Dao.Terrain;

/// <summary>Optional structured streaming settings resource used by TerrainSettings to group LOD, cache, and async generation controls.</summary>
[GlobalClass]
public partial class TerrainStreamingSettingsResource : Resource
{
    [Export(PropertyHint.Range, "1,12,1")] public int StreamRadiusChunks { get; set; } = 5;
    [Export(PropertyHint.Range, "0,8,1")] public int CollisionRadiusChunks { get; set; } = 2;
    [Export(PropertyHint.Range, "0,5,1")] public int MaxLod { get; set; } = 3;
    [Export(PropertyHint.Range, "1,16,1")] public int MaxCompletedTilesPerFrame { get; set; } = 4;
    [Export(PropertyHint.Range, "1,64,1")] public int MaxQueuedTileJobs { get; set; } = 24;
    [Export(PropertyHint.Range, "0,512,1")] public int MaxCachedTileData { get; set; } = 96;
    [Export] public bool GenerateCollision { get; set; } = true;
    [Export] public bool UseNativeSamplerWhenAvailable { get; set; } = true;
}
