using Godot;

namespace Dao.Terrain;

/// <summary>Optional structured world settings resource used by TerrainSettings to group seed and tile scale inputs.</summary>
[GlobalClass]
public partial class TerrainWorldSettingsResource : Resource
{
    [Export] public int Seed { get; set; } = 613_061;
    [Export(PropertyHint.Range, "64,2048,1")] public float ChunkSize { get; set; } = 192.0f;
    [Export(PropertyHint.Range, "16,192,1")] public int BaseResolution { get; set; } = 64;
}
