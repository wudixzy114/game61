using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldFieldSampler
{
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
        float rainShadowNoise = ProceduralNoise.Ridged(
            (climateWarp.X + 2281.0f) / (profile.ContinentScale * 0.64f),
            (climateWarp.Y - 3167.0f) / (profile.ContinentScale * 0.64f),
            profile.Seed + 389,
            4);
        float rainShadowAridity =
            Mathf.SmoothStep(0.50f, 0.82f, rainShadowNoise) *
            Mathf.SmoothStep(0.38f, 0.78f, baseTemperature) *
            Mathf.SmoothStep(0.34f, 0.82f, continent + island * 0.18f) *
            (1.0f - Mathf.SmoothStep(0.74f, 0.94f, baseMoisture));
        float aridity = (1.0f - Mathf.SmoothStep(0.30f, 0.58f, baseMoisture)) *
            Mathf.SmoothStep(0.52f, 0.84f, baseTemperature) *
            Mathf.SmoothStep(0.33f, 0.78f, continent + island * 0.22f);
        aridity = Mathf.Clamp(Mathf.Max(aridity, rainShadowAridity * 0.82f), 0.0f, 1.0f);
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
        float lakeBlob = ProceduralNoise.Fbm(
            (warped.X + 4211.0f) / (profile.ContinentScale * 0.31f),
            (warped.Y - 2753.0f) / (profile.ContinentScale * 0.31f),
            profile.Seed + 431,
            5) * 0.5f + 0.5f;
        float lakePocket = ProceduralNoise.Ridged(
            (warped.X - 1487.0f) / (profile.MountainScale * 0.92f),
            (warped.Y + 3191.0f) / (profile.MountainScale * 0.92f),
            profile.Seed + 443,
            4);
        float inlandWaterMask = Mathf.SmoothStep(0.36f, 0.74f, continent + island * 0.16f);
        float lowlandLake =
            Mathf.SmoothStep(0.52f, 0.76f, lakeBlob) *
            Mathf.SmoothStep(0.36f, 0.68f, lakePocket) *
            inlandWaterMask *
            (1.0f - Mathf.SmoothStep(0.32f, 0.62f, mountains)) *
            Mathf.Lerp(0.62f, 1.18f, Mathf.Clamp(baseMoisture + river * 0.18f - aridity * 0.20f, 0.0f, 1.0f));
        float alpineLake =
            Mathf.SmoothStep(0.34f, 0.58f, mountains) *
            (1.0f - Mathf.SmoothStep(0.64f, 0.84f, mountains)) *
            Mathf.SmoothStep(0.54f, 0.78f, lakeBlob) *
            Mathf.SmoothStep(0.48f, 0.74f, lakePocket) *
            inlandWaterMask *
            0.72f;
        float lake = Mathf.Clamp(Mathf.Max(lowlandLake * 1.34f, alpineLake * 1.22f), 0.0f, 1.0f);
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
            lake,
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
}
