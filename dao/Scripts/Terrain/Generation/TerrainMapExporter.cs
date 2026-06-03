using Godot;

namespace Dao.Terrain.Generation;

public enum TerrainBiome
{
    Ocean,
    Coast,
    Grassland,
    Forest,
    Wetland,
    Alpine,
    Rock,
    Snow,
    RiverValley,
    Canyon,
    Vista
}

public enum TerrainMapLayer
{
    Biome,
    Height,
    River,
    Moisture,
    Temperature,
    ScenicPotential,
    Traversability,
    Exposure,
    ResourcePotential,
    HazardPotential,
    EncounterPotential,
    Landscape
}

public readonly record struct TerrainMapSample(
    Vector2 WorldPosition,
    float Height,
    float River,
    float Moisture,
    float Temperature,
    float ScenicPotential,
    float Traversability,
    float Exposure,
    float ResourcePotential,
    float HazardPotential,
    float EncounterPotential,
    TerrainLandscapeKind LandscapeKind,
    TerrainBiome Biome,
    Color Color);

public static class TerrainMapExporter
{
    public static TerrainMapSample SampleWorld(Vector2 world, TerrainGenerationProfile profile)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        float slope = 1.0f - Mathf.Clamp(TerrainSampler.NormalAt(world, profile, 24.0f).Y, 0.0f, 1.0f);
        TerrainBiome biome = ClassifyBiome(field, profile.SeaLevel, slope);
        Color terrainColor = TerrainSampler.ColorForSurface(field, profile, slope);

        return new TerrainMapSample(
            world,
            field.Height,
            field.River,
            field.Moisture,
            field.Temperature,
            field.ScenicPotential,
            field.Traversability,
            field.Exposure,
            field.ResourcePotential,
            field.HazardPotential,
            field.EncounterPotential,
            field.LandscapeKind,
            biome,
            ColorForBiome(biome, terrainColor));
    }

    public static Image CreateBiomeMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize)
    {
        return CreateMap(profile, center, worldSize, imageSize, TerrainMapLayer.Biome);
    }

    public static Image CreateMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize,
        TerrainMapLayer layer)
    {
        int size = Mathf.Clamp(imageSize, 16, 4096);
        float safeWorldSize = Mathf.Max(1.0f, worldSize);
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float tx = size == 1 ? 0.0f : x / (float)(size - 1);
                float ty = size == 1 ? 0.0f : y / (float)(size - 1);
                Vector2 world = new(
                    center.X + (tx - 0.5f) * safeWorldSize,
                    center.Y + (ty - 0.5f) * safeWorldSize);
                image.SetPixel(x, y, ColorForLayer(SampleWorld(world, profile), profile, layer));
            }
        }

        return image;
    }

    public static Error SaveBiomeMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize,
        string outputPath)
    {
        return SaveMap(profile, center, worldSize, imageSize, TerrainMapLayer.Biome, outputPath);
    }

    public static Error SaveMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize,
        TerrainMapLayer layer,
        string outputPath)
    {
        Image image = CreateMap(profile, center, worldSize, imageSize, layer);
        return image.SavePng(outputPath);
    }

    private static TerrainBiome ClassifyBiome(TerrainWorldField field, float seaLevel, float slope)
    {
        if (field.Height < seaLevel - 12.0f)
        {
            return TerrainBiome.Ocean;
        }

        if (field.Height < seaLevel + 10.0f)
        {
            return TerrainBiome.Coast;
        }

        if (field.LandscapeKind == TerrainLandscapeKind.Canyon)
        {
            return TerrainBiome.Canyon;
        }

        if (field.River > 0.64f && field.Height < seaLevel + 420.0f)
        {
            return TerrainBiome.RiverValley;
        }

        if (field.LandscapeKind == TerrainLandscapeKind.Snowfield ||
            field.Height > seaLevel + 680.0f ||
            (field.Temperature < 0.20f && field.Height > seaLevel + 360.0f))
        {
            return TerrainBiome.Snow;
        }

        if (slope > 0.58f)
        {
            return TerrainBiome.Rock;
        }

        if (field.LandscapeKind == TerrainLandscapeKind.VistaPlateau)
        {
            return TerrainBiome.Vista;
        }

        if (field.Height > seaLevel + 420.0f ||
            field.Temperature < 0.34f ||
            field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif)
        {
            return TerrainBiome.Alpine;
        }

        if (field.LandscapeKind == TerrainLandscapeKind.Wetland)
        {
            return TerrainBiome.Wetland;
        }

        if (field.Moisture > 0.62f && field.Temperature > 0.28f)
        {
            return TerrainBiome.Forest;
        }

        return TerrainBiome.Grassland;
    }

    private static Color ColorForBiome(TerrainBiome biome, Color terrainColor)
    {
        Color overlay = biome switch
        {
            TerrainBiome.Ocean => new Color(0.03f, 0.10f, 0.22f),
            TerrainBiome.Coast => new Color(0.68f, 0.58f, 0.38f),
            TerrainBiome.Grassland => new Color(0.24f, 0.45f, 0.20f),
            TerrainBiome.Forest => new Color(0.08f, 0.28f, 0.13f),
            TerrainBiome.Wetland => new Color(0.11f, 0.33f, 0.26f),
            TerrainBiome.Alpine => new Color(0.42f, 0.45f, 0.38f),
            TerrainBiome.Rock => new Color(0.35f, 0.35f, 0.33f),
            TerrainBiome.Snow => new Color(0.88f, 0.90f, 0.86f),
            TerrainBiome.RiverValley => new Color(0.10f, 0.32f, 0.30f),
            TerrainBiome.Canyon => new Color(0.45f, 0.31f, 0.24f),
            TerrainBiome.Vista => new Color(0.55f, 0.47f, 0.26f),
            _ => terrainColor
        };

        return terrainColor.Lerp(overlay, 0.58f);
    }

    private static Color ColorForLayer(TerrainMapSample sample, TerrainGenerationProfile profile, TerrainMapLayer layer)
    {
        return layer switch
        {
            TerrainMapLayer.Biome => sample.Color,
            TerrainMapLayer.Height => ColorForHeight(sample.Height, profile),
            TerrainMapLayer.River => ScalarRamp(sample.River, new Color(0.04f, 0.07f, 0.10f), new Color(0.08f, 0.36f, 0.72f)),
            TerrainMapLayer.Moisture => ScalarRamp(sample.Moisture, new Color(0.42f, 0.31f, 0.18f), new Color(0.08f, 0.42f, 0.36f)),
            TerrainMapLayer.Temperature => ScalarRamp(sample.Temperature, new Color(0.40f, 0.55f, 0.78f), new Color(0.76f, 0.46f, 0.20f)),
            TerrainMapLayer.ScenicPotential => ScalarRamp(sample.ScenicPotential, new Color(0.10f, 0.10f, 0.12f), new Color(0.86f, 0.68f, 0.22f)),
            TerrainMapLayer.Traversability => ScalarRamp(sample.Traversability, new Color(0.25f, 0.08f, 0.08f), new Color(0.20f, 0.60f, 0.24f)),
            TerrainMapLayer.Exposure => ScalarRamp(sample.Exposure, new Color(0.10f, 0.12f, 0.15f), new Color(0.80f, 0.78f, 0.64f)),
            TerrainMapLayer.ResourcePotential => ScalarRamp(sample.ResourcePotential, new Color(0.12f, 0.12f, 0.08f), new Color(0.32f, 0.72f, 0.26f)),
            TerrainMapLayer.HazardPotential => ScalarRamp(sample.HazardPotential, new Color(0.12f, 0.10f, 0.10f), new Color(0.78f, 0.28f, 0.18f)),
            TerrainMapLayer.EncounterPotential => ScalarRamp(sample.EncounterPotential, new Color(0.10f, 0.10f, 0.14f), new Color(0.82f, 0.60f, 0.26f)),
            TerrainMapLayer.Landscape => ColorForLandscape(sample.LandscapeKind),
            _ => sample.Color
        };
    }

    private static Color ColorForHeight(float height, TerrainGenerationProfile profile)
    {
        float low = profile.SeaLevel - profile.HeightScale * 0.52f;
        float high = profile.SeaLevel + profile.HeightScale * 1.36f;
        float t = Mathf.Clamp((height - low) / Mathf.Max(1.0f, high - low), 0.0f, 1.0f);

        if (t < 0.32f)
        {
            return new Color(0.03f, 0.10f, 0.22f).Lerp(new Color(0.08f, 0.32f, 0.42f), t / 0.32f);
        }

        if (t < 0.58f)
        {
            return new Color(0.17f, 0.38f, 0.18f).Lerp(new Color(0.46f, 0.42f, 0.30f), (t - 0.32f) / 0.26f);
        }

        if (t < 0.82f)
        {
            return new Color(0.46f, 0.42f, 0.30f).Lerp(new Color(0.36f, 0.36f, 0.34f), (t - 0.58f) / 0.24f);
        }

        return new Color(0.36f, 0.36f, 0.34f).Lerp(new Color(0.90f, 0.91f, 0.88f), (t - 0.82f) / 0.18f);
    }

    private static Color ColorForLandscape(TerrainLandscapeKind landscape)
    {
        return landscape switch
        {
            TerrainLandscapeKind.Ocean => new Color(0.03f, 0.10f, 0.22f),
            TerrainLandscapeKind.Coast => new Color(0.70f, 0.58f, 0.34f),
            TerrainLandscapeKind.Lowland => new Color(0.30f, 0.48f, 0.20f),
            TerrainLandscapeKind.Wetland => new Color(0.08f, 0.34f, 0.30f),
            TerrainLandscapeKind.ForestBasin => new Color(0.08f, 0.25f, 0.12f),
            TerrainLandscapeKind.RiverValley => new Color(0.10f, 0.34f, 0.36f),
            TerrainLandscapeKind.Canyon => new Color(0.45f, 0.29f, 0.22f),
            TerrainLandscapeKind.Highlands => new Color(0.39f, 0.43f, 0.35f),
            TerrainLandscapeKind.MountainMassif => new Color(0.34f, 0.34f, 0.32f),
            TerrainLandscapeKind.Snowfield => new Color(0.88f, 0.90f, 0.86f),
            TerrainLandscapeKind.VistaPlateau => new Color(0.56f, 0.49f, 0.26f),
            _ => Colors.Magenta
        };
    }

    private static Color ScalarRamp(float value, Color low, Color high)
    {
        return low.Lerp(high, Mathf.Clamp(value, 0.0f, 1.0f));
    }
}
