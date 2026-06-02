using Godot;

namespace Dao.Terrain.Generation;

public static class TerrainSampler
{
    private static readonly Color DeepWater = new(0.03f, 0.10f, 0.22f);
    private static readonly Color ShallowWater = new(0.09f, 0.26f, 0.36f);
    private static readonly Color Sand = new(0.63f, 0.56f, 0.38f);
    private static readonly Color Grass = new(0.24f, 0.42f, 0.20f);
    private static readonly Color Forest = new(0.10f, 0.26f, 0.15f);
    private static readonly Color Alpine = new(0.38f, 0.42f, 0.35f);
    private static readonly Color Rock = new(0.34f, 0.34f, 0.32f);
    private static readonly Color Snow = new(0.86f, 0.88f, 0.84f);

    public static TerrainSample Sample(Vector2 world, TerrainGenerationProfile profile)
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
        float river = 1.0f - Mathf.SmoothStep(0.02f, 0.135f, Mathf.Abs(canyonNoise - 0.52f));
        river *= Mathf.SmoothStep(0.23f, 0.72f, continent) * profile.RiverStrength;

        float micro = ProceduralNoise.Fbm(
            world.X / 118.0f,
            world.Y / 118.0f,
            profile.Seed + 71,
            4);

        float height =
            ((basin - 0.48f) * profile.HeightScale * 0.72f) +
            (shelf * broad * profile.HeightScale * 0.34f) +
            (mountains * profile.HeightScale * 1.08f) +
            (micro * profile.HeightScale * profile.DetailWeight);

        float valleyCarve = river * profile.RiverCarveDepth * (0.35f + mountains * 0.85f);
        height -= valleyCarve * profile.ValleyWeight;

        float terraceMask = Mathf.SmoothStep(0.52f, 0.86f, mountains) * profile.VistaFrequency;
        height = ProceduralNoise.Terrace(height, Mathf.Max(12.0f, profile.TerraceStrength), terraceMask * 0.38f);

        float moisture = Mathf.Clamp(
            (ProceduralNoise.Fbm(world.X / 950.0f, world.Y / 950.0f, profile.Seed + 83, 5) * 0.5f) + 0.5f + river * 0.45f,
            0.0f,
            1.0f);
        float latitude = Mathf.Abs(Mathf.Sin(world.Y / 9000.0f));
        float temperature = Mathf.Clamp(1.0f - latitude - Mathf.Max(0.0f, height) / (profile.HeightScale * 1.7f), 0.0f, 1.0f);

        return new TerrainSample(
            height,
            continent,
            mountains,
            river,
            moisture,
            temperature,
            0.0f,
            ColorFor(height, profile.SeaLevel, 0.0f, river, moisture, temperature));
    }

    public static TerrainSample SampleWithSlope(Vector2 world, TerrainGenerationProfile profile, float spacing)
    {
        TerrainSample center = Sample(world, profile);
        float delta = Mathf.Max(1.0f, spacing);
        float left = Sample(new Vector2(world.X - delta, world.Y), profile).Height;
        float right = Sample(new Vector2(world.X + delta, world.Y), profile).Height;
        float down = Sample(new Vector2(world.X, world.Y - delta), profile).Height;
        float up = Sample(new Vector2(world.X, world.Y + delta), profile).Height;

        Vector3 normal = new(left - right, delta * 2.0f, down - up);
        normal = normal.Normalized();
        float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);

        return center with
        {
            Slope = slope,
            Color = ColorFor(center.Height, profile.SeaLevel, slope, center.River, center.Moisture, center.Temperature)
        };
    }

    public static Vector3 NormalAt(Vector2 world, TerrainGenerationProfile profile, float spacing)
    {
        float delta = Mathf.Max(1.0f, spacing);
        float left = Sample(new Vector2(world.X - delta, world.Y), profile).Height;
        float right = Sample(new Vector2(world.X + delta, world.Y), profile).Height;
        float down = Sample(new Vector2(world.X, world.Y - delta), profile).Height;
        float up = Sample(new Vector2(world.X, world.Y + delta), profile).Height;
        return new Vector3(left - right, delta * 2.0f, down - up).Normalized();
    }

    public static Color ColorForSurface(Vector2 world, TerrainGenerationProfile profile, float height, float slope)
    {
        float canyonNoise = ProceduralNoise.Ridged(
            (world.X + 811.0f) / (profile.MountainScale * 0.82f),
            (world.Y - 347.0f) / (profile.MountainScale * 0.82f),
            profile.Seed + 53,
            4);
        float river = 1.0f - Mathf.SmoothStep(0.02f, 0.135f, Mathf.Abs(canyonNoise - 0.52f));
        river *= profile.RiverStrength;

        float moisture = Mathf.Clamp(
            (ProceduralNoise.Fbm(world.X / 950.0f, world.Y / 950.0f, profile.Seed + 83, 4) * 0.5f) + 0.5f + river * 0.45f,
            0.0f,
            1.0f);
        float latitude = Mathf.Abs(Mathf.Sin(world.Y / 9000.0f));
        float temperature = Mathf.Clamp(1.0f - latitude - Mathf.Max(0.0f, height) / (profile.HeightScale * 1.7f), 0.0f, 1.0f);

        return ColorFor(height, profile.SeaLevel, slope, river, moisture, temperature);
    }

    private static Color ColorFor(float height, float seaLevel, float slope, float river, float moisture, float temperature)
    {
        if (height < seaLevel - 18.0f)
        {
            return DeepWater.Lerp(ShallowWater, Mathf.Clamp((height - seaLevel + 80.0f) / 80.0f, 0.0f, 1.0f));
        }

        if (height < seaLevel + 8.0f)
        {
            return ShallowWater.Lerp(Sand, Mathf.Clamp((height - seaLevel + 18.0f) / 26.0f, 0.0f, 1.0f));
        }

        if (river > 0.62f && height < seaLevel + 360.0f)
        {
            return Grass.Lerp(Forest, 0.35f).Lerp(ShallowWater, 0.22f);
        }

        if (height > seaLevel + 650.0f || (temperature < 0.22f && height > seaLevel + 360.0f))
        {
            return Rock.Lerp(Snow, Mathf.Clamp((height - seaLevel - 520.0f) / 360.0f, 0.0f, 1.0f));
        }

        if (slope > 0.55f)
        {
            return Alpine.Lerp(Rock, Mathf.Clamp((slope - 0.55f) / 0.38f, 0.0f, 1.0f));
        }

        if (moisture > 0.62f && temperature > 0.28f)
        {
            return Grass.Lerp(Forest, Mathf.Clamp((moisture - 0.55f) / 0.35f, 0.0f, 1.0f));
        }

        if (temperature < 0.34f)
        {
            return Grass.Lerp(Alpine, 0.55f);
        }

        return Sand.Lerp(Grass, Mathf.Clamp((moisture + temperature) * 0.5f, 0.0f, 1.0f));
    }
}
