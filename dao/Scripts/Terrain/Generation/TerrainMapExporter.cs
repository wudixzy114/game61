using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
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
public static class TerrainMapExporter
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

    private static Color ColorForBiome(TerrainBiomeKind biome, Color terrainColor)
    {
        Color overlay = biome switch
        {
            TerrainBiomeKind.Ocean => new Color(0.03f, 0.10f, 0.22f),
            TerrainBiomeKind.Coast => new Color(0.68f, 0.58f, 0.38f),
            TerrainBiomeKind.Island => new Color(0.28f, 0.58f, 0.34f),
            TerrainBiomeKind.Plains => new Color(0.45f, 0.56f, 0.22f),
            TerrainBiomeKind.Grassland => new Color(0.24f, 0.45f, 0.20f),
            TerrainBiomeKind.Desert => new Color(0.72f, 0.54f, 0.27f),
            TerrainBiomeKind.Oasis => new Color(0.12f, 0.54f, 0.42f),
            TerrainBiomeKind.Forest => new Color(0.08f, 0.28f, 0.13f),
            TerrainBiomeKind.Wetland => new Color(0.11f, 0.33f, 0.26f),
            TerrainBiomeKind.Hills => new Color(0.42f, 0.46f, 0.28f),
            TerrainBiomeKind.Mountains => new Color(0.36f, 0.36f, 0.34f),
            TerrainBiomeKind.Snowfield => new Color(0.88f, 0.90f, 0.86f),
            TerrainBiomeKind.Lake => new Color(0.05f, 0.34f, 0.44f),
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

    private static TerrainTraversalCost SampleTraversalCost(
        Vector2 world,
        TerrainGenerationProfile profile,
        float spacing)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        TerrainSample surface = TerrainSampler.SampleWithSlope(world, profile, spacing);
        return TerrainSemanticClassifier.ClassifyTraversalCost(field, surface, profile);
    }

    private static Color ColorForTraversalCost(TerrainTraversalCost traversal)
    {
        if (traversal.IsBlocked)
        {
            return new Color(0.08f, 0.07f, 0.09f);
        }

        float t = Mathf.Clamp((traversal.Cost - 1.0f) / 7.0f, 0.0f, 1.0f);
        if (t < 0.45f)
        {
            return new Color(0.12f, 0.50f, 0.22f).Lerp(new Color(0.72f, 0.64f, 0.22f), t / 0.45f);
        }

        return new Color(0.72f, 0.64f, 0.22f).Lerp(new Color(0.78f, 0.18f, 0.14f), (t - 0.45f) / 0.55f);
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
            TerrainLandscapeKind.Lake => new Color(0.05f, 0.32f, 0.46f),
            _ => Colors.Magenta
        };
    }

    private static Color ScalarRamp(float value, Color low, Color high)
    {
        return low.Lerp(high, Mathf.Clamp(value, 0.0f, 1.0f));
    }

    private static string FileSystemPath(string path)
    {
        return path.Contains("://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
    }

    private static void WritePng(Stream stream, TerrainMapRaster raster)
    {
        if (raster.Width <= 0 || raster.Height <= 0 || raster.PixelCount < raster.Width * raster.Height)
        {
            throw new ArgumentException("Terrain PNG raster dimensions are invalid.", nameof(raster));
        }

        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[0..4], raster.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..8], raster.Height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WritePngChunk(stream, "IHDR", ihdr);

        using var idatStream = new MemoryStream(Math.Max(1024, raster.Height * ((raster.Width * 4) + 1)));
        using (var deflate = new ZLibStream(idatStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            byte[] row = new byte[(raster.Width * 4) + 1];
            for (int y = 0; y < raster.Height; y++)
            {
                row[0] = 0;
                int cursor = 1;
                for (int x = 0; x < raster.Width; x++)
                {
                    Color color = raster.GetPixel(x, y);
                    row[cursor++] = ColorByte(color.R);
                    row[cursor++] = ColorByte(color.G);
                    row[cursor++] = ColorByte(color.B);
                    row[cursor++] = ColorByte(color.A);
                }

                deflate.Write(row, 0, row.Length);
            }
        }

        WritePngChunk(stream, "IDAT", idatStream.ToArray());
        WritePngChunk(stream, "IEND", ReadOnlySpan<byte>.Empty);
    }

    private static byte ColorByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(value, 0.0f, 1.0f) * 255.0f), 0, 255);
    }

    private static void WritePngChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        typeBytes[0] = (byte)type[0];
        typeBytes[1] = (byte)type[1];
        typeBytes[2] = (byte)type[2];
        typeBytes[3] = (byte)type[3];
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = UpdatePngCrc(0xFFFFFFFFu, typeBytes);
        crc = UpdatePngCrc(crc, data) ^ 0xFFFFFFFFu;

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint UpdatePngCrc(uint crc, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            crc = PngCrcTable[(crc ^ data[i]) & 0xFFu] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildPngCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1u) != 0u
                    ? 0xEDB88320u ^ (c >> 1)
                    : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
