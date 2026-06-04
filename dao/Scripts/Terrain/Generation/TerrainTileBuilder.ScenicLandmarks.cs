using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Partial class containing scenic landmark detection and scoring for tile generation.</summary>
public static partial class TerrainTileBuilder
{
    private static void AddBestLandmark(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals,
        CancellationToken cancellationToken,
        List<TerrainScatterInstance> scatter,
        List<TerrainLandmarkData> landmarks)
    {
        if (!TileHasWaterfallPotential(profile, resolution, vertexCountPerSide, heights, fields, normals))
        {
            return;
        }

        TerrainLandmarkData best = default;
        float bestScore = 0.0f;

        for (int i = 0; i < 8; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            float localX = (0.15f + Hash01(coord.X, coord.Z, i * 173, profile.Seed + 101) * 0.70f) * profile.ChunkSize;
            float localZ = (0.15f + Hash01(coord.X, coord.Z, i * 277, profile.Seed + 103) * 0.70f) * profile.ChunkSize;
            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
            float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
            TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
            float flatness = 1.0f - Mathf.Clamp(slope * 2.2f, 0.0f, 1.0f);
            float heightScore = Mathf.Clamp((height - profile.SeaLevel - 140.0f) / 560.0f, 0.0f, 1.0f);
            float rarity = Hash01(coord.X, coord.Z, i * 421, profile.Seed + 107);

            TerrainLandmarkKind kind = TerrainLandmarkKind.Vista;
            float score =
                field.ScenicPotential * 0.52f +
                heightScore * 0.20f +
                flatness * 0.16f +
                field.Traversability * 0.08f +
                rarity * 0.04f;

            if (field.River > 0.70f && height > profile.SeaLevel + 10.0f && slope < 0.24f)
            {
                kind = TerrainLandmarkKind.RiverCrossing;
                score = 0.70f + field.River * 0.16f + flatness * 0.10f + field.Traversability * 0.04f;
            }
            else if (field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau &&
                height > profile.SeaLevel + 300.0f &&
                slope is > 0.14f and < 0.46f)
            {
                kind = TerrainLandmarkKind.MountainPass;
                score = 0.52f + field.ScenicPotential * 0.24f + heightScore * 0.14f + (1.0f - Mathf.Abs(slope - 0.28f) * 2.0f) * 0.10f;
            }
            else if (rarity > 0.92f && slope < 0.26f && field.Traversability > 0.22f)
            {
                kind = TerrainLandmarkKind.AncientStone;
                score = 0.74f + field.ScenicPotential * 0.12f + flatness * 0.10f + heightScore * 0.04f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = new TerrainLandmarkData(kind, new Vector3(localX, height, localZ), score, $"{kind}_{coord.X}_{coord.Z}");
            }
        }

        if (bestScore < 0.66f)
        {
            return;
        }

        landmarks.Add(best);
        float rotation = Hash01(coord.X, coord.Z, 8191, profile.Seed + 109) * Mathf.Pi * 2.0f;
        float scale = best.Kind == TerrainLandmarkKind.AncientStone ? 7.0f : 4.6f;
        Color tint = best.Kind == TerrainLandmarkKind.RiverCrossing
            ? new Color(0.42f, 0.48f, 0.45f)
            : new Color(0.52f, 0.50f, 0.44f);
        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Landmark, best.LocalPosition, rotation, scale, tint, best.Kind));
    }

    private static void AddScenicNaturalLandmarks(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals,
        CancellationToken cancellationToken,
        List<TerrainScatterInstance> scatter,
        List<TerrainLandmarkData> landmarks)
    {
        if (!TileHasDramaticNaturalPotential(profile, resolution, vertexCountPerSide, heights, fields, normals))
        {
            return;
        }

        TerrainLandmarkData best = default;
        float bestScore = 0.0f;
        TerrainLandmarkData bestWaterfall = default;
        float bestWaterfallScore = 0.0f;
        TerrainLandmarkData bestDesertMonolith = default;
        float bestDesertMonolithScore = 0.0f;
        TerrainLandmarkData bestCanyonNeedle = default;
        float bestCanyonNeedleScore = 0.0f;
        TerrainLandmarkData bestNaturalArch = default;
        float bestNaturalArchScore = 0.0f;
        TerrainLandmarkData bestGlacialRidge = default;
        float bestGlacialRidgeScore = 0.0f;

        for (int i = 0; i < 16; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            float localX = (0.10f + Hash01(coord.X, coord.Z, i * 1559, profile.Seed + 307) * 0.80f) * profile.ChunkSize;
            float localZ = (0.10f + Hash01(coord.X, coord.Z, i * 1601, profile.Seed + 311) * 0.80f) * profile.ChunkSize;
            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            if (height < profile.SeaLevel + 96.0f)
            {
                continue;
            }

            Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
            float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
            TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
            float elevation = Mathf.SmoothStep(profile.SeaLevel + 120.0f, profile.SeaLevel + profile.HeightScale * 0.70f, height);
            TerrainLandmarkKind kind = TerrainLandmarkKind.Waterfall;
            float waterfallScore = ScoreWaterfallLandmark(field, slope, elevation);
            float score = waterfallScore;
            ConsiderNaturalLandmark(TerrainLandmarkKind.DuneCrest, ScoreDuneCrestLandmark(field, slope, elevation), ref kind, ref score);
            float desertMonolithScore = ScoreDesertMonolithLandmark(field, slope, elevation);
            ConsiderNaturalLandmark(TerrainLandmarkKind.DesertMonolith, desertMonolithScore, ref kind, ref score);
            float canyonNeedleScore = ScoreCanyonNeedleLandmark(field, slope, elevation);
            ConsiderNaturalLandmark(TerrainLandmarkKind.CanyonNeedle, canyonNeedleScore, ref kind, ref score);
            ConsiderNaturalLandmark(TerrainLandmarkKind.IceSpire, ScoreIceSpireLandmark(field, slope, elevation), ref kind, ref score);
            float naturalArchScore = ScoreNaturalArchLandmark(field, slope, elevation);
            ConsiderNaturalLandmark(TerrainLandmarkKind.NaturalArch, naturalArchScore, ref kind, ref score);
            ConsiderNaturalLandmark(TerrainLandmarkKind.GeothermalSpring, ScoreGeothermalSpringLandmark(field, slope, elevation), ref kind, ref score);
            float glacialRidgeScore = ScoreGlacialRidgeLandmark(field, slope, elevation);
            ConsiderNaturalLandmark(TerrainLandmarkKind.GlacialRidge, glacialRidgeScore, ref kind, ref score);

            if (waterfallScore > bestWaterfallScore)
            {
                bestWaterfallScore = waterfallScore;
                bestWaterfall = new TerrainLandmarkData(
                    TerrainLandmarkKind.Waterfall,
                    new Vector3(localX, height, localZ),
                    Mathf.Clamp(waterfallScore, 0.0f, 1.0f),
                    $"Waterfall_{coord.X}_{coord.Z}");
            }

            if (desertMonolithScore > bestDesertMonolithScore)
            {
                bestDesertMonolithScore = desertMonolithScore;
                bestDesertMonolith = new TerrainLandmarkData(
                    TerrainLandmarkKind.DesertMonolith,
                    new Vector3(localX, height, localZ),
                    Mathf.Clamp(desertMonolithScore, 0.0f, 1.0f),
                    $"DesertMonolith_{coord.X}_{coord.Z}");
            }

            if (canyonNeedleScore > bestCanyonNeedleScore)
            {
                bestCanyonNeedleScore = canyonNeedleScore;
                bestCanyonNeedle = new TerrainLandmarkData(
                    TerrainLandmarkKind.CanyonNeedle,
                    new Vector3(localX, height, localZ),
                    Mathf.Clamp(canyonNeedleScore, 0.0f, 1.0f),
                    $"CanyonNeedle_{coord.X}_{coord.Z}");
            }

            if (naturalArchScore > bestNaturalArchScore)
            {
                bestNaturalArchScore = naturalArchScore;
                bestNaturalArch = new TerrainLandmarkData(
                    TerrainLandmarkKind.NaturalArch,
                    new Vector3(localX, height, localZ),
                    Mathf.Clamp(naturalArchScore, 0.0f, 1.0f),
                    $"NaturalArch_{coord.X}_{coord.Z}");
            }

            if (glacialRidgeScore > bestGlacialRidgeScore)
            {
                bestGlacialRidgeScore = glacialRidgeScore;
                bestGlacialRidge = new TerrainLandmarkData(
                    TerrainLandmarkKind.GlacialRidge,
                    new Vector3(localX, height, localZ),
                    Mathf.Clamp(glacialRidgeScore, 0.0f, 1.0f),
                    $"GlacialRidge_{coord.X}_{coord.Z}");
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = new TerrainLandmarkData(
                    kind,
                    new Vector3(localX, height, localZ),
                    Mathf.Clamp(score, 0.0f, 1.0f),
                    $"{kind}_{coord.X}_{coord.Z}");
            }
        }

        bool addedLandmark = false;
        if (bestScore >= NaturalLandmarkThreshold(best.Kind))
        {
            AddNaturalLandmarkInstance(coord, profile, best, 1621, 313, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.Waterfall &&
            bestWaterfallScore >= NaturalLandmarkThreshold(TerrainLandmarkKind.Waterfall) &&
            IsDistinctNaturalLandmark(bestWaterfall.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestWaterfall, 1643, 315, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.DesertMonolith &&
            bestDesertMonolithScore >= NaturalLandmarkThreshold(TerrainLandmarkKind.DesertMonolith) &&
            IsDistinctNaturalLandmark(bestDesertMonolith.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestDesertMonolith, 1657, 323, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.CanyonNeedle &&
            bestCanyonNeedleScore >= NaturalLandmarkThreshold(TerrainLandmarkKind.CanyonNeedle) &&
            IsDistinctNaturalLandmark(bestCanyonNeedle.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestCanyonNeedle, 1663, 317, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.NaturalArch &&
            bestNaturalArchScore >= NaturalLandmarkThreshold(TerrainLandmarkKind.NaturalArch) &&
            IsDistinctNaturalLandmark(bestNaturalArch.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestNaturalArch, 1667, 321, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.GlacialRidge &&
            bestGlacialRidgeScore >= NaturalLandmarkThreshold(TerrainLandmarkKind.GlacialRidge) &&
            IsDistinctNaturalLandmark(bestGlacialRidge.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestGlacialRidge, 1699, 319, scatter, landmarks);
            addedLandmark = true;
        }

        if (!addedLandmark)
        {
            return;
        }
    }

    private static void AddNaturalLandmarkInstance(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        TerrainLandmarkData landmark,
        int rotationSalt,
        int seedOffset,
        List<TerrainScatterInstance> scatter,
        List<TerrainLandmarkData> landmarks)
    {
        landmarks.Add(landmark);
        float rotation = Hash01(coord.X, coord.Z, rotationSalt, profile.Seed + seedOffset) * Mathf.Pi * 2.0f;
        float scale = NaturalLandmarkScale(landmark.Kind, landmark.Score);
        Color tint = NaturalLandmarkColor(landmark.Kind, landmark.Score);
        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Landmark, landmark.LocalPosition, rotation, scale, tint, landmark.Kind));
    }

    private static bool IsDistinctNaturalLandmark(
        Vector3 localPosition,
        List<TerrainLandmarkData> landmarks)
    {
        foreach (TerrainLandmarkData landmark in landmarks)
        {
            if (localPosition.DistanceSquaredTo(landmark.LocalPosition) <= 144.0f)
            {
                return false;
            }
        }

        return true;
    }

    private static void ConsiderNaturalLandmark(
        TerrainLandmarkKind candidateKind,
        float candidateScore,
        ref TerrainLandmarkKind bestKind,
        ref float bestScore)
    {
        if (candidateScore > bestScore)
        {
            bestKind = candidateKind;
            bestScore = candidateScore;
        }
    }

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

    private static float NaturalLandmarkThreshold(TerrainLandmarkKind kind)
    {
        return kind switch
        {
            TerrainLandmarkKind.Waterfall => 0.74f,
            TerrainLandmarkKind.DuneCrest => 0.68f,
            TerrainLandmarkKind.DesertMonolith => 0.66f,
            TerrainLandmarkKind.CanyonNeedle => 0.68f,
            TerrainLandmarkKind.IceSpire => 0.66f,
            TerrainLandmarkKind.NaturalArch => 0.64f,
            TerrainLandmarkKind.GeothermalSpring => 0.64f,
            TerrainLandmarkKind.GlacialRidge => 0.64f,
            _ => 0.72f
        };
    }

    private static float NaturalLandmarkScale(TerrainLandmarkKind kind, float score)
    {
        return kind switch
        {
            TerrainLandmarkKind.Waterfall => 4.8f + score * 3.2f,
            TerrainLandmarkKind.DuneCrest => 4.4f + score * 2.6f,
            TerrainLandmarkKind.DesertMonolith => 3.6f + score * 2.8f,
            TerrainLandmarkKind.CanyonNeedle => 4.2f + score * 3.0f,
            TerrainLandmarkKind.IceSpire => 3.6f + score * 2.4f,
            TerrainLandmarkKind.NaturalArch => 4.2f + score * 2.8f,
            TerrainLandmarkKind.GeothermalSpring => 3.8f + score * 2.2f,
            TerrainLandmarkKind.GlacialRidge => 4.4f + score * 2.6f,
            _ => 4.8f + score * 2.0f
        };
    }

    private static Color NaturalLandmarkColor(TerrainLandmarkKind kind, float score)
    {
        Color baseColor = kind switch
        {
            TerrainLandmarkKind.Waterfall => new Color(0.30f, 0.62f, 0.82f),
            TerrainLandmarkKind.DuneCrest => new Color(0.76f, 0.58f, 0.30f),
            TerrainLandmarkKind.DesertMonolith => new Color(0.62f, 0.42f, 0.24f),
            TerrainLandmarkKind.CanyonNeedle => new Color(0.58f, 0.36f, 0.24f),
            TerrainLandmarkKind.IceSpire => new Color(0.62f, 0.76f, 0.86f),
            TerrainLandmarkKind.NaturalArch => new Color(0.66f, 0.44f, 0.28f),
            TerrainLandmarkKind.GeothermalSpring => new Color(0.24f, 0.58f, 0.62f),
            TerrainLandmarkKind.GlacialRidge => new Color(0.70f, 0.82f, 0.88f),
            _ => new Color(0.52f, 0.50f, 0.44f)
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp(score * 0.18f, 0.0f, 0.18f));
    }

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
