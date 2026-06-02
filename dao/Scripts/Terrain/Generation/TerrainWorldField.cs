using Godot;

namespace Dao.Terrain.Generation;

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
    TerrainLandscapeKind LandscapeKind);

public static class TerrainWorldFieldSampler
{
    public static TerrainWorldField Sample(Vector2 world, TerrainGenerationProfile profile)
    {
        TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: true);
        float height = BuildHeight(terms, profile);
        return BuildField(world, profile, terms, height);
    }

    public static TerrainWorldField SampleKnownHeight(Vector2 world, TerrainGenerationProfile profile, float height)
    {
        TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: false);
        return BuildField(world, profile, terms, height);
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
        river = Mathf.Clamp(river * Mathf.SmoothStep(0.21f, 0.72f, continent) * profile.RiverStrength * 1.16f, 0.0f, 1.0f);

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
            micro);
    }

    private static float BuildHeight(TerrainShapeTerms terms, TerrainGenerationProfile profile)
    {
        float height =
            ((terms.Basin - 0.48f) * profile.HeightScale * 0.72f) +
            (terms.Shelf * terms.BroadElevation * profile.HeightScale * 0.34f) +
            (terms.Mountains * profile.HeightScale * 1.08f) +
            (terms.MicroDetail * profile.HeightScale * profile.DetailWeight);

        float valleyCarve = terms.River * profile.RiverCarveDepth * (0.35f + terms.Mountains * 0.85f);
        height -= valleyCarve * profile.ValleyWeight;

        float terraceMask = Mathf.SmoothStep(0.52f, 0.86f, terms.Mountains) * profile.VistaFrequency;
        return ProceduralNoise.Terrace(height, Mathf.Max(12.0f, profile.TerraceStrength), terraceMask * 0.38f);
    }

    private static TerrainWorldField BuildField(
        Vector2 world,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float height)
    {
        float moisture = Mathf.Clamp(
            (ProceduralNoise.Fbm(world.X / 950.0f, world.Y / 950.0f, profile.Seed + 83, 5) * 0.5f) + 0.5f + terms.River * 0.45f,
            0.0f,
            1.0f);
        float latitude = Mathf.Abs(Mathf.Sin(world.Y / 9000.0f));
        float temperature = Mathf.Clamp(1.0f - latitude - Mathf.Max(0.0f, height) / (profile.HeightScale * 1.7f), 0.0f, 1.0f);
        float scenicPotential = ComputeScenicPotential(height, profile, terms, moisture, temperature);
        float traversability = ComputeTraversability(height, profile, terms);
        TerrainLandscapeKind landscape = ClassifyLandscape(height, profile, terms, moisture, temperature, scenicPotential);

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

        float dominantVista = Mathf.Max(
            Mathf.Max(ridgeScore * 0.92f, riverContrast * 0.86f),
            Mathf.Max(coastDrama * 0.74f, highlandScore * 0.72f));

        float blendedVista =
            ridgeScore * 0.30f +
            elevationScore * 0.18f +
            riverContrast * 0.22f +
            highlandScore * 0.14f +
            coastDrama * 0.10f +
            biomeContrast * 0.06f;

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
        float ruggedPenalty = Mathf.Clamp(terms.Mountains * 1.45f, 0.0f, 0.82f);
        float riverPenalty = terms.River * 0.24f;
        return Mathf.Clamp(land * (1.0f - ruggedPenalty) * (1.0f - riverPenalty), 0.0f, 1.0f);
    }

    private static TerrainLandscapeKind ClassifyLandscape(
        float height,
        TerrainGenerationProfile profile,
        TerrainShapeTerms terms,
        float moisture,
        float temperature,
        float scenicPotential)
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
        float MicroDetail);
}
