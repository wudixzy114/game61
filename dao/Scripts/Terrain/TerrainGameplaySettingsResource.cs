using Godot;

namespace Dao.Terrain;

/// <summary>Optional structured gameplay-landmark settings resource used by TerrainSettings to group vista, river, and terrace controls.</summary>
[GlobalClass]
public partial class TerrainGameplaySettingsResource : Resource
{
    [Export(PropertyHint.Range, "0,1,0.01")] public float VistaFrequency { get; set; } = 0.42f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RiverStrength { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,500,1")] public float RiverCarveDepth { get; set; } = 115.0f;
    [Export(PropertyHint.Range, "0,300,1")] public float TerraceStrength { get; set; } = 72.0f;
}
