using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static float ScoreWaterfallLandmark(TerrainWorldField field, float slope, float elevation)
    {
        float score =
            Mathf.SmoothStep(0.48f, 0.86f, field.River) * 0.38f +
            Mathf.SmoothStep(0.16f, 0.42f, slope) * 0.22f +
            elevation * 0.18f +
            field.ScenicPotential * 0.18f +
            field.Exposure * 0.04f;

        if (field.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.RiverValley)
        {
            score += 0.08f;
        }

        return score;
    }

    private static float ScoreDuneCrestLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (!IsDesertLike(field) || slope > 0.22f)
        {
            return 0.0f;
        }

        float flatness = 1.0f - Mathf.Clamp(slope * 3.4f, 0.0f, 1.0f);
        float dryness = Mathf.Clamp(1.0f - field.Moisture, 0.0f, 1.0f);
        return 0.44f +
            dryness * 0.18f +
            field.Temperature * 0.12f +
            field.ScenicPotential * 0.14f +
            field.Exposure * 0.08f +
            flatness * 0.08f +
            elevation * 0.04f;
    }

    private static float ScoreDesertMonolithLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (!IsDesertLike(field) || slope is < 0.08f or > 0.42f)
        {
            return 0.0f;
        }

        float slopeFit = 1.0f - Mathf.Clamp(Mathf.Abs(slope - 0.25f) * 3.8f, 0.0f, 1.0f);
        float dryness = Mathf.Clamp(1.0f - field.Moisture, 0.0f, 1.0f);
        return 0.36f +
            field.ScenicPotential * 0.22f +
            field.Exposure * 0.18f +
            dryness * 0.16f +
            slopeFit * 0.10f +
            elevation * 0.08f;
    }

    private static float ScoreCanyonNeedleLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (field.LandscapeKind is not (TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau or TerrainLandscapeKind.RiverValley) ||
            slope < 0.10f ||
            elevation < 0.18f)
        {
            return 0.0f;
        }

        float slopeFit =
            Mathf.SmoothStep(0.10f, 0.28f, slope) *
            (1.0f - Mathf.SmoothStep(0.62f, 0.84f, slope));
        float riverCut = Mathf.SmoothStep(0.34f, 0.76f, field.River);
        float terrainBonus = field.LandscapeKind switch
        {
            TerrainLandscapeKind.Canyon => 0.12f,
            TerrainLandscapeKind.RiverValley => 0.09f,
            TerrainLandscapeKind.VistaPlateau => 0.08f,
            TerrainLandscapeKind.Highlands => 0.07f,
            _ => 0.05f
        };
        return 0.38f +
            field.ScenicPotential * 0.24f +
            field.Exposure * 0.18f +
            elevation * 0.14f +
            slopeFit * 0.12f +
            riverCut * 0.08f +
            terrainBonus;
    }

    private static float ScoreIceSpireLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (field.BiomeKind != TerrainBiomeKind.Snowfield && field.LandscapeKind != TerrainLandscapeKind.Snowfield)
        {
            return 0.0f;
        }

        float slopeFit =
            Mathf.SmoothStep(0.08f, 0.24f, slope) *
            (1.0f - Mathf.SmoothStep(0.54f, 0.78f, slope));
        float exposedIce = Mathf.SmoothStep(0.28f, 0.70f, field.Exposure);
        float cold = Mathf.Clamp(1.0f - field.Temperature, 0.0f, 1.0f);
        return 0.42f +
            field.ScenicPotential * 0.18f +
            field.Exposure * 0.24f +
            elevation * 0.16f +
            slopeFit * 0.18f +
            exposedIce * 0.05f +
            cold * 0.07f;
    }

    private static float ScoreNaturalArchLandmark(TerrainWorldField field, float slope, float elevation)
    {
        bool rockArchTerrain = field.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau;
        bool erodedRiverArchTerrain = field.LandscapeKind == TerrainLandscapeKind.RiverValley && field.River > 0.42f && field.Exposure > 0.30f;
        bool desertArchTerrain = IsDesertLike(field) && field.Exposure > 0.50f && slope > 0.12f;
        if ((!rockArchTerrain && !erodedRiverArchTerrain && !desertArchTerrain) || slope is < 0.08f or > 0.40f)
        {
            return 0.0f;
        }

        float slopeFit =
            Mathf.SmoothStep(0.08f, 0.18f, slope) *
            (1.0f - Mathf.SmoothStep(0.34f, 0.46f, slope));
        float dryness = Mathf.Clamp(1.0f - field.Moisture, 0.0f, 1.0f);
        float erosionFit = Mathf.Max(
            Mathf.SmoothStep(0.34f, 0.74f, field.River) * 0.08f,
            dryness * 0.07f);
        float terrainBonus = field.LandscapeKind switch
        {
            TerrainLandscapeKind.Canyon => 0.08f,
            TerrainLandscapeKind.VistaPlateau => 0.07f,
            TerrainLandscapeKind.RiverValley => 0.06f,
            TerrainLandscapeKind.Highlands => 0.05f,
            _ => desertArchTerrain ? 0.06f : 0.04f
        };
        return 0.40f +
            field.ScenicPotential * 0.23f +
            field.Exposure * 0.20f +
            dryness * 0.08f +
            elevation * 0.08f +
            slopeFit * 0.13f +
            erosionFit +
            terrainBonus;
    }

    private static float ScoreGeothermalSpringLandmark(TerrainWorldField field, float slope, float elevation)
    {
        bool springTerrain =
            field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.RiverValley or TerrainLandscapeKind.Snowfield ||
            field.BiomeKind == TerrainBiomeKind.Snowfield;
        if (!springTerrain || slope > 0.30f || field.Moisture < 0.28f || field.River > 0.78f)
        {
            return 0.0f;
        }

        bool exposedSnow = field.BiomeKind == TerrainBiomeKind.Snowfield || field.LandscapeKind == TerrainLandscapeKind.Snowfield;
        if (exposedSnow && field.Exposure > 0.46f && elevation > 0.42f && field.River < 0.42f)
        {
            return 0.0f;
        }

        float flatness = 1.0f - Mathf.Clamp(slope * 2.6f, 0.0f, 1.0f);
        float waterAccess = Mathf.Max(field.Moisture, Mathf.SmoothStep(0.18f, 0.58f, field.River));
        float terrainBonus = field.BiomeKind == TerrainBiomeKind.Snowfield || field.LandscapeKind == TerrainLandscapeKind.Snowfield
            ? 0.08f
            : field.LandscapeKind == TerrainLandscapeKind.RiverValley
            ? 0.07f
            : 0.05f;
        float thermalContrast =
            Mathf.Clamp(1.0f - field.Temperature, 0.0f, 1.0f) * 0.08f +
            Mathf.Clamp(field.Temperature, 0.0f, 1.0f) * 0.04f;
        return 0.40f +
            field.ScenicPotential * 0.18f +
            waterAccess * 0.18f +
            elevation * 0.12f +
            flatness * 0.12f +
            terrainBonus +
            thermalContrast;
    }

    private static float ScoreGlacialRidgeLandmark(TerrainWorldField field, float slope, float elevation)
    {
        if (field.BiomeKind != TerrainBiomeKind.Snowfield && field.LandscapeKind != TerrainLandscapeKind.Snowfield)
        {
            return 0.0f;
        }

        if (slope > 0.20f)
        {
            return 0.0f;
        }

        float ridgeFit = Mathf.Clamp((field.Exposure + elevation) * 0.5f, 0.0f, 1.0f);
        float cold = Mathf.Clamp(1.0f - field.Temperature, 0.0f, 1.0f);
        return 0.46f +
            field.ScenicPotential * 0.18f +
            field.Exposure * 0.22f +
            elevation * 0.18f +
            ridgeFit * 0.08f +
            cold * 0.07f;
    }

    private static bool IsDesertLike(TerrainWorldField field)
    {
        return field.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis &&
            field.Temperature > 0.34f &&
            field.Moisture < 0.62f;
    }

    private static float NaturalLandmarkThreshold(TerrainGenerationProfile profile, TerrainLandmarkKind kind)
    {
        return GetNaturalLandmarkRule(profile, kind).Threshold;
    }

    private static float NaturalLandmarkScale(TerrainGenerationProfile profile, TerrainLandmarkKind kind, float score)
    {
        TerrainScenicLandmarkRule rule = GetNaturalLandmarkRule(profile, kind);
        return rule.BaseScale + score * rule.ScoreScale;
    }

    private static Color NaturalLandmarkColor(TerrainGenerationProfile profile, TerrainLandmarkKind kind, float score)
    {
        TerrainScenicLandmarkRule rule = GetNaturalLandmarkRule(profile, kind);
        return rule.BaseColor.Lerp(Colors.White, Mathf.Clamp(score * 0.18f, 0.0f, 0.18f));
    }
}
