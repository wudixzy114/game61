using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Convenience sampler combining field queries with surface-color computation for a world position.</summary>
public static class TerrainSampler
{
    private static readonly Color DeepWater = new(0.03f, 0.10f, 0.22f);
    private static readonly Color ShallowWater = new(0.09f, 0.26f, 0.36f);
    private static readonly Color Sand = new(0.63f, 0.56f, 0.38f);
    private static readonly Color DesertSand = new(0.70f, 0.55f, 0.30f);
    private static readonly Color DryGrass = new(0.48f, 0.48f, 0.22f);
    private static readonly Color Grass = new(0.24f, 0.42f, 0.20f);
    private static readonly Color Forest = new(0.10f, 0.26f, 0.15f);
    private static readonly Color Wetland = new(0.10f, 0.34f, 0.27f);
    private static readonly Color Alpine = new(0.38f, 0.42f, 0.35f);
    private static readonly Color Rock = new(0.34f, 0.34f, 0.32f);
    private static readonly Color Snow = new(0.86f, 0.88f, 0.84f);

    /// <summary>Full terrain sample including surface color at the given world position.</summary>
    public static TerrainSample Sample(Vector2 world, TerrainGenerationProfile profile)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);

        return new TerrainSample(
            field.Height,
            field.Continent,
            field.Mountains,
            field.River,
            field.Lake,
            field.Moisture,
            field.Temperature,
            field.ScenicPotential,
            field.Traversability,
            field.BiomeKind,
            field.LandscapeKind,
            0.0f,
            ColorFor(field.Height, profile.SeaLevel, 0.0f, field.River, field.Moisture, field.Temperature, field.BiomeKind));
    }

    /// <summary>Terrain sample with an approximate slope computed via finite differencing.</summary>
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
            Color = ColorFor(center.Height, profile.SeaLevel, slope, center.River, center.Moisture, center.Temperature, center.BiomeKind)
        };
    }

    /// <summary>Approximate surface normal at a world position using finite differencing.</summary>
    public static Vector3 NormalAt(Vector2 world, TerrainGenerationProfile profile, float spacing)
    {
        float delta = Mathf.Max(1.0f, spacing);
        float left = Sample(new Vector2(world.X - delta, world.Y), profile).Height;
        float right = Sample(new Vector2(world.X + delta, world.Y), profile).Height;
        float down = Sample(new Vector2(world.X, world.Y - delta), profile).Height;
        float up = Sample(new Vector2(world.X, world.Y + delta), profile).Height;
        return new Vector3(left - right, delta * 2.0f, down - up).Normalized();
    }

    /// <summary>Surface color at a world position given known height and slope.</summary>
    public static Color ColorForSurface(Vector2 world, TerrainGenerationProfile profile, float height, float slope)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.SampleKnownHeight(world, profile, height);
        return ColorForSurface(field, profile, slope);
    }

    /// <summary>Surface color derived from precomputed field attributes and slope.</summary>
    public static Color ColorForSurface(TerrainWorldField field, TerrainGenerationProfile profile, float slope)
    {
        return ColorFor(field.Height, profile.SeaLevel, slope, field.River, field.Moisture, field.Temperature, field.BiomeKind);
    }

    private static Color ColorFor(
        float height,
        float seaLevel,
        float slope,
        float river,
        float moisture,
        float temperature,
        TerrainBiomeKind biome)
    {
        if (height < seaLevel - 18.0f)
        {
            return DeepWater.Lerp(ShallowWater, Mathf.Clamp((height - seaLevel + 80.0f) / 80.0f, 0.0f, 1.0f));
        }

        if (height < seaLevel + 8.0f)
        {
            return ShallowWater.Lerp(Sand, Mathf.Clamp((height - seaLevel + 18.0f) / 26.0f, 0.0f, 1.0f));
        }

        if (biome == TerrainBiomeKind.Oasis)
        {
            Color oasisGreen = Grass.Lerp(Forest, Mathf.Clamp(moisture, 0.0f, 1.0f) * 0.48f);
            return DesertSand.Lerp(oasisGreen, 0.68f).Lerp(ShallowWater, Mathf.Clamp(river * 0.20f, 0.0f, 0.20f));
        }

        if (biome == TerrainBiomeKind.Desert)
        {
            Color dune = DesertSand.Lerp(Sand, Mathf.Clamp((temperature + 1.0f - moisture) * 0.35f, 0.0f, 1.0f));
            return slope > 0.42f
                ? dune.Lerp(Rock, Mathf.Clamp((slope - 0.42f) / 0.42f, 0.0f, 1.0f) * 0.52f)
                : dune;
        }

        if (biome == TerrainBiomeKind.Snowfield)
        {
            float snowStrength = Mathf.Clamp((height - seaLevel - 260.0f) / 260.0f + (0.34f - temperature) * 1.20f, 0.0f, 1.0f);
            return Rock.Lerp(Snow, snowStrength).Lerp(Colors.White, Mathf.Clamp((1.0f - slope) * 0.10f, 0.0f, 0.10f));
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

        if (biome == TerrainBiomeKind.Wetland)
        {
            return Grass.Lerp(Wetland, Mathf.Clamp(moisture, 0.0f, 1.0f));
        }

        if (biome == TerrainBiomeKind.Plains)
        {
            return DryGrass.Lerp(Grass, Mathf.Clamp(moisture * 0.75f + temperature * 0.18f, 0.0f, 1.0f));
        }

        if (biome == TerrainBiomeKind.Island)
        {
            return Sand.Lerp(Grass, Mathf.Clamp(moisture * 0.62f + 0.18f, 0.0f, 1.0f));
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
