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
    public Rect2 WorldBounds => new(
        Center - new Vector2(WorldSize * 0.5f, WorldSize * 0.5f),
        new Vector2(WorldSize, WorldSize));
    public int SampleCount => _samples?.Length ?? 0;
    public ReadOnlySpan<TerrainTraversalCost> Samples => _samples is null ? ReadOnlySpan<TerrainTraversalCost>.Empty : _samples;

    public TerrainTraversalCost GetSample(int x, int y)
    {
        ValidateIndices(x, y);
        TerrainTraversalCost[] samples = _samples ?? throw new InvalidOperationException("Terrain traversal cost grid has no samples.");
        return samples[(y * Width) + x];
    }

    public Vector2 GetWorldPosition(int x, int y)
    {
        ValidateIndices(x, y);
        if (Width == 1 && Height == 1)
        {
            return Center;
        }

        Rect2 bounds = WorldBounds;
        float tx = Width <= 1 ? 0.5f : x / (float)(Width - 1);
        float ty = Height <= 1 ? 0.5f : y / (float)(Height - 1);
        return new Vector2(
            bounds.Position.X + tx * bounds.Size.X,
            bounds.Position.Y + ty * bounds.Size.Y);
    }

    public bool TryGetGridIndex(Vector2 world, out Vector2I index)
    {
        index = default;
        if (_samples is null ||
            Width <= 0 ||
            Height <= 0 ||
            WorldSize <= 0.0f ||
            _samples.Length < Width * Height)
        {
            return false;
        }

        Rect2 bounds = WorldBounds;
        float minX = bounds.Position.X;
        float minY = bounds.Position.Y;
        float maxX = bounds.Position.X + bounds.Size.X;
        float maxY = bounds.Position.Y + bounds.Size.Y;
        if (world.X < minX || world.X > maxX || world.Y < minY || world.Y > maxY)
        {
            return false;
        }

        float tx = bounds.Size.X <= 0.0f ? 0.0f : (world.X - minX) / bounds.Size.X;
        float ty = bounds.Size.Y <= 0.0f ? 0.0f : (world.Y - minY) / bounds.Size.Y;
        index = new Vector2I(
            Mathf.Clamp(Mathf.RoundToInt(tx * Mathf.Max(0, Width - 1)), 0, Mathf.Max(0, Width - 1)),
            Mathf.Clamp(Mathf.RoundToInt(ty * Mathf.Max(0, Height - 1)), 0, Mathf.Max(0, Height - 1)));
        return true;
    }

    public TerrainTraversalCost GetNearestSample(Vector2 world)
    {
        if (!TryGetGridIndex(
                new Vector2(
                    Mathf.Clamp(world.X, WorldBounds.Position.X, WorldBounds.Position.X + WorldBounds.Size.X),
                    Mathf.Clamp(world.Y, WorldBounds.Position.Y, WorldBounds.Position.Y + WorldBounds.Size.Y)),
                out Vector2I index))
        {
            throw new InvalidOperationException("Terrain traversal cost grid has no samples.");
        }

        return GetSample(index.X, index.Y);
    }

    public TerrainTraversalCost[] QuerySamples(Rect2 worldBounds)
    {
        return QuerySamples(worldBounds, int.MaxValue);
    }

    public TerrainTraversalCost[] QuerySamples(Rect2 worldBounds, int maxSamples)
    {
        if (_samples is null || Width <= 0 || Height <= 0 || _samples.Length < Width * Height)
        {
            return [];
        }

        int safeMaxSamples = Mathf.Max(0, maxSamples);
        if (safeMaxSamples == 0)
        {
            return [];
        }

        float x0 = worldBounds.Position.X;
        float y0 = worldBounds.Position.Y;
        float x1 = worldBounds.Position.X + worldBounds.Size.X;
        float y1 = worldBounds.Position.Y + worldBounds.Size.Y;
        float minX = Mathf.Min(x0, x1);
        float maxX = Mathf.Max(x0, x1);
        float minY = Mathf.Min(y0, y1);
        float maxY = Mathf.Max(y0, y1);

        var matches = new System.Collections.Generic.List<TerrainTraversalCost>();
        for (int y = 0; y < Height && matches.Count < safeMaxSamples; y++)
        {
            for (int x = 0; x < Width && matches.Count < safeMaxSamples; x++)
            {
                TerrainTraversalCost sample = GetSample(x, y);
                Vector2 world = sample.WorldPosition;
                if (world.X >= minX &&
                    world.X <= maxX &&
                    world.Y >= minY &&
                    world.Y <= maxY)
                {
                    matches.Add(sample);
                }
            }
        }

        return matches.Count == 0 ? [] : matches.ToArray();
    }

    public TerrainTraversalCost[] ToSampleArray()
    {
        return _samples is null ? [] : (TerrainTraversalCost[])_samples.Clone();
    }

    public bool ContainsWorldPosition(Vector2 world)
    {
        return TryGetGridIndex(world, out _);
    }

    private void ValidateIndices(int x, int y)
    {
        if (_samples is null)
        {
            throw new InvalidOperationException("Terrain traversal cost grid has no samples.");
        }

        if (x < 0 || y < 0 || x >= Width || y >= Height || (y * Width) + x >= _samples.Length)
        {
            throw new ArgumentOutOfRangeException($"Traversal grid index ({x}, {y}) was outside {Width} x {Height}.");
        }
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

    /// <summary>Creates a traversal-cost grid exactly covering one streaming tile.</summary>
    public static TerrainTraversalCostGrid CreateTraversalCostGridForTile(
        TerrainGenerationProfile profile,
        TerrainTileCoord coord,
        int gridSize,
        float spacing = 24.0f)
    {
        float chunkSize = Mathf.Max(1.0f, profile.ChunkSize);
        Vector2 origin = coord.Origin(chunkSize);
        Vector2 center = origin + new Vector2(chunkSize * 0.5f, chunkSize * 0.5f);
        return CreateTraversalCostGrid(profile, center, chunkSize, gridSize, spacing);
    }

    /// <summary>Samples traversal costs inside a bounded world-space region without requiring rendered tiles.</summary>
    public static TerrainTraversalCost[] QueryTraversalCosts(
        TerrainGenerationProfile profile,
        Rect2 worldBounds,
        float sampleSpacing = 24.0f,
        int maxSamples = 1024)
    {
        Rect2 bounds = NormalizeBounds(worldBounds);
        float safeSpacing = Mathf.Max(1.0f, sampleSpacing);
        int safeMaxSamples = Mathf.Clamp(maxSamples, 0, 262_144);
        if (safeMaxSamples == 0 || bounds.Size.X <= 0.0f || bounds.Size.Y <= 0.0f)
        {
            return [];
        }

        int xCount = Mathf.Max(1, Mathf.FloorToInt(bounds.Size.X / safeSpacing) + 1);
        int yCount = Mathf.Max(1, Mathf.FloorToInt(bounds.Size.Y / safeSpacing) + 1);
        var samples = new System.Collections.Generic.List<TerrainTraversalCost>(Mathf.Min(safeMaxSamples, xCount * yCount));

        for (int y = 0; y < yCount && samples.Count < safeMaxSamples; y++)
        {
            float wy = yCount == 1
                ? bounds.Position.Y + bounds.Size.Y * 0.5f
                : Mathf.Min(bounds.Position.Y + y * safeSpacing, bounds.Position.Y + bounds.Size.Y);
            for (int x = 0; x < xCount && samples.Count < safeMaxSamples; x++)
            {
                float wx = xCount == 1
                    ? bounds.Position.X + bounds.Size.X * 0.5f
                    : Mathf.Min(bounds.Position.X + x * safeSpacing, bounds.Position.X + bounds.Size.X);
                samples.Add(SampleTraversalCost(new Vector2(wx, wy), profile, safeSpacing));
            }
        }

        return samples.Count == 0 ? [] : samples.ToArray();
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

    private static Rect2 NormalizeBounds(Rect2 bounds)
    {
        float x0 = bounds.Position.X;
        float y0 = bounds.Position.Y;
        float x1 = bounds.Position.X + bounds.Size.X;
        float y1 = bounds.Position.Y + bounds.Size.Y;
        float minX = Mathf.Min(x0, x1);
        float maxX = Mathf.Max(x0, x1);
        float minY = Mathf.Min(y0, y1);
        float maxY = Mathf.Max(y0, y1);
        return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
    }
}
