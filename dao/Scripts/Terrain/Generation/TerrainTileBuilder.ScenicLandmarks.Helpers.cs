using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
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
        float scale = NaturalLandmarkScale(profile, landmark.Kind, landmark.Score);
        Color tint = NaturalLandmarkColor(profile, landmark.Kind, landmark.Score);
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
}
