using System;
using System.IO;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Selects which terrain attribute is visualized when exporting a map image.</summary>
public enum TerrainMapLayer
{
    Biome = 0,
    Height = 1,
    River = 2,
    Moisture = 3,
    Temperature = 4,
    ScenicPotential = 5,
    Traversability = 6,
    Exposure = 7,
    ResourcePotential = 8,
    HazardPotential = 9,
    EncounterPotential = 10,
    Landscape = 11,
    TraversalCost = 12
}

/// <summary>A single terrain sample ready for map export with pre-computed color.</summary>
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
    TerrainBiomeKind Biome,
    Color Color);

/// <summary>Pure managed RGBA terrain map raster used for deterministic CLI and runtime artifact export.</summary>
public readonly struct TerrainMapRaster
{
    private readonly Color[] _pixels;

    public TerrainMapRaster(int width, int height, Color[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels is null ? [] : (Color[])pixels.Clone();
    }

    public int Width { get; }
    public int Height { get; }
    public int PixelCount => _pixels?.Length ?? 0;
    public ReadOnlySpan<Color> Pixels => _pixels is null ? ReadOnlySpan<Color>.Empty : _pixels;

    public Color GetPixel(int x, int y)
    {
        Color[] pixels = _pixels ?? throw new InvalidOperationException("Terrain map raster has no pixels.");
        return pixels[(y * Width) + x];
    }

    public void SetPixel(int x, int y, Color color)
    {
        Color[] pixels = _pixels ?? throw new InvalidOperationException("Terrain map raster has no pixels.");
        pixels[(y * Width) + x] = color;
    }

    public Color[] ToPixelArray()
    {
        return _pixels is null ? [] : (Color[])_pixels.Clone();
    }
}

/// <summary>Structured traversal cost samples over a world-space square, for navigation and AI tools to consume without pathfinding.</summary>
public readonly struct TerrainTraversalCostGrid
{
    private readonly TerrainTraversalCost[] _samples;

    public TerrainTraversalCostGrid(
        int width,
        int height,
        Vector2 center,
        float worldSize,
        TerrainTraversalCost[] samples)
    {
        Width = width;
        Height = height;
        Center = center;
        WorldSize = worldSize;
        _samples = samples is null ? [] : (TerrainTraversalCost[])samples.Clone();
    }

    public int Width { get; }
    public int Height { get; }
    public Vector2 Center { get; }
    public float WorldSize { get; }
    public int SampleCount => _samples?.Length ?? 0;
    public ReadOnlySpan<TerrainTraversalCost> Samples => _samples is null ? ReadOnlySpan<TerrainTraversalCost>.Empty : _samples;

    public TerrainTraversalCost GetSample(int x, int y)
    {
        TerrainTraversalCost[] samples = _samples ?? throw new InvalidOperationException("Terrain traversal cost grid has no samples.");
        return samples[(y * Width) + x];
    }

    public TerrainTraversalCost[] ToSampleArray()
    {
        return _samples is null ? [] : (TerrainTraversalCost[])_samples.Clone();
    }
}

/// <summary>Exports terrain data as colorized map images (biome, height, moisture, etc.) and samples individual points.</summary>
public static partial class TerrainMapExporter
{
    private static readonly uint[] PngCrcTable = BuildPngCrcTable();

    /// <summary>Samples a single world point and produces a map-ready sample with surface color.</summary>
    public static TerrainMapSample SampleWorld(Vector2 world, TerrainGenerationProfile profile)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        float slope = 1.0f - Mathf.Clamp(TerrainSampler.NormalAt(world, profile, 24.0f).Y, 0.0f, 1.0f);
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
            field.BiomeKind,
            ColorForBiome(field.BiomeKind, terrainColor));
    }

    /// <summary>Creates a biome-colored map image of the terrain.</summary>
    public static Image CreateBiomeMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize)
    {
        return CreateMap(profile, center, worldSize, imageSize, TerrainMapLayer.Biome);
    }

    /// <summary>Creates a map image for the specified terrain layer.</summary>
    public static Image CreateMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize,
        TerrainMapLayer layer)
    {
        return CreateImage(CreateRaster(profile, center, worldSize, imageSize, layer));
    }

    /// <summary>Creates a pure managed map raster for the specified terrain layer.</summary>
    public static TerrainMapRaster CreateRaster(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize,
        TerrainMapLayer layer)
    {
        int size = Mathf.Clamp(imageSize, 16, 4096);
        float safeWorldSize = Mathf.Max(1.0f, worldSize);
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float tx = size == 1 ? 0.0f : x / (float)(size - 1);
                float ty = size == 1 ? 0.0f : y / (float)(size - 1);
                Vector2 world = new(
                    center.X + (tx - 0.5f) * safeWorldSize,
                    center.Y + (ty - 0.5f) * safeWorldSize);
                pixels[(y * size) + x] = layer == TerrainMapLayer.TraversalCost
                    ? ColorForTraversalCost(SampleTraversalCost(world, profile, spacing: 24.0f))
                    : ColorForLayer(SampleWorld(world, profile), profile, layer);
            }
        }

        return new TerrainMapRaster(size, size, pixels);
    }

    /// <summary>Creates a structured traversal-cost grid over a world-space square without building navigation paths.</summary>
    public static TerrainTraversalCostGrid CreateTraversalCostGrid(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int gridSize,
        float spacing = 24.0f)
    {
        int size = Mathf.Clamp(gridSize, 2, 4096);
        float safeWorldSize = Mathf.Max(1.0f, worldSize);
        var samples = new TerrainTraversalCost[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float tx = x / (float)(size - 1);
                float ty = y / (float)(size - 1);
                Vector2 world = new(
                    center.X + (tx - 0.5f) * safeWorldSize,
                    center.Y + (ty - 0.5f) * safeWorldSize);
                samples[(y * size) + x] = SampleTraversalCost(world, profile, spacing);
            }
        }

        return new TerrainTraversalCostGrid(size, size, center, safeWorldSize, samples);
    }

    /// <summary>Creates a Godot image from a managed terrain raster for runtime preview use.</summary>
    public static Image CreateImage(TerrainMapRaster raster)
    {
        var image = Image.CreateEmpty(raster.Width, raster.Height, false, Image.Format.Rgba8);
        for (int y = 0; y < raster.Height; y++)
        {
            for (int x = 0; x < raster.Width; x++)
            {
                image.SetPixel(x, y, raster.GetPixel(x, y));
            }
        }

        return image;
    }

    /// <summary>Saves a PNG biome map to disk.</summary>
    public static Error SaveBiomeMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize,
        string outputPath)
    {
        return SaveMap(profile, center, worldSize, imageSize, TerrainMapLayer.Biome, outputPath);
    }

    /// <summary>Saves a PNG map for the specified layer to disk.</summary>
    public static Error SaveMap(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize,
        TerrainMapLayer layer,
        string outputPath)
    {
        return SaveRasterPng(CreateRaster(profile, center, worldSize, imageSize, layer), outputPath);
    }

    /// <summary>Saves a managed RGBA raster as PNG without relying on Godot's Image native type.</summary>
    public static Error SaveRasterPng(TerrainMapRaster raster, string outputPath)
    {
        try
        {
            string path = FileSystemPath(outputPath);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = File.Create(path);
            WritePng(stream, raster);
            return Error.Ok;
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to save terrain PNG '{outputPath}': {exception.Message}");
            return Error.FileCantWrite;
        }
    }
}
