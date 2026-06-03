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
        List<TerrainScatterInstance> scatter)
    {
        float understoryRoll = Hash01(coord.X, coord.Z, cellX * 2711 + cellZ * 2797, profile.Seed + 149);
        if (slope < 0.22f &&
            field.ResourcePotential > 0.42f &&
            field.Moisture > 0.50f &&
            field.Temperature > 0.24f &&
            field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Wetland or TerrainLandscapeKind.RiverValley &&
            understoryRoll < Mathf.Lerp(0.08f, 0.46f, field.ResourcePotential))
        {
            float scale = 0.55f + Hash01(coord.X, coord.Z, cellX * 3253 + cellZ * 3307, profile.Seed + 151) * 0.95f;
            float rotation = Hash01(coord.X, coord.Z, cellX * 3533 + cellZ * 3581, profile.Seed + 157) * Mathf.Pi * 2.0f;
            Color tint = new Color(0.18f, 0.36f, 0.16f).Lerp(new Color(0.34f, 0.50f, 0.22f), field.ResourcePotential);
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Understory, new Vector3(localX, height, localZ), rotation, scale, tint));
        }

        float resourceRoll = Hash01(coord.X, coord.Z, cellX * 3761 + cellZ * 3851, profile.Seed + 163);
        if (field.ResourcePotential > 0.62f &&
            field.Traversability > 0.34f &&
            slope < 0.30f &&
            resourceRoll < Mathf.Lerp(0.04f, 0.24f, field.ResourcePotential))
        {
            float scale = 0.95f + Hash01(coord.X, coord.Z, cellX * 4001 + cellZ * 4027, profile.Seed + 167) * 1.45f;
            float rotation = Hash01(coord.X, coord.Z, cellX * 4211 + cellZ * 4241, profile.Seed + 173) * Mathf.Pi * 2.0f;
            Color tint = new Color(0.28f, 0.48f, 0.22f).Lerp(new Color(0.62f, 0.54f, 0.30f), Mathf.Clamp(field.ResourcePotential, 0.0f, 1.0f));
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.ResourceNode, new Vector3(localX, height, localZ), rotation, scale, tint));
        }

        float hazardRoll = Hash01(coord.X, coord.Z, cellX * 4441 + cellZ * 4481, profile.Seed + 181);
        if (field.HazardPotential > 0.48f &&
            field.EncounterPotential > 0.40f &&
            (slope > 0.24f || field.Exposure > 0.46f) &&
            hazardRoll < Mathf.Lerp(0.05f, 0.30f, field.HazardPotential))
        {
            float scale = 0.85f + Hash01(coord.X, coord.Z, cellX * 4651 + cellZ * 4721, profile.Seed + 191) * 1.80f;
            float rotation = Hash01(coord.X, coord.Z, cellX * 4861 + cellZ * 4931, profile.Seed + 193) * Mathf.Pi * 2.0f;
            Color tint = new Color(0.38f, 0.30f, 0.27f).Lerp(new Color(0.64f, 0.58f, 0.50f), Mathf.Clamp(field.Exposure, 0.0f, 1.0f));
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.HazardOutcrop, new Vector3(localX, height, localZ), rotation, scale, tint));
        }
    }

    private static void AddBiomeSurfaceScatter(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int cellX,
        int cellZ,
        float localX,
        float localZ,
        float height,
        float slope,
        TerrainWorldField field,
        bool placedNaturalScatter,
        List<TerrainScatterInstance> scatter)
    {
        if (slope > 0.36f || field.Traversability < 0.18f)
        {
            return;
        }

        float roll = Hash01(coord.X, coord.Z, cellX * 6011 + cellZ * 6073, profile.Seed + 263);
        float densityPenalty = placedNaturalScatter ? 0.42f : 1.0f;
        TerrainScatterKind kind;
        float probability;
        Color tint;
        float baseScale;

        if (field.BiomeKind is TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland && slope < 0.20f && field.Moisture is > 0.28f and < 0.72f)
        {
            kind = TerrainScatterKind.GrassTuft;
            probability = Mathf.Lerp(0.10f, 0.32f, Mathf.Clamp(field.ResourcePotential, 0.0f, 1.0f)) * densityPenalty;
            tint = new Color(0.34f, 0.46f, 0.20f).Lerp(new Color(0.55f, 0.50f, 0.24f), Mathf.Clamp(field.Temperature * 0.45f, 0.0f, 0.45f));
            baseScale = 0.52f;
        }
        else if (field.BiomeKind is TerrainBiomeKind.Coast or TerrainBiomeKind.Island && slope < 0.22f && height < profile.SeaLevel + 130.0f)
        {
            float shoreline = 1.0f - Mathf.Clamp(Mathf.Abs(height - (profile.SeaLevel + 18.0f)) / 96.0f, 0.0f, 1.0f);
            float palmSuitability = Mathf.Clamp(
                (field.Temperature - 0.24f) * 1.20f +
                field.Moisture * 0.34f +
                (field.BiomeKind == TerrainBiomeKind.Island ? 0.24f : 0.0f),
                0.0f,
                1.0f);
            float mangroveSuitability = Mathf.Clamp(
                shoreline * 0.46f +
                field.Moisture * 0.34f +
                field.River * 0.24f +
                Mathf.Clamp(field.Temperature - 0.22f, 0.0f, 1.0f) * 0.18f,
                0.0f,
                1.0f);
            bool placeMangrove = field.Moisture > 0.58f &&
                Hash01(coord.X, coord.Z, cellX * 6203 + cellZ * 6217, profile.Seed + 275) < mangroveSuitability * 0.44f;
            bool placePalm = Hash01(coord.X, coord.Z, cellX * 6221 + cellZ * 6257, profile.Seed + 277) < palmSuitability * 0.58f;
            kind = placeMangrove
                ? TerrainScatterKind.MangroveRoot
                : placePalm
                ? TerrainScatterKind.CoastalPalm
                : TerrainScatterKind.Driftwood;
            probability = (kind == TerrainScatterKind.MangroveRoot
                ? Mathf.Lerp(0.08f, 0.22f, mangroveSuitability)
                : placePalm
                ? Mathf.Lerp(0.07f, 0.24f, palmSuitability)
                : Mathf.Lerp(0.08f, 0.26f, shoreline)) * densityPenalty;
            tint = kind == TerrainScatterKind.MangroveRoot
                ? new Color(0.22f, 0.28f, 0.16f).Lerp(new Color(0.36f, 0.42f, 0.22f), Mathf.Clamp(field.Moisture * 0.28f, 0.0f, 0.28f))
                : placePalm
                ? new Color(0.16f, 0.42f, 0.23f).Lerp(new Color(0.48f, 0.40f, 0.20f), 0.18f)
                : new Color(0.46f, 0.36f, 0.24f).Lerp(new Color(0.66f, 0.58f, 0.42f), Mathf.Clamp(shoreline * 0.30f, 0.0f, 0.30f));
            baseScale = kind == TerrainScatterKind.MangroveRoot ? 0.82f : placePalm ? 1.10f : 0.70f;
        }
        else if (field.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis && slope < 0.24f)
        {
            float dryness = Mathf.Clamp(1.0f - field.Moisture, 0.0f, 1.0f);
            bool placeCactus = field.BiomeKind == TerrainBiomeKind.Desert &&
                field.Temperature > 0.38f &&
                dryness > 0.44f &&
                Hash01(coord.X, coord.Z, cellX * 6301 + cellZ * 6311, profile.Seed + 281) < Mathf.Lerp(0.18f, 0.54f, dryness);
            kind = field.BiomeKind == TerrainBiomeKind.Oasis && field.Moisture > 0.42f
                ? TerrainScatterKind.ReedCluster
                : placeCactus
                ? TerrainScatterKind.CactusCluster
                : TerrainScatterKind.DesertShrub;
            probability = (kind == TerrainScatterKind.CactusCluster
                ? Mathf.Lerp(0.06f, 0.18f, dryness)
                : Mathf.Lerp(0.08f, 0.30f, dryness)) * densityPenalty;
            tint = kind == TerrainScatterKind.ReedCluster
                ? new Color(0.22f, 0.48f, 0.30f).Lerp(new Color(0.52f, 0.46f, 0.24f), 0.20f)
                : kind == TerrainScatterKind.CactusCluster
                ? new Color(0.20f, 0.36f, 0.22f).Lerp(new Color(0.44f, 0.50f, 0.26f), Mathf.Clamp(field.ResourcePotential * 0.20f, 0.0f, 0.20f))
                : new Color(0.46f, 0.38f, 0.20f).Lerp(new Color(0.70f, 0.56f, 0.30f), Mathf.Clamp(field.Temperature * 0.26f, 0.0f, 0.26f));
            baseScale = kind == TerrainScatterKind.ReedCluster ? 0.62f : kind == TerrainScatterKind.CactusCluster ? 0.88f : 0.56f;
        }
        else if (field.BiomeKind == TerrainBiomeKind.Wetland && slope < 0.18f && field.Moisture > 0.62f)
        {
            bool placeMangrove = height < profile.SeaLevel + 92.0f &&
                field.Temperature > 0.28f &&
                Hash01(coord.X, coord.Z, cellX * 6323 + cellZ * 6337, profile.Seed + 283) < Mathf.Lerp(0.16f, 0.46f, field.Moisture);
            kind = placeMangrove ? TerrainScatterKind.MangroveRoot : TerrainScatterKind.ReedCluster;
            probability = (placeMangrove
                ? Mathf.Lerp(0.08f, 0.24f, field.Moisture)
                : Mathf.Lerp(0.16f, 0.42f, field.Moisture)) * densityPenalty;
            tint = placeMangrove
                ? new Color(0.20f, 0.27f, 0.15f).Lerp(new Color(0.36f, 0.42f, 0.22f), Mathf.Clamp(field.River * 0.24f, 0.0f, 0.24f))
                : new Color(0.20f, 0.40f, 0.24f).Lerp(new Color(0.48f, 0.42f, 0.22f), Mathf.Clamp(field.River * 0.24f, 0.0f, 0.24f));
            baseScale = placeMangrove ? 0.82f : 0.70f;
        }
        else if (field.BiomeKind == TerrainBiomeKind.Snowfield && slope < 0.32f)
        {
            bool placeAlpinePine = field.Moisture > 0.26f &&
                field.Exposure < 0.76f &&
                Hash01(coord.X, coord.Z, cellX * 6353 + cellZ * 6361, profile.Seed + 287) < 0.34f;
            kind = placeAlpinePine ? TerrainScatterKind.AlpinePine : TerrainScatterKind.SnowClump;
            probability = (placeAlpinePine
                ? Mathf.Lerp(0.05f, 0.18f, Mathf.Clamp(field.Moisture, 0.0f, 1.0f))
                : Mathf.Lerp(0.10f, 0.34f, Mathf.Clamp(field.Exposure, 0.0f, 1.0f))) * densityPenalty;
            tint = placeAlpinePine
                ? new Color(0.10f, 0.26f, 0.18f).Lerp(new Color(0.62f, 0.72f, 0.70f), 0.20f)
                : new Color(0.74f, 0.80f, 0.82f).Lerp(Colors.White, 0.22f);
            baseScale = placeAlpinePine ? 0.94f : 0.72f;
        }
        else if (field.BiomeKind is TerrainBiomeKind.Hills or TerrainBiomeKind.Mountains &&
            slope < 0.30f &&
            field.Temperature < 0.42f &&
            field.Moisture > 0.32f)
        {
            kind = TerrainScatterKind.AlpinePine;
            probability = Mathf.Lerp(0.04f, 0.16f, Mathf.Clamp(field.Moisture + field.ScenicPotential * 0.35f, 0.0f, 1.0f)) * densityPenalty;
            tint = new Color(0.10f, 0.24f, 0.17f).Lerp(new Color(0.36f, 0.42f, 0.32f), Mathf.Clamp(field.Exposure * 0.20f, 0.0f, 0.20f));
            baseScale = 1.00f;
        }
        else
        {
            return;
        }

        if (roll > probability)
        {
            return;
        }

        float scale = baseScale + Hash01(coord.X, coord.Z, cellX * 6113 + cellZ * 6151, profile.Seed + 269) * baseScale * 0.86f;
        float rotation = Hash01(coord.X, coord.Z, cellX * 6173 + cellZ * 6197, profile.Seed + 271) * Mathf.Pi * 2.0f;
        scatter.Add(new TerrainScatterInstance(kind, new Vector3(localX, height, localZ), rotation, scale, tint));
    }
}
