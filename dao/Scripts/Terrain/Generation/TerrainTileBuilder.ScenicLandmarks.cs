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
        if (bestScore >= NaturalLandmarkThreshold(profile, best.Kind))
        {
            AddNaturalLandmarkInstance(coord, profile, best, 1621, 313, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.Waterfall &&
            bestWaterfallScore >= NaturalLandmarkThreshold(profile, TerrainLandmarkKind.Waterfall) &&
            IsDistinctNaturalLandmark(bestWaterfall.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestWaterfall, 1643, 315, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.DesertMonolith &&
            bestDesertMonolithScore >= NaturalLandmarkThreshold(profile, TerrainLandmarkKind.DesertMonolith) &&
            IsDistinctNaturalLandmark(bestDesertMonolith.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestDesertMonolith, 1657, 323, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.CanyonNeedle &&
            bestCanyonNeedleScore >= NaturalLandmarkThreshold(profile, TerrainLandmarkKind.CanyonNeedle) &&
            IsDistinctNaturalLandmark(bestCanyonNeedle.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestCanyonNeedle, 1663, 317, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.NaturalArch &&
            bestNaturalArchScore >= NaturalLandmarkThreshold(profile, TerrainLandmarkKind.NaturalArch) &&
            IsDistinctNaturalLandmark(bestNaturalArch.LocalPosition, landmarks))
        {
            AddNaturalLandmarkInstance(coord, profile, bestNaturalArch, 1667, 321, scatter, landmarks);
            addedLandmark = true;
        }

        if (best.Kind != TerrainLandmarkKind.GlacialRidge &&
            bestGlacialRidgeScore >= NaturalLandmarkThreshold(profile, TerrainLandmarkKind.GlacialRidge) &&
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

}
