using System.Collections.Concurrent;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Broad landscape form classification for a terrain position.</summary>
public enum TerrainLandscapeKind
{
    Ocean,
    Coast,
    Lowland,
    Wetland,
    ForestBasin,
    RiverValley,
    Canyon,
    Highlands,
    MountainMassif,
    Snowfield,
    VistaPlateau
}

/// <summary>Climate-based biome classification for a terrain position.</summary>
public enum TerrainBiomeKind
{
    Ocean,
    Coast,
    Island,
    Plains,
    Grassland,
    Desert,
    Oasis,
    Forest,
    Wetland,
    Hills,
    Mountains,
    Snowfield
}

/// <summary>Complete set of derived terrain attributes sampled at a world position.</summary>
public readonly record struct TerrainWorldField(
    Vector2 WorldPosition,
    float Height,
    float Continent,
    float Basin,
    float Shelf,
    float Mountains,
    float BroadElevation,
    float River,
    float Moisture,
    float Temperature,
    float ScenicPotential,
    float Traversability,
    float Exposure,
    float ResourcePotential,
    float HazardPotential,
    float EncounterPotential,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind);

/// <summary>Primary sampler that computes all terrain field values (height, climate, resources, etc.) at any world coordinate.</summary>
public static class TerrainWorldFieldSampler
{
    public const int NativeFieldGridStride = 17;

    private static readonly ConcurrentDictionary<TerrainGenerationProfile, float> LandBalanceOffsets = new();

    /// <summary>Full terrain sample at a world position, computing all field attributes including height.</summary>
    public static TerrainWorldField Sample(Vector2 world, TerrainGenerationProfile profile)
    {
        TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: true);
        float height = BuildHeight(terms, profile);
        return BuildField(world, profile, terms, height);
    }

    /// <summary>Samples terrain fields using a precomputed height, skipping micro-detail for efficiency.</summary>
    public static TerrainWorldField SampleKnownHeight(Vector2 world, TerrainGenerationProfile profile, float height)
    {
        TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: false);
        return BuildField(world, profile, terms, height);
    }

    /// <summary>Deserializes a <see cref="TerrainWorldField"/> from a native sampler's flat float array at the given index.</summary>
    public static TerrainWorldField SampleNativeFieldGrid(Vector2 world, TerrainGenerationProfile profile, float[] samples, int sampleIndex)
    {
        int offset = sampleIndex * NativeFieldGridStride;
        float height = samples[offset];
        TerrainShapeTerms terms = new(
            samples[offset + 1],
            samples[offset + 2],
            samples[offset + 3],
            samples[offset + 4],
            samples[offset + 5],
            samples[offset + 6],
            0.0f,
            samples[offset + 7],
            samples[offset + 8],
            samples[offset + 9],
            samples[offset + 10],
            samples[offset + 11],
            samples[offset + 12],
            samples[offset + 13],
            samples[offset + 14],
            samples[offset + 15],
            samples[offset + 16]);
        return BuildField(world, profile, terms, height);
    }

    /// <summary>Returns the cached land-balance height offset for the given profile (computes if needed).</summary>
    public static float LandBalanceOffsetFor(TerrainGenerationProfile profile)
    {
        return GetLandBalanceOffset(profile);
    }

    private static TerrainShapeTerms SampleShapeTerms(
        Vector2 world,
        TerrainGenerationProfile profile,
        bool includeMicroDetail)
    {
        Vector2 warped = ProceduralNoise.DomainWarp(
            world,
            profile.ContinentScale * 0.42f,
            profile.ContinentScale * 0.085f,
            profile.Seed);

        float continent = ProceduralNoise.Fbm(
            warped.X / profile.ContinentScale,
            warped.Y / profile.ContinentScale,
            profile.Seed + 11,
            6);
        continent = Mathf.Clamp((continent + 1.0f) * 0.5f, 0.0f, 1.0f);

        float islandNoise = ProceduralNoise.Ridged(
            (warped.X + 2509.0f) / (profile.ContinentScale * 0.46f),
            (warped.Y - 1877.0f) / (profile.ContinentScale * 0.46f),
            profile.Seed + 233,
            4);
        float island = Mathf.SmoothStep(0.63f, 0.86f, islandNoise) *
            (1.0f - Mathf.SmoothStep(0.38f, 0.58f, continent));

        float basin = Mathf.SmoothStep(0.18f, 0.82f, continent);
        float shelf = Mathf.SmoothStep(0.35f, 0.72f, continent);

        Vector2 mountainWarp = ProceduralNoise.DomainWarp(
            world,
            profile.MountainScale * 0.62f,
            profile.MountainScale * 0.11f,
            profile.Seed + 199);
        float ridge = ProceduralNoise.Ridged(
            mountainWarp.X / profile.MountainScale,
            mountainWarp.Y / profile.MountainScale,
            profile.Seed + 29,
            7);
        float mountainMask = Mathf.SmoothStep(0.42f, 0.86f, continent);
        float mountains = ridge * mountainMask * profile.MountainWeight;

        float broad = ProceduralNoise.Fbm(
            warped.X / (profile.MountainScale * 1.75f),
            warped.Y / (profile.MountainScale * 1.75f),
            profile.Seed + 41,
            5) * 0.5f + 0.5f;

        float canyonNoise = ProceduralNoise.Ridged(
            (warped.X + 811.0f) / (profile.MountainScale * 0.82f),
            (warped.Y - 347.0f) / (profile.MountainScale * 0.82f),
            profile.Seed + 53,
            5);
        float mainRiver = 1.0f - Mathf.SmoothStep(0.035f, 0.215f, Mathf.Abs(canyonNoise - 0.52f));
        float tributaryNoise = ProceduralNoise.Ridged(
            (warped.X - 1729.0f) / (profile.MountainScale * 1.34f),
            (warped.Y + 941.0f) / (profile.MountainScale * 1.34f),
            profile.Seed + 137,
            4);
        float tributary = 1.0f - Mathf.SmoothStep(0.03f, 0.18f, Mathf.Abs(tributaryNoise - 0.48f));
        float river = Mathf.Max(mainRiver, tributary * 0.58f);
        river = Mathf.Clamp(river * Mathf.SmoothStep(0.21f, 0.72f, continent) * profile.RiverStrength * 1.24f, 0.0f, 1.0f);

        Vector2 climateWarp = ProceduralNoise.DomainWarp(
            world,
            profile.ContinentScale * 0.58f,
            profile.ContinentScale * 0.075f,
            profile.Seed + 307);
        float baseMoisture = Mathf.Clamp(
            ProceduralNoise.Fbm(
                (climateWarp.X - 1301.0f) / 1350.0f,
                (climateWarp.Y + 661.0f) / 1350.0f,
                profile.Seed + 83,
                5) * 0.5f + 0.5f,
            0.0f,
            1.0f);
        float latitude = Mathf.Abs(Mathf.Sin(world.Y / 9000.0f));
        float temperatureNoise = ProceduralNoise.Fbm(
            (climateWarp.X + 379.0f) / 4200.0f,
            (climateWarp.Y - 919.0f) / 4200.0f,
            profile.Seed + 317,
            4);
        float baseTemperature = Mathf.Clamp(1.0f - latitude + temperatureNoise * 0.16f - 0.04f, 0.0f, 1.0f);
        float aridity = (1.0f - Mathf.SmoothStep(0.30f, 0.58f, baseMoisture)) *
            Mathf.SmoothStep(0.52f, 0.84f, baseTemperature) *
            Mathf.SmoothStep(0.33f, 0.78f, continent + island * 0.22f);
        float lowlandMask = Mathf.SmoothStep(0.36f, 0.72f, continent + island * 0.25f) *
            (1.0f - Mathf.SmoothStep(0.22f, 0.50f, mountains));
        float plains = lowlandMask *
            (1.0f - aridity * 0.62f) *
            (1.0f - Mathf.SmoothStep(0.55f, 0.82f, baseMoisture));
        float wetland = Mathf.SmoothStep(0.66f, 0.88f, baseMoisture + river * 0.20f) *
            lowlandMask *
            Mathf.SmoothStep(0.25f, 0.68f, continent + island * 0.20f);
        float forest = Mathf.SmoothStep(0.54f, 0.78f, baseMoisture) *
            Mathf.SmoothStep(0.24f, 0.60f, baseTemperature) *
            (1.0f - Mathf.SmoothStep(0.44f, 0.78f, mountains));
        float hills = Mathf.SmoothStep(0.16f, 0.38f, mountains) *
            (1.0f - Mathf.SmoothStep(0.48f, 0.72f, mountains)) *
            Mathf.SmoothStep(0.42f, 0.78f, continent + island * 0.18f);
        float alpine = Mathf.SmoothStep(0.48f, 0.76f, mountains) *
            Mathf.SmoothStep(0.52f, 0.86f, continent + island * 0.12f);
        float duneDetail = ProceduralNoise.Ridged(
            (world.X + 541.0f) / 360.0f,
            (world.Y - 877.0f) / 360.0f,
            profile.Seed + 353,
            3);

        float micro = includeMicroDetail
            ? ProceduralNoise.Fbm(
                world.X / 118.0f,
                world.Y / 118.0f,
                profile.Seed + 71,
                4)
            : 0.0f;

        return new TerrainShapeTerms(
            continent,
            basin,
            shelf,
            mountains,
            broad,
            river,
            micro,
            baseMoisture,
            baseTemperature,
            aridity,
            plains,
            wetland,
            forest,
            hills,
            alpine,
            island,
            duneDetail);
    }

    private static float BuildHeight(TerrainShapeTerms terms, TerrainGenerationProfile profile)
    {
        float height = BuildUnbalancedHeight(terms, profile);
        height -= GetLandBalanceOffset(profile);

        float terraceMask = Mathf.SmoothStep(0.52f, 0.86f, terms.Mountains) *
            profile.VistaFrequency *
            Mathf.Lerp(0.55f, 1.0f, terms.Alpine);
        return ProceduralNoise.Terrace(height, Mathf.Max(12.0f, profile.TerraceStrength), terraceMask * 0.38f);
    }

    private static float BuildUnbalancedHeight(TerrainShapeTerms terms, TerrainGenerationProfile profile)
    {
        float lowlandFlatness = Mathf.Clamp(
            Mathf.Max(terms.Plains * 0.80f, Mathf.Max(terms.Aridity * 0.72f, terms.Wetland * 0.68f)) *
            (1.0f - Mathf.SmoothStep(0.32f, 0.64f, terms.Mountains)),
            0.0f,
            1.0f);
        float mountainFactor = Mathf.Lerp(0.48f, 1.14f, Mathf.Clamp(terms.Alpine + terms.Hills * 0.24f, 0.0f, 1.0f));
        float shelfFactor = Mathf.Lerp(0.20f, 0.34f, 1.0f - lowlandFlatness);
        float detailFactor = profile.DetailWeight *
            Mathf.Lerp(0.42f, 1.16f, Mathf.Clamp(terms.Alpine + terms.Hills * 0.45f, 0.0f, 1.0f)) *
            Mathf.Lerp(1.0f, 0.62f, lowlandFlatness);

        float height =
            ((terms.Basin - 0.44f) * profile.HeightScale * 0.72f) +
            (terms.Shelf * terms.BroadElevation * profile.HeightScale * shelfFactor) +
            (terms.Mountains * profile.HeightScale * mountainFactor) +
            (terms.MicroDetail * profile.HeightScale * detailFactor) +
            (terms.Island * profile.HeightScale * 0.36f);

        float lowlandTarget =
            ((terms.Basin - 0.46f) * profile.HeightScale * 0.44f) +
            ((terms.BroadElevation - 0.50f) * profile.HeightScale * 0.10f) +
            (terms.Island * profile.HeightScale * 0.25f);
        height = Mathf.Lerp(height, lowlandTarget, lowlandFlatness * 0.62f);
        height += terms.Aridity * (terms.DuneDetail - 0.40f) * profile.HeightScale * 0.075f;
        height -= terms.Wetland *
            Mathf.SmoothStep(0.26f, 0.72f, terms.Continent + terms.Island * 0.20f) *
            profile.HeightScale *
            0.045f;

        float shallowShelf = terms.Shelf * (1.0f - Mathf.SmoothStep(0.14f, 0.46f, terms.Mountains));
        float waterlineProximity = 1.0f - Mathf.SmoothStep(profile.SeaLevel + 52.0f, profile.SeaLevel + 220.0f, height);
        height -= shallowShelf * waterlineProximity * profile.HeightScale * 0.035f;

        float valleyCarve = terms.River * profile.RiverCarveDepth * (0.35f + terms.Mountains * 0.85f);
        height -= valleyCarve * profile.ValleyWeight;

        return height;
    }

    private static float GetLandBalanceOffset(TerrainGenerationProfile profile)
    {
        return LandBalanceOffsets.GetOrAdd(profile, ComputeLandBalanceOffset);
    }

    private static float ComputeLandBalanceOffset(TerrainGenerationProfile profile)
    {
        const int resolution = 17;
        const float targetLandRatio = 0.58f;
        const float correctionStrength = 0.36f;
        float extent = Mathf.Max(profile.ChunkSize * 48.0f, profile.ContinentScale * 2.2f);
        int landCount = 0;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float tx = x / (float)(resolution - 1);
                float ty = y / (float)(resolution - 1);
                Vector2 world = new((tx - 0.5f) * extent, (ty - 0.5f) * extent);
                TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: false);
                float height = BuildUnbalancedHeight(terms, profile);
                if (height >= profile.SeaLevel + 3.0f)
                {
                    landCount++;
                }
            }
        }

        float landRatio = landCount / (float)(resolution * resolution);
        float offset = (landRatio - targetLandRatio) * profile.HeightScale * correctionStrength;
        return Mathf.Clamp(offset, profile.HeightScale * -0.075f, profile.HeightScale * 0.075f);
    }

    private static TerrainWorldField BuildField(
        Vector2 world,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float height)
    {
        float moisture = Mathf.Clamp(
            terms.BaseMoisture + terms.River * 0.45f - terms.Aridity * 0.22f + terms.Wetland * 0.16f,
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

        float dominantVista = Mathf.Max(
            Mathf.Max(ridgeScore * 0.92f, riverContrast * 0.86f),
            Mathf.Max(Mathf.Max(coastDrama * 0.74f, highlandScore * 0.72f), Mathf.Max(desertVista * 0.54f, islandVista * 0.64f)));

        float blendedVista =
            ridgeScore * 0.30f +
            elevationScore * 0.18f +
            riverContrast * 0.22f +
            highlandScore * 0.14f +
            coastDrama * 0.10f +
            biomeContrast * 0.06f +
            desertVista * 0.06f +
            islandVista * 0.05f;

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
        return Mathf.Clamp(land * (1.0f - ruggedPenalty) * (1.0f - riverPenalty), 0.0f, 1.0f);
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

        return Mathf.Clamp(
            Mathf.Max(elevation * 0.58f, ridge * 0.70f) +
            plateau * 0.16f +
            scenicPotential * 0.18f +
            coastal * 0.08f,
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
        float waterAccess = Mathf.SmoothStep(0.18f, 0.66f, terms.River);
        float climate = Mathf.Clamp(1.0f - Mathf.Abs(temperature - 0.54f) * 1.75f, 0.0f, 1.0f);
        float lowElevation = 1.0f - Mathf.SmoothStep(profile.SeaLevel + 320.0f, profile.SeaLevel + profile.HeightScale * 0.92f, height);
        float oasis = terms.Aridity * Mathf.SmoothStep(0.38f, 0.78f, terms.River + moisture * 0.24f);
        float arableLowland = Mathf.Clamp(terms.Plains * 0.12f + terms.Wetland * 0.16f + oasis * 0.24f, 0.0f, 0.32f);
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

        return Mathf.Clamp(
            Mathf.Max(
                Mathf.Max(Mathf.Max(rugged * 0.74f, canyon * 0.82f), riverRisk * 0.50f),
                Mathf.Max(
                    Mathf.Max(desertExposure * 0.64f, floodRisk * 0.62f),
                    coastalStorm * 0.46f)) +
            waterDepth * 0.12f +
            highElevation * 0.16f +
            exposedRidge * 0.24f +
            snow * 0.08f +
            isolation * 0.16f +
            heatRisk * 0.28f +
            floodRisk * 0.18f +
            islandIsolation * 0.20f +
            coastalStorm * 0.10f,
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

        if (height > profile.SeaLevel + 680.0f || (temperature < 0.20f && height > profile.SeaLevel + 360.0f))
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

        if (terms.Aridity > 0.48f &&
            moisture < 0.56f &&
            height < profile.SeaLevel + 460.0f)
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

        if (height > profile.SeaLevel + 680.0f || (temperature < 0.20f && height > profile.SeaLevel + 360.0f))
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

    private readonly record struct TerrainShapeTerms(
        float Continent,
        float Basin,
        float Shelf,
        float Mountains,
        float BroadElevation,
        float River,
        float MicroDetail,
        float BaseMoisture,
        float BaseTemperature,
        float Aridity,
        float Plains,
        float Wetland,
        float Forest,
        float Hills,
        float Alpine,
        float Island,
        float DuneDetail);
}
