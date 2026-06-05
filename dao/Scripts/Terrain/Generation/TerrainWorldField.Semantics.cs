using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldFieldSampler
{
    private static TerrainWorldField BuildField(
        Vector2 world,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float height)
    {
        float moisture = Mathf.Clamp(
            terms.BaseMoisture + terms.River * 0.45f + terms.Lake * 0.52f - terms.Aridity * 0.22f + terms.Wetland * 0.16f,
            0.0f,
            1.0f);
        float temperature = Mathf.Clamp(
            terms.BaseTemperature -
            Mathf.Max(0.0f, height) / (profile.HeightScale * 1.7f) -
            terms.Alpine * 0.08f,
            0.0f,
            1.0f);
        float scenicPotential = ComputeScenicPotential(height, profile, terms, moisture, temperature);
        float traversability = ComputeTraversability(height, profile, terms);
        float exposure = ComputeExposure(height, profile, terms, scenicPotential);
        float resourcePotential = ComputeResourcePotential(height, profile, terms, moisture, temperature, traversability);
        float hazardPotential = ComputeHazardPotential(height, profile, terms, temperature, traversability, exposure);
        float encounterPotential = ComputeEncounterPotential(scenicPotential, traversability, exposure, resourcePotential, hazardPotential);
        TerrainBiomeKind biome = ClassifyBiome(height, profile, terms, moisture, temperature);
        TerrainLandscapeKind landscape = ClassifyLandscape(height, profile, terms, moisture, temperature, scenicPotential, biome);

        return new TerrainWorldField(
            world,
            height,
            terms.Continent,
            terms.Basin,
            terms.Shelf,
            terms.Mountains,
            terms.BroadElevation,
            terms.River,
            terms.Lake,
            moisture,
            temperature,
            scenicPotential,
            traversability,
            exposure,
            resourcePotential,
            hazardPotential,
            encounterPotential,
            biome,
            landscape);
    }

    private static float ComputeScenicPotential(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float moisture,
        float temperature)
    {
        float elevationScore = Mathf.SmoothStep(profile.SeaLevel + 48.0f, profile.SeaLevel + profile.HeightScale * 0.46f, height);
        float ridgeScore = Mathf.SmoothStep(0.10f, 0.34f, terms.Mountains);
        float riverContrast = Mathf.SmoothStep(0.20f, 0.58f, terms.River) *
            Mathf.SmoothStep(profile.SeaLevel + 18.0f, profile.SeaLevel + profile.HeightScale * 0.34f, height);
        float highlandScore = Mathf.SmoothStep(0.30f, 0.62f, terms.Shelf * terms.BroadElevation);
        float biomeContrast = Mathf.Clamp(Mathf.Abs(moisture - temperature) * 1.35f, 0.0f, 1.0f);
        float coastDrama = Mathf.Clamp(1.0f - Mathf.Abs(height - profile.SeaLevel - 22.0f) / 180.0f, 0.0f, 1.0f) *
            Mathf.Clamp(terms.Continent * 1.5f, 0.0f, 1.0f);
        float desertVista = terms.Aridity *
            Mathf.SmoothStep(profile.SeaLevel + 34.0f, profile.SeaLevel + 260.0f, height) *
            (1.0f - Mathf.SmoothStep(0.34f, 0.64f, terms.Mountains));
        float islandVista = terms.Island *
            Mathf.Clamp(1.0f - Mathf.Abs(height - profile.SeaLevel - 58.0f) / 260.0f, 0.0f, 1.0f);
        float lakeVista = terms.Lake *
            Mathf.SmoothStep(profile.SeaLevel + 18.0f, profile.SeaLevel + profile.HeightScale * 0.32f, height) *
            (1.0f - Mathf.SmoothStep(profile.SeaLevel + profile.HeightScale * 0.76f, profile.SeaLevel + profile.HeightScale * 0.96f, height));

        float dominantVista = Mathf.Max(
            Mathf.Max(ridgeScore * 0.92f, riverContrast * 0.86f),
            Mathf.Max(Mathf.Max(coastDrama * 0.74f, highlandScore * 0.72f), Mathf.Max(Mathf.Max(desertVista * 0.54f, islandVista * 0.64f), lakeVista * 0.72f)));

        float blendedVista =
            ridgeScore * 0.30f +
            elevationScore * 0.18f +
            riverContrast * 0.22f +
            highlandScore * 0.14f +
            coastDrama * 0.10f +
            biomeContrast * 0.06f +
            desertVista * 0.06f +
            islandVista * 0.05f +
            lakeVista * 0.08f;

        return Mathf.Clamp(
            Mathf.Max(dominantVista, blendedVista) * (0.94f + profile.VistaFrequency * 0.12f),
            0.0f,
            1.0f);
    }

    private static float ComputeTraversability(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms)
    {
        float land = Mathf.SmoothStep(profile.SeaLevel + 3.0f, profile.SeaLevel + 38.0f, height);
        float lowlandBonus = Mathf.Clamp(terms.Plains * 0.18f + terms.Aridity * 0.10f + terms.Wetland * 0.04f, 0.0f, 0.24f);
        float ruggedPenalty = Mathf.Clamp(terms.Mountains * 1.45f - lowlandBonus, 0.0f, 0.82f);
        float riverPenalty = terms.River * 0.24f;
        float lakePenalty = terms.Lake * 0.76f;
        return Mathf.Clamp(land * (1.0f - ruggedPenalty) * (1.0f - riverPenalty) * (1.0f - lakePenalty), 0.0f, 1.0f);
    }

    private static float ComputeExposure(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float scenicPotential)
    {
        float elevation = Mathf.SmoothStep(profile.SeaLevel + 140.0f, profile.SeaLevel + profile.HeightScale * 0.86f, height);
        float ridge = Mathf.SmoothStep(0.20f, 0.64f, terms.Mountains);
        float plateau = Mathf.SmoothStep(0.34f, 0.70f, terms.Shelf * terms.BroadElevation);
        float coastal = Mathf.Clamp(1.0f - Mathf.Abs(height - profile.SeaLevel - 18.0f) / 210.0f, 0.0f, 1.0f);
        float lakeShore = terms.Lake *
            Mathf.SmoothStep(profile.SeaLevel + 10.0f, profile.SeaLevel + profile.HeightScale * 0.36f, height);

        return Mathf.Clamp(
            Mathf.Max(elevation * 0.58f, ridge * 0.70f) +
            plateau * 0.16f +
            scenicPotential * 0.18f +
            coastal * 0.08f +
            lakeShore * 0.06f,
            0.0f,
            1.0f);
    }

    private static float ComputeResourcePotential(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float moisture,
        float temperature,
        float traversability)
    {
        float land = Mathf.SmoothStep(profile.SeaLevel + 8.0f, profile.SeaLevel + 58.0f, height);
        float waterAccess = Mathf.Max(Mathf.SmoothStep(0.18f, 0.66f, terms.River), Mathf.SmoothStep(0.32f, 0.72f, terms.Lake));
        float climate = Mathf.Clamp(1.0f - Mathf.Abs(temperature - 0.54f) * 1.75f, 0.0f, 1.0f);
        float lowElevation = 1.0f - Mathf.SmoothStep(profile.SeaLevel + 320.0f, profile.SeaLevel + profile.HeightScale * 0.92f, height);
        float oasis = terms.Aridity * Mathf.SmoothStep(0.38f, 0.78f, terms.River + moisture * 0.24f);
        float arableLowland = Mathf.Clamp(terms.Plains * 0.12f + terms.Wetland * 0.16f + terms.Lake * 0.10f + oasis * 0.24f, 0.0f, 0.34f);
        float soil = Mathf.Clamp(
            moisture * 0.52f +
            climate * 0.22f +
            lowElevation * 0.18f +
            waterAccess * 0.08f +
            arableLowland -
            terms.Aridity * 0.16f,
            0.0f,
            1.0f);

        return Mathf.Clamp(land * (soil * 0.72f + traversability * 0.28f), 0.0f, 1.0f);
    }

    private static float ComputeHazardPotential(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float temperature,
        float traversability,
        float exposure)
    {
        float waterDepth = Mathf.Clamp((profile.SeaLevel - height) / Mathf.Max(1.0f, profile.HeightScale * 0.38f), 0.0f, 1.0f);
        float rugged = Mathf.SmoothStep(0.05f, 0.32f, terms.Mountains);
        float canyon = terms.River * Mathf.SmoothStep(0.05f, 0.30f, terms.Mountains);
        float riverRisk = Mathf.SmoothStep(0.66f, 0.92f, terms.River) *
            Mathf.SmoothStep(profile.SeaLevel + 8.0f, profile.SeaLevel + profile.HeightScale * 0.48f, height);
        float lakeRisk = Mathf.SmoothStep(0.46f, 0.86f, terms.Lake) *
            Mathf.SmoothStep(profile.SeaLevel + 8.0f, profile.SeaLevel + profile.HeightScale * 0.42f, height);
        float highElevation = Mathf.SmoothStep(profile.SeaLevel + 260.0f, profile.SeaLevel + profile.HeightScale * 0.92f, height);
        float exposedRidge = Mathf.SmoothStep(0.16f, 0.52f, exposure);
        float snow = temperature < 0.22f
            ? Mathf.SmoothStep(profile.SeaLevel + 280.0f, profile.SeaLevel + profile.HeightScale * 0.92f, height)
            : 0.0f;
        float isolation = 1.0f - traversability;
        float heatRisk = terms.Aridity * Mathf.SmoothStep(0.64f, 0.90f, temperature);
        float desertExposure = heatRisk *
            (0.58f + terms.DuneDetail * 0.42f) *
            (1.0f - Mathf.SmoothStep(0.36f, 0.66f, terms.Mountains));
        float floodRisk = terms.Wetland *
            Mathf.SmoothStep(0.46f, 0.86f, terms.River + terms.BaseMoisture * 0.32f) *
            (1.0f - Mathf.SmoothStep(profile.SeaLevel + 180.0f, profile.SeaLevel + 420.0f, height));
        float islandIsolation = terms.Island *
            (1.0f - Mathf.SmoothStep(0.32f, 0.58f, terms.Continent)) *
            Mathf.SmoothStep(profile.SeaLevel + 8.0f, profile.SeaLevel + 220.0f, height);
        float coastalStorm = Mathf.Clamp(1.0f - Mathf.Abs(height - profile.SeaLevel - 16.0f) / 150.0f, 0.0f, 1.0f) *
            Mathf.SmoothStep(0.26f, 0.68f, terms.Continent + terms.Island * 0.28f);
        float frontierWildland =
            Mathf.SmoothStep(0.42f, 0.78f, terms.Plains + terms.Forest * 0.70f + terms.Wetland * 0.85f + terms.River * 0.22f) *
            Mathf.SmoothStep(0.34f, 0.72f, terms.BaseMoisture + terms.River * 0.30f) *
            Mathf.SmoothStep(profile.SeaLevel + 12.0f, profile.SeaLevel + 380.0f, height) *
            (1.0f - Mathf.SmoothStep(0.46f, 0.76f, terms.Mountains));

        return Mathf.Clamp(
            Mathf.Max(
            Mathf.Max(Mathf.Max(rugged * 0.74f, canyon * 0.82f), Mathf.Max(riverRisk * 0.50f, lakeRisk * 0.42f)),
                Mathf.Max(
                    Mathf.Max(desertExposure * 0.64f, floodRisk * 0.62f),
                    Mathf.Max(coastalStorm * 0.46f, frontierWildland * 0.48f))) +
            waterDepth * 0.12f +
            highElevation * 0.16f +
            exposedRidge * 0.24f +
            snow * 0.08f +
            isolation * 0.16f +
            heatRisk * 0.28f +
            floodRisk * 0.18f +
            lakeRisk * 0.16f +
            islandIsolation * 0.20f +
            coastalStorm * 0.10f +
            frontierWildland * 0.12f,
            0.0f,
            1.0f);
    }

    private static float ComputeEncounterPotential(
        float scenicPotential,
        float traversability,
        float exposure,
        float resourcePotential,
        float hazardPotential)
    {
        float riskReward = Mathf.Min(resourcePotential, hazardPotential) * 0.22f;
        return Mathf.Clamp(
            scenicPotential * 0.24f +
            traversability * 0.20f +
            resourcePotential * 0.22f +
            hazardPotential * 0.18f +
            exposure * 0.16f +
            riskReward,
            0.0f,
            1.0f);
    }
}
