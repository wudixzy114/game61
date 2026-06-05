using Godot;

namespace Dao.Terrain;

/// <summary>Reusable scatter output tuning resource for a single biome-scatter variant family.</summary>
[GlobalClass]
public partial class TerrainScatterVariantRuleResource : Resource
{
    [Export(PropertyHint.Range, "0,1,0.001")] public float ProbabilityLow { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float ProbabilityHigh { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float BaseScale { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0,4,0.001")] public float ScaleJitterFactor { get; set; } = 0.86f;
    [Export] public Color TintLow { get; set; } = new(0.30f, 0.30f, 0.30f);
    [Export] public Color TintHigh { get; set; } = new(0.60f, 0.60f, 0.60f);
}
