using Godot;

namespace Dao.Terrain;

/// <summary>Optional structured terrain shape settings resource used by TerrainSettings to group landform controls.</summary>
[GlobalClass]
public partial class TerrainShapeSettingsResource : Resource
{
    [Export(PropertyHint.Range, "64,3000,1")] public float HeightScale { get; set; } = 780.0f;
    [Export(PropertyHint.Range, "-400,400,1")] public float SeaLevel { get; set; } = -18.0f;
    [Export(PropertyHint.Range, "512,12000,1")] public float ContinentScale { get; set; } = 5200.0f;
    [Export(PropertyHint.Range, "128,6000,1")] public float MountainScale { get; set; } = 1800.0f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float MountainWeight { get; set; } = 0.72f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ValleyWeight { get; set; } = 0.44f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float DetailWeight { get; set; } = 0.18f;
}
