using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static void AddGameplayScatter(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int cellX,
        int cellZ,
        float localX,
        float localZ,
        float height,
        float slope,
        TerrainWorldField field,
        TerrainScatterRuleSetSnapshot rules,
        List<TerrainScatterInstance> scatter)
    {
        TerrainGameplayScatterRule understoryRule = rules.Understory;
        float understoryRoll = Hash01(coord.X, coord.Z, cellX * 2711 + cellZ * 2797, profile.Seed + 149);
        if (slope < understoryRule.MaxSlope &&
            field.ResourcePotential > understoryRule.MinPrimary &&
            field.Moisture > understoryRule.MinSecondary &&
            field.Temperature > understoryRule.MinTemperature &&
            field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Wetland or TerrainLandscapeKind.RiverValley &&
            understoryRoll < Mathf.Lerp(understoryRule.ProbabilityLow, understoryRule.ProbabilityHigh, field.ResourcePotential))
        {
            float scale = understoryRule.BaseScale + Hash01(coord.X, coord.Z, cellX * 3253 + cellZ * 3307, profile.Seed + 151) * understoryRule.ScaleJitter;
            float rotation = Hash01(coord.X, coord.Z, cellX * 3533 + cellZ * 3581, profile.Seed + 157) * Mathf.Pi * 2.0f;
            Color tint = understoryRule.TintLow.Lerp(understoryRule.TintHigh, field.ResourcePotential);
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Understory, new Vector3(localX, height, localZ), rotation, scale, tint));
        }

        TerrainGameplayScatterRule resourceRule = rules.ResourceNode;
        float resourceRoll = Hash01(coord.X, coord.Z, cellX * 3761 + cellZ * 3851, profile.Seed + 163);
        if (field.ResourcePotential > resourceRule.MinPrimary &&
            field.Traversability > resourceRule.MinSecondary &&
            slope < resourceRule.MaxSlope &&
            resourceRoll < Mathf.Lerp(resourceRule.ProbabilityLow, resourceRule.ProbabilityHigh, field.ResourcePotential))
        {
            float scale = resourceRule.BaseScale + Hash01(coord.X, coord.Z, cellX * 4001 + cellZ * 4027, profile.Seed + 167) * resourceRule.ScaleJitter;
            float rotation = Hash01(coord.X, coord.Z, cellX * 4211 + cellZ * 4241, profile.Seed + 173) * Mathf.Pi * 2.0f;
            Color tint = resourceRule.TintLow.Lerp(resourceRule.TintHigh, Mathf.Clamp(field.ResourcePotential, 0.0f, 1.0f));
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.ResourceNode, new Vector3(localX, height, localZ), rotation, scale, tint));
        }

        TerrainGameplayScatterRule hazardRule = rules.HazardOutcrop;
        float hazardRoll = Hash01(coord.X, coord.Z, cellX * 4441 + cellZ * 4481, profile.Seed + 181);
        if (field.HazardPotential > hazardRule.MinPrimary &&
            field.EncounterPotential > hazardRule.MinSecondary &&
            (slope > 0.24f || field.Exposure > 0.46f) &&
            hazardRoll < Mathf.Lerp(hazardRule.ProbabilityLow, hazardRule.ProbabilityHigh, field.HazardPotential))
        {
            float scale = hazardRule.BaseScale + Hash01(coord.X, coord.Z, cellX * 4651 + cellZ * 4721, profile.Seed + 191) * hazardRule.ScaleJitter;
            float rotation = Hash01(coord.X, coord.Z, cellX * 4861 + cellZ * 4931, profile.Seed + 193) * Mathf.Pi * 2.0f;
            Color tint = hazardRule.TintLow.Lerp(hazardRule.TintHigh, Mathf.Clamp(field.Exposure, 0.0f, 1.0f));
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.HazardOutcrop, new Vector3(localX, height, localZ), rotation, scale, tint));
        }
    }
}
