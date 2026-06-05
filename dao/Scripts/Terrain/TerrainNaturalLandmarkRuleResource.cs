using Godot;

namespace Dao.Terrain;

/// <summary>Configurable rule resource for a single scenic natural landmark family.</summary>
[GlobalClass]
public partial class TerrainNaturalLandmarkRuleResource : Resource
{
    [Export(PropertyHint.Range, "0,1,0.001")] public float Threshold { get; set; } = 0.72f;
    [Export(PropertyHint.Range, "0,32,0.01")] public float BaseScale { get; set; } = 4.8f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float ScoreScale { get; set; } = 2.0f;
    [Export] public Color BaseColor { get; set; } = new(0.52f, 0.50f, 0.44f, 1.0f);
}
