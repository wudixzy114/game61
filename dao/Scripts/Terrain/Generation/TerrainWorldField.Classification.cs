using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldFieldSampler
{
    private static TerrainBiomeKind ClassifyBiome(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float moisture,
        float temperature)
    {
        if (height < profile.SeaLevel - 12.0f)
        {
            return TerrainBiomeKind.Ocean;
        }

        if (height < profile.SeaLevel + 10.0f)
        {
            return TerrainBiomeKind.Coast;
        }

        if (IsLake(height, profile, terms))
        {
            return TerrainBiomeKind.Lake;
        }

        if (IsSnowfield(height, profile, terms, temperature))
        {
            return TerrainBiomeKind.Snowfield;
        }

        if (terms.Mountains > 0.62f)
        {
            return TerrainBiomeKind.Mountains;
        }

        if (terms.Aridity > 0.55f &&
            terms.River > 0.46f &&
            moisture > 0.36f &&
            height < profile.SeaLevel + 320.0f)
        {
            return TerrainBiomeKind.Oasis;
        }

        if (IsDesertLowland(height, profile, terms, moisture, temperature))
        {
            return TerrainBiomeKind.Desert;
        }

        if (terms.Island > 0.54f &&
            terms.Continent < 0.56f &&
            height < profile.SeaLevel + 280.0f)
        {
            return TerrainBiomeKind.Island;
        }

        if (terms.Hills > 0.36f || terms.Mountains > 0.34f)
        {
            return TerrainBiomeKind.Hills;
        }

        if (terms.Wetland > 0.54f)
        {
            return TerrainBiomeKind.Wetland;
        }

        if (terms.Forest > 0.48f && moisture > 0.56f)
        {
            return TerrainBiomeKind.Forest;
        }

        if (terms.Plains > 0.42f && height < profile.SeaLevel + 300.0f)
        {
            return TerrainBiomeKind.Plains;
        }

        return TerrainBiomeKind.Grassland;
    }

    private static TerrainLandscapeKind ClassifyLandscape(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float moisture,
        float temperature,
        float scenicPotential,
        TerrainBiomeKind biome)
    {
        if (height < profile.SeaLevel - 12.0f)
        {
            return TerrainLandscapeKind.Ocean;
        }

        if (height < profile.SeaLevel + 12.0f)
        {
            return TerrainLandscapeKind.Coast;
        }

        if (IsLake(height, profile, terms))
        {
            return TerrainLandscapeKind.Lake;
        }

        if (IsSnowfield(height, profile, terms, temperature))
        {
            return TerrainLandscapeKind.Snowfield;
        }

        if (terms.River > 0.68f && terms.Mountains > 0.34f)
        {
            return TerrainLandscapeKind.Canyon;
        }

        if (biome == TerrainBiomeKind.Oasis || biome == TerrainBiomeKind.Desert)
        {
            return terms.Hills > 0.42f
                ? TerrainLandscapeKind.Highlands
                : TerrainLandscapeKind.Lowland;
        }

        if (terms.River > 0.62f)
        {
            return TerrainLandscapeKind.RiverValley;
        }

        if (terms.Mountains > 0.62f)
        {
            return TerrainLandscapeKind.MountainMassif;
        }

        if (scenicPotential > 0.68f && height > profile.SeaLevel + 180.0f)
        {
            return TerrainLandscapeKind.VistaPlateau;
        }

        if (height > profile.SeaLevel + 360.0f || terms.Mountains > 0.36f)
        {
            return TerrainLandscapeKind.Highlands;
        }

        if (moisture > 0.76f && temperature > 0.34f && height < profile.SeaLevel + 260.0f)
        {
            return TerrainLandscapeKind.Wetland;
        }

        if (moisture > 0.62f && temperature > 0.28f)
        {
            return TerrainLandscapeKind.ForestBasin;
        }

        return TerrainLandscapeKind.Lowland;
    }

    private static bool IsSnowfield(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float temperature)
    {
        float normalizedTemperature = Mathf.Clamp(temperature / 0.46f, 0.0f, 1.0f);
        float climateSnowLine = profile.SeaLevel + profile.HeightScale * Mathf.Lerp(0.30f, 0.55f, normalizedTemperature);
        float alpineLowering = profile.HeightScale * Mathf.Clamp(terms.Alpine * 0.16f + terms.Mountains * 0.10f + terms.Hills * 0.045f, 0.0f, 0.19f);
        float snowLine = climateSnowLine - alpineLowering;
        float minimumElevation = profile.SeaLevel + profile.HeightScale * 0.22f;

        return height > snowLine &&
            height > minimumElevation &&
            (temperature < 0.45f || terms.Alpine > 0.34f || terms.Mountains > 0.28f);
    }

    private static bool IsLake(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms)
    {
        float inland = terms.Continent + terms.Island * 0.18f;
        bool lowlandLake = terms.Lake > 0.34f && terms.Mountains < 0.58f;
        bool alpineTarn = terms.Lake > 0.42f && terms.Mountains is > 0.22f and < 0.70f;
        return (lowlandLake || alpineTarn) &&
            height > profile.SeaLevel + 6.0f &&
            height < profile.SeaLevel + profile.HeightScale * 0.72f &&
            inland > 0.34f;
    }

    private static bool IsDesertLowland(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float moisture,
        float temperature)
    {
        if (height < profile.SeaLevel + 12.0f ||
            height > profile.SeaLevel + 460.0f ||
            terms.Mountains > 0.44f ||
            terms.Wetland > 0.42f)
        {
            return false;
        }

        float warmDryness =
            (1.0f - Mathf.SmoothStep(0.44f, 0.68f, moisture)) *
            Mathf.SmoothStep(0.38f, 0.72f, temperature);
        float aridLowland = terms.Aridity * 0.74f + warmDryness * 0.26f;
        bool coreDesert = terms.Aridity > 0.44f && moisture < 0.62f;
        bool dryPlain = aridLowland > 0.36f && moisture < 0.58f && temperature > 0.34f && terms.Forest < 0.62f;

        return coreDesert || dryPlain;
    }
}
