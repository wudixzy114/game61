using System;
using System.IO;
using System.Text.Json;
using Godot;

internal static class TerrainBenchmarkArtifactWriter
{
    internal const string Contract = "terrain-tile-benchmark-artifact-v1";
    internal const int Version = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static string ToJson(TerrainTileBenchmarkReport report)
    {
        var dto = new
        {
            contract = Contract,
            version = Version,
            generatedAtUtc = DateTimeOffset.UtcNow,
            performanceContract = Dao.Terrain.TerrainPerformanceContract.Contract,
            hardwareBaseline = Dao.Terrain.TerrainPerformanceContract.TileBenchmarkHardwareBaseline,
            passed = report.Passed,
            seed = report.Seed,
            profileHash = report.ProfileHash,
            managedBackendMode = report.ManagedBackendMode,
            nativeBackendMode = report.NativeBackendMode,
            nativeAvailable = report.NativeAvailable,
            nativeSelectedForTileGeneration = report.NativeSelectedForTileGeneration,
            requestedTileCount = report.RequestedTileCount,
            measuredTileCount = report.MeasuredTileCount,
            coverage = report.Coverage,
            managed = CreatePassDto(report.Managed),
            native = CreatePassDto(report.Native),
            parityTileCount = report.ParityTileCount,
            maxHeightDelta = report.MaxHeightDelta,
            maxColorDelta = report.MaxColorDelta,
            nativeSpeedup = report.NativeSpeedup,
            measurementPassCount = report.MeasurementPassCount,
            thresholds = report.Thresholds,
            reason = report.Reason
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    internal static Error SaveJson(TerrainTileBenchmarkReport report, string outputPath)
    {
        try
        {
            string path = TerrainValidationCliHelpers.FileSystemPath(outputPath);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, ToJson(report));
            return Error.Ok;
        }
        catch
        {
            return Error.CantCreate;
        }
    }

    private static object CreatePassDto(TerrainTileBenchmarkPass pass)
    {
        return new
        {
            pass.TileCount,
            pass.TotalVertices,
            pass.TotalIndices,
            pass.TotalScatter,
            pass.TotalLandmarks,
            pass.ElapsedMilliseconds,
            pass.TilesPerSecond,
            pass.MillisecondsPerTile,
            pass.P50Milliseconds,
            pass.P95Milliseconds,
            pass.P99Milliseconds,
            pass.AllocatedBytes,
            pass.AllocatedMegabytes,
            pass.AllocatedKilobytesPerTile,
            pass.HeightChecksum
        };
    }
}
