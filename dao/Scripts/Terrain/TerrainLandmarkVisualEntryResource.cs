using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Configurable mesh visual for one terrain landmark kind.</summary>
[GlobalClass]
public partial class TerrainLandmarkVisualEntryResource : Resource
{
    [Export] public TerrainLandmarkKind Kind { get; set; } = TerrainLandmarkKind.Settlement;
    [Export] public Mesh? Mesh { get; set; }
    [Export] public string NodeName { get; set; } = string.Empty;
    [Export(PropertyHint.Range, "-32,32,0.01")] public float VerticalOffset { get; set; }
    [Export] public Vector3 AxisScale { get; set; } = Vector3.One;
    [Export(PropertyHint.Range, "0,512,1")] public float AabbHeightPadding { get; set; } = 132.0f;
}
