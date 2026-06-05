using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static bool TileHasWaterfallPotential(
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals)
    {
        int stride = Mathf.Max(1, resolution / 4);
        for (int z = 0; z <= resolution; z += stride)
        {
            for (int x = 0; x <= resolution; x += stride)
            {
                int index = Index(x, z, vertexCountPerSide);
                float height = heights[index];
                if (height < profile.SeaLevel + 80.0f)
                {
                    continue;
                }

                TerrainWorldField field = fields[index];
                if (field.River < 0.36f || field.ScenicPotential < 0.24f)
                {
                    continue;
                }

                float slope = 1.0f - Mathf.Clamp(normals[index].Y, 0.0f, 1.0f);
                float elevation = Mathf.SmoothStep(profile.SeaLevel + 96.0f, profile.SeaLevel + profile.HeightScale * 0.70f, height);
                float potential =
                    field.River * 0.34f +
                    field.ScenicPotential * 0.28f +
                    elevation * 0.18f +
                    slope * 0.14f +
                    field.Exposure * 0.06f;

                if (field.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.RiverValley)
                {
                    potential += 0.10f;
                }

                if (potential >= 0.54f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TileHasDramaticNaturalPotential(
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals)
    {
        int stride = Mathf.Max(1, resolution / 4);
        for (int z = 0; z <= resolution; z += stride)
        {
            for (int x = 0; x <= resolution; x += stride)
            {
                int index = Index(x, z, vertexCountPerSide);
                float height = heights[index];
                if (height < profile.SeaLevel + 24.0f)
                {
                    continue;
                }

                TerrainWorldField field = fields[index];
                float slope = 1.0f - Mathf.Clamp(normals[index].Y, 0.0f, 1.0f);
                float elevation = Mathf.SmoothStep(profile.SeaLevel + 120.0f, profile.SeaLevel + profile.HeightScale * 0.70f, height);

                if (ScoreWaterfallLandmark(field, slope, elevation) >= 0.54f ||
                    ScoreDuneCrestLandmark(field, slope, elevation) >= 0.56f ||
                    ScoreDesertMonolithLandmark(field, slope, elevation) >= 0.56f ||
                    ScoreCanyonNeedleLandmark(field, slope, elevation) >= 0.58f ||
                    ScoreIceSpireLandmark(field, slope, elevation) >= 0.56f ||
                    ScoreNaturalArchLandmark(field, slope, elevation) >= 0.56f ||
                    ScoreGeothermalSpringLandmark(field, slope, elevation) >= 0.54f ||
                    ScoreGlacialRidgeLandmark(field, slope, elevation) >= 0.56f)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
