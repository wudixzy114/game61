using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
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
        TerrainScatterRuleSetSnapshot rules,
        bool placedNaturalScatter,
        List<TerrainScatterInstance> scatter)
    {
        bool isTidalMangroveFlat = IsMangroveTidalFlat(height, slope, field, profile, rules.TidalMangroveFlat);
        bool isLakeScatterZone = IsLakeScatterZone(height, slope, field, profile, rules.LakeScatterZone);
        if (slope > 0.36f || (!isTidalMangroveFlat && !isLakeScatterZone && field.Traversability < 0.18f))
        {
            return;
        }

        float roll = Hash01(coord.X, coord.Z, cellX * 6011 + cellZ * 6073, profile.Seed + 263);
        float densityPenalty = placedNaturalScatter
            ? rules.NaturalDensityPenalty
            : rules.BaseDensityPenalty;
        TerrainScatterKind kind;
        float probability;
        Color tint;
        float baseScale;

        if (isTidalMangroveFlat)
        {
            TerrainScatterVariantRule variant = rules.TidalMangroveRoot;
            float waterline = 1.0f - Mathf.Clamp(Mathf.Abs(height - (profile.SeaLevel + 3.0f)) / 38.0f, 0.0f, 1.0f);
            float riverMouth = Mathf.SmoothStep(0.24f, 0.74f, field.River);
            float suitability = Mathf.Clamp(
                waterline * 0.42f +
                field.Moisture * 0.30f +
                riverMouth * 0.18f +
                Mathf.Clamp(field.Temperature - 0.26f, 0.0f, 1.0f) * 0.20f,
                0.0f,
                1.0f);
            kind = TerrainScatterKind.MangroveRoot;
            probability = Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, suitability) * densityPenalty;
            tint = variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(field.Moisture * 0.34f + riverMouth * 0.12f, 0.0f, 0.42f));
            baseScale = variant.BaseScale;
        }
        else if (isLakeScatterZone)
        {
            float lakeCore = Mathf.SmoothStep(0.34f, 0.72f, field.Lake);
            float lakeMargin = 1.0f - Mathf.Clamp(Mathf.Abs(field.Lake - 0.38f) / 0.32f, 0.0f, 1.0f);
            float warmWater = Mathf.SmoothStep(0.16f, 0.52f, field.Temperature);
            float calmWater = lakeCore * (1.0f - Mathf.SmoothStep(0.10f, 0.22f, slope));
            float shelteredWater = Mathf.Max(calmWater, lakeMargin * warmWater * Mathf.SmoothStep(0.58f, 0.86f, field.Moisture));
            bool placeWaterLily =
                shelteredWater > 0.16f &&
                slope < 0.18f &&
                field.Temperature > 0.18f &&
                Hash01(coord.X, coord.Z, cellX * 6239 + cellZ * 6263, profile.Seed + 279) < Mathf.Lerp(0.24f, 0.62f, shelteredWater * warmWater);
            TerrainScatterVariantRule variant = placeWaterLily ? rules.LakeWaterLily : rules.LakeReed;
            kind = placeWaterLily ? TerrainScatterKind.WaterLily : TerrainScatterKind.LakeReed;
            probability = (placeWaterLily
                ? Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, shelteredWater * (0.52f + warmWater * 0.48f))
                : Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, Mathf.Max(lakeMargin, field.ResourcePotential * 0.72f))) * densityPenalty;
            tint = placeWaterLily
                ? variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(warmWater * 0.32f, 0.0f, 0.32f))
                : variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(lakeMargin * 0.26f, 0.0f, 0.26f));
            baseScale = variant.BaseScale;
        }
        else if (field.BiomeKind is TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland && slope < 0.20f && field.Moisture is > 0.28f and < 0.72f)
        {
            TerrainScatterVariantRule variant = rules.GrassTuft;
            kind = TerrainScatterKind.GrassTuft;
            probability = Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, Mathf.Clamp(field.ResourcePotential, 0.0f, 1.0f)) * densityPenalty;
            tint = variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(field.Temperature * 0.45f, 0.0f, 0.45f));
            baseScale = variant.BaseScale;
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
            TerrainScatterVariantRule variant = placeMangrove
                ? rules.CoastalMangroveRoot
                : placePalm
                ? rules.CoastalPalm
                : rules.Driftwood;
            kind = placeMangrove
                ? TerrainScatterKind.MangroveRoot
                : placePalm
                ? TerrainScatterKind.CoastalPalm
                : TerrainScatterKind.Driftwood;
            probability = (kind == TerrainScatterKind.MangroveRoot
                ? Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, mangroveSuitability)
                : placePalm
                ? Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, palmSuitability)
                : Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, shoreline)) * densityPenalty;
            tint = kind == TerrainScatterKind.MangroveRoot
                ? variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(field.Moisture * 0.28f, 0.0f, 0.28f))
                : placePalm
                ? variant.TintLow.Lerp(variant.TintHigh, 0.18f)
                : variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(shoreline * 0.30f, 0.0f, 0.30f));
            baseScale = variant.BaseScale;
        }
        else if (field.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis && slope < 0.24f)
        {
            float dryness = Mathf.Clamp(1.0f - field.Moisture, 0.0f, 1.0f);
            bool placeCactus = field.BiomeKind == TerrainBiomeKind.Desert &&
                field.Temperature > 0.38f &&
                dryness > 0.44f &&
                Hash01(coord.X, coord.Z, cellX * 6301 + cellZ * 6311, profile.Seed + 281) < Mathf.Lerp(0.18f, 0.54f, dryness);
            TerrainScatterVariantRule variant = field.BiomeKind == TerrainBiomeKind.Oasis && field.Moisture > 0.42f
                ? rules.OasisReed
                : placeCactus
                ? rules.DesertCactus
                : rules.DesertShrub;
            kind = field.BiomeKind == TerrainBiomeKind.Oasis && field.Moisture > 0.42f
                ? TerrainScatterKind.ReedCluster
                : placeCactus
                ? TerrainScatterKind.CactusCluster
                : TerrainScatterKind.DesertShrub;
            probability = (kind == TerrainScatterKind.CactusCluster
                ? Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, dryness)
                : Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, dryness)) * densityPenalty;
            tint = kind == TerrainScatterKind.ReedCluster
                ? variant.TintLow.Lerp(variant.TintHigh, 0.20f)
                : kind == TerrainScatterKind.CactusCluster
                ? variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(field.ResourcePotential * 0.20f, 0.0f, 0.20f))
                : variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(field.Temperature * 0.26f, 0.0f, 0.26f));
            baseScale = variant.BaseScale;
        }
        else if (field.BiomeKind == TerrainBiomeKind.Wetland && slope < 0.18f && field.Moisture > 0.62f)
        {
            bool placeMangrove = height < profile.SeaLevel + 92.0f &&
                field.Temperature > 0.28f &&
                Hash01(coord.X, coord.Z, cellX * 6323 + cellZ * 6337, profile.Seed + 283) < Mathf.Lerp(0.16f, 0.46f, field.Moisture);
            TerrainScatterVariantRule variant = placeMangrove ? rules.WetlandMangroveRoot : rules.WetlandReed;
            kind = placeMangrove ? TerrainScatterKind.MangroveRoot : TerrainScatterKind.ReedCluster;
            probability = (placeMangrove
                ? Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, field.Moisture)
                : Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, field.Moisture)) * densityPenalty;
            tint = placeMangrove
                ? variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(field.River * 0.24f, 0.0f, 0.24f))
                : variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(field.River * 0.24f, 0.0f, 0.24f));
            baseScale = variant.BaseScale;
        }
        else if (field.BiomeKind == TerrainBiomeKind.Snowfield && slope < 0.32f)
        {
            bool placeAlpinePine = field.Moisture > 0.26f &&
                field.Exposure < 0.76f &&
                Hash01(coord.X, coord.Z, cellX * 6353 + cellZ * 6361, profile.Seed + 287) < 0.34f;
            TerrainScatterVariantRule variant = placeAlpinePine ? rules.SnowfieldAlpinePine : rules.SnowClump;
            kind = placeAlpinePine ? TerrainScatterKind.AlpinePine : TerrainScatterKind.SnowClump;
            probability = (placeAlpinePine
                ? Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, Mathf.Clamp(field.Moisture, 0.0f, 1.0f))
                : Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, Mathf.Clamp(field.Exposure, 0.0f, 1.0f))) * densityPenalty;
            tint = placeAlpinePine
                ? variant.TintLow.Lerp(variant.TintHigh, 0.20f)
                : variant.TintLow.Lerp(variant.TintHigh, 0.22f);
            baseScale = variant.BaseScale;
        }
        else if (field.BiomeKind is TerrainBiomeKind.Hills or TerrainBiomeKind.Mountains &&
            slope < 0.30f &&
            field.Temperature < 0.42f &&
            field.Moisture > 0.32f)
        {
            TerrainScatterVariantRule variant = rules.MountainAlpinePine;
            kind = TerrainScatterKind.AlpinePine;
            probability = Mathf.Lerp(variant.ProbabilityLow, variant.ProbabilityHigh, Mathf.Clamp(field.Moisture + field.ScenicPotential * 0.35f, 0.0f, 1.0f)) * densityPenalty;
            tint = variant.TintLow.Lerp(variant.TintHigh, Mathf.Clamp(field.Exposure * 0.20f, 0.0f, 0.20f));
            baseScale = variant.BaseScale;
        }
        else
        {
            return;
        }

        if (roll > probability)
        {
            return;
        }

        TerrainScatterVariantRule scaleVariant = kind switch
        {
            TerrainScatterKind.MangroveRoot when isTidalMangroveFlat => rules.TidalMangroveRoot,
            TerrainScatterKind.WaterLily => rules.LakeWaterLily,
            TerrainScatterKind.LakeReed when isLakeScatterZone => rules.LakeReed,
            TerrainScatterKind.GrassTuft => rules.GrassTuft,
            TerrainScatterKind.MangroveRoot when field.BiomeKind == TerrainBiomeKind.Wetland => rules.WetlandMangroveRoot,
            TerrainScatterKind.MangroveRoot => rules.CoastalMangroveRoot,
            TerrainScatterKind.CoastalPalm => rules.CoastalPalm,
            TerrainScatterKind.Driftwood => rules.Driftwood,
            TerrainScatterKind.ReedCluster when field.BiomeKind == TerrainBiomeKind.Wetland => rules.WetlandReed,
            TerrainScatterKind.ReedCluster => rules.OasisReed,
            TerrainScatterKind.CactusCluster => rules.DesertCactus,
            TerrainScatterKind.DesertShrub => rules.DesertShrub,
            TerrainScatterKind.SnowClump => rules.SnowClump,
            TerrainScatterKind.AlpinePine when field.BiomeKind == TerrainBiomeKind.Snowfield => rules.SnowfieldAlpinePine,
            TerrainScatterKind.AlpinePine => rules.MountainAlpinePine,
            _ => rules.GrassTuft
        };
        float scale = baseScale + Hash01(coord.X, coord.Z, cellX * 6113 + cellZ * 6151, profile.Seed + 269) * baseScale * scaleVariant.ScaleJitterFactor;
        float rotation = Hash01(coord.X, coord.Z, cellX * 6173 + cellZ * 6197, profile.Seed + 271) * Mathf.Pi * 2.0f;
        scatter.Add(new TerrainScatterInstance(kind, new Vector3(localX, height, localZ), rotation, scale, tint));
    }

    private static bool IsMangroveTidalFlat(
        float height,
        float slope,
        TerrainWorldField field,
        TerrainGenerationProfile profile,
        TerrainWaterZoneScatterRule rules)
    {
        if (slope > rules.MaxSlope ||
            height < profile.SeaLevel + rules.MinHeightOffset ||
            height > profile.SeaLevel + rules.MaxHeightValue ||
            field.Moisture < rules.MinPrimary ||
            field.Temperature < rules.MinSecondary)
        {
            return false;
        }

        bool coastalOrWetland =
            field.BiomeKind is TerrainBiomeKind.Coast or TerrainBiomeKind.Island or TerrainBiomeKind.Wetland ||
            field.LandscapeKind is TerrainLandscapeKind.Coast or TerrainLandscapeKind.Wetland or TerrainLandscapeKind.RiverValley;
        bool hasWaterSource = field.River > rules.Threshold || height < profile.SeaLevel + rules.ShorelineHeightOffset;
        return coastalOrWetland && hasWaterSource;
    }

    private static bool IsLakeScatterZone(
        float height,
        float slope,
        TerrainWorldField field,
        TerrainGenerationProfile profile,
        TerrainWaterZoneScatterRule rules)
    {
        if (slope > rules.MaxSlope ||
            height < profile.SeaLevel + rules.MinHeightOffset ||
            height > profile.SeaLevel + profile.HeightScale * rules.MaxHeightValue ||
            field.Lake < rules.MinPrimary)
        {
            return false;
        }

        return field.BiomeKind == TerrainBiomeKind.Lake ||
            field.LandscapeKind == TerrainLandscapeKind.Lake ||
            (field.Moisture > rules.MinSecondary && field.ResourcePotential > rules.Threshold);
    }
}
