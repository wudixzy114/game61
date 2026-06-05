using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static NaturalLandmarkRule GetNaturalLandmarkRule(TerrainLandmarkKind kind)
    {
        return kind switch
        {
            TerrainLandmarkKind.Waterfall => new NaturalLandmarkRule(0.74f, 4.8f, 3.2f, new Color(0.30f, 0.62f, 0.82f)),
            TerrainLandmarkKind.DuneCrest => new NaturalLandmarkRule(0.68f, 4.4f, 2.6f, new Color(0.76f, 0.58f, 0.30f)),
            TerrainLandmarkKind.DesertMonolith => new NaturalLandmarkRule(0.66f, 3.6f, 2.8f, new Color(0.62f, 0.42f, 0.24f)),
            TerrainLandmarkKind.CanyonNeedle => new NaturalLandmarkRule(0.68f, 4.2f, 3.0f, new Color(0.58f, 0.36f, 0.24f)),
            TerrainLandmarkKind.IceSpire => new NaturalLandmarkRule(0.66f, 3.6f, 2.4f, new Color(0.62f, 0.76f, 0.86f)),
            TerrainLandmarkKind.NaturalArch => new NaturalLandmarkRule(0.64f, 4.2f, 2.8f, new Color(0.66f, 0.44f, 0.28f)),
            TerrainLandmarkKind.GeothermalSpring => new NaturalLandmarkRule(0.64f, 3.8f, 2.2f, new Color(0.24f, 0.58f, 0.62f)),
            TerrainLandmarkKind.GlacialRidge => new NaturalLandmarkRule(0.64f, 4.4f, 2.6f, new Color(0.70f, 0.82f, 0.88f)),
            _ => new NaturalLandmarkRule(0.72f, 4.8f, 2.0f, new Color(0.52f, 0.50f, 0.44f))
        };
    }

    private readonly record struct NaturalLandmarkRule(
        float Threshold,
        float BaseScale,
        float ScoreScale,
        Color BaseColor);
}
