using Godot;

namespace Dao.Terrain.Generation;

public enum TerrainBiome
{
    Ocean,
    Coast,
    Grassland,
    Forest,
    Alpine,
    Rock,
    Snow,
    RiverValley
}

public readonly record struct TerrainMapSample(
    Vector2 WorldPosition,
    float Height,
    float River,
    float Moisture,
    float Temperature,
    TerrainBiome Biome,
    Color Color);

public static class TerrainMapExporter
{
    public static TerrainMapSample SampleWorld(Vector2 world, TerrainGenerationProfile profile)
    {
        TerrainSample sample = TerrainSampler.SampleWithSlope(world, profile, 24.0f);
        TerrainBiome biome = ClassifyBiome(sample.Height, profile.SeaLevel, sample.Slope, sample.River, sample.Moisture, sample.Temperature);

        return new TerrainMapSample(
            world,
            sample.Height,
            sample.River,
            sample.Moisture,
            sample.Temperature,
            biome,
            ColorForBiome(biome, sample.Color));
    }

    public static Image CreateBiomeMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize)
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
                image.SetPixel(x, y, SampleWorld(world, profile).Color);
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
        Image image = CreateBiomeMap(profile, center, worldSize, imageSize);
        return image.SavePng(outputPath);
    }

    private static TerrainBiome ClassifyBiome(float height, float seaLevel, float slope, float river, float moisture, float temperature)
    {
        if (height < seaLevel - 12.0f)
        {
            return TerrainBiome.Ocean;
        }

        if (height < seaLevel + 10.0f)
        {
            return TerrainBiome.Coast;
        }

        if (river > 0.64f && height < seaLevel + 420.0f)
        {
            return TerrainBiome.RiverValley;
        }

        if (height > seaLevel + 680.0f || (temperature < 0.20f && height > seaLevel + 360.0f))
        {
            return TerrainBiome.Snow;
        }

        if (slope > 0.58f)
        {
            return TerrainBiome.Rock;
        }

        if (height > seaLevel + 420.0f || temperature < 0.34f)
        {
            return TerrainBiome.Alpine;
        }

        if (moisture > 0.62f && temperature > 0.28f)
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
            TerrainBiome.Alpine => new Color(0.42f, 0.45f, 0.38f),
            TerrainBiome.Rock => new Color(0.35f, 0.35f, 0.33f),
            TerrainBiome.Snow => new Color(0.88f, 0.90f, 0.86f),
            TerrainBiome.RiverValley => new Color(0.10f, 0.32f, 0.30f),
            _ => terrainColor
        };

        return terrainColor.Lerp(overlay, 0.58f);
    }
}
