using Godot;

namespace Dao.Terrain;

/// <summary>Optional structured rendering settings resource used by TerrainSettings to group visual mesh generation controls.</summary>
[GlobalClass]
public partial class TerrainRenderingSettingsResource : Resource
{
    [Export(PropertyHint.Range, "0,120,1")] public float SkirtDepth { get; set; } = 42.0f;
}
