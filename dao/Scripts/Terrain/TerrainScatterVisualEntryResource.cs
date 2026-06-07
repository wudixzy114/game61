using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Configurable mesh visual for one terrain scatter kind.</summary>
[GlobalClass]
public partial class TerrainScatterVisualEntryResource : Resource
{
    [Export] public TerrainScatterKind Kind { get; set; } = TerrainScatterKind.Tree;
    [Export] public Mesh? Mesh { get; set; }
    [Export] public PackedScene? Scene { get; set; }
    [Export] public bool PreferSceneInstances { get; set; }
    [Export] public string NodeName { get; set; } = string.Empty;
    [Export(PropertyHint.Range, "-32,32,0.01")] public float VerticalOffset { get; set; }
    [Export] public Vector3 AxisScale { get; set; } = Vector3.One;
    [Export(PropertyHint.Range, "0,512,1")] public float AabbHeightPadding { get; set; } = 64.0f;
    [Export(PropertyHint.Range, "0,8,1")] public int MinLod { get; set; }
    [Export(PropertyHint.Range, "0,8,1")] public int MaxLod { get; set; } = 8;
    [Export] public bool CreatesCollision { get; set; }
    [Export] public bool CreatesNavigationObstacle { get; set; }
    [Export] public string InteractionTag { get; set; } = string.Empty;
}
