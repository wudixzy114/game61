using System;
using System.IO;
using System.Text.Json.Nodes;
using Dao.Terrain;
using Godot;

internal static class TerrainValidationBenchmarkArtifactChecks
{
    internal static TerrainBenchmarkArtifactSmokeReport ValidateTerrainBenchmarkArtifactContract()
    {
        try
        {
            TerrainTileBenchmarkReport report = CreateSampleReport();
            string json = TerrainBenchmarkArtifactWriter.ToJson(report);
            JsonObject? root = JsonNode.Parse(json) as JsonObject;
            bool jsonSchemaPassed =
                root is not null &&
                JsonStringEquals(root, "contract", TerrainBenchmarkArtifactWriter.Contract) &&
                JsonIntEquals(root, "version", TerrainBenchmarkArtifactWriter.Version) &&
                JsonStringEquals(root, "performanceContract", TerrainPerformanceContract.Contract) &&
                JsonStringEquals(root, "hardwareBaseline", TerrainPerformanceContract.TileBenchmarkHardwareBaseline) &&
                JsonBoolEquals(root, "passed", report.Passed) &&
                JsonIntEquals(root, "seed", report.Seed) &&
                JsonStringEquals(root, "profileHash", report.ProfileHash) &&
                JsonIntEquals(root, "measuredTileCount", report.MeasuredTileCount) &&
                root["coverage"] is JsonObject &&
                root["managed"] is JsonObject &&
                root["native"] is JsonObject &&
                root["thresholds"] is JsonObject &&
                JsonDoublePositive(root["managed"] as JsonObject, "millisecondsPerTile") &&
                JsonDoublePositive(root["managed"] as JsonObject, "allocatedKilobytesPerTile");

            string outputPath = Path.Combine(
                Path.GetTempPath(),
                "dao_terrain_validation",
                "benchmark_artifact_contract",
                "terrain_tile_benchmark.json");
            Error saveError = TerrainBenchmarkArtifactWriter.SaveJson(report, outputPath);
            string filePath = TerrainValidationCliHelpers.FileSystemPath(outputPath);
            bool fileExists = File.Exists(filePath);
            long fileBytes = fileExists ? new FileInfo(filePath).Length : 0L;
            JsonObject? fileRoot = fileExists ? JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject : null;
            bool fileRoundtripPassed =
                saveError == Error.Ok &&
                fileBytes >= json.Length &&
                fileRoot is not null &&
                JsonStringEquals(fileRoot, "contract", TerrainBenchmarkArtifactWriter.Contract) &&
                JsonIntEquals(fileRoot, "seed", report.Seed) &&
                JsonStringEquals(fileRoot, "profileHash", report.ProfileHash);

            bool passed = jsonSchemaPassed && saveError == Error.Ok && fileRoundtripPassed;
            return new TerrainBenchmarkArtifactSmokeReport(
                passed,
                jsonSchemaPassed,
                saveError == Error.Ok && fileExists,
                fileRoundtripPassed,
                outputPath,
                fileBytes,
                passed
                    ? "tile benchmark JSON artifact contract saves stable performance, allocation, coverage, threshold, and parity data"
                    : "tile benchmark JSON artifact contract did not save or roundtrip expected benchmark fields");
        }
        catch (Exception ex)
        {
            return new TerrainBenchmarkArtifactSmokeReport(
                false,
                false,
                false,
                false,
                string.Empty,
                0L,
                $"benchmark artifact smoke threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static TerrainTileBenchmarkReport CreateSampleReport()
    {
        TerrainTileBenchmarkPass managed = new(
            TileCount: 4,
            TotalVertices: 1024,
            TotalIndices: 4096,
            TotalScatter: 128,
            TotalLandmarks: 16,
            ElapsedMilliseconds: 32.0,
            P50Milliseconds: 7.0,
            P95Milliseconds: 9.0,
            P99Milliseconds: 10.0,
            AllocatedBytes: 512 * 1024,
            HeightChecksum: 1234.5);
        TerrainTileBenchmarkPass native = managed with
        {
            ElapsedMilliseconds = 16.0,
            P50Milliseconds = 3.5,
            P95Milliseconds = 4.5,
            P99Milliseconds = 5.0
        };

        return new TerrainTileBenchmarkReport(
            Passed: true,
            Seed: 613061,
            ProfileHash: "benchmark-artifact-smoke",
            ManagedBackendMode: "managed",
            NativeBackendMode: "native",
            NativeAvailable: true,
            NativeSelectedForTileGeneration: true,
            RequestedTileCount: 4,
            MeasuredTileCount: 4,
            Coverage: new TerrainTileBenchmarkCoverage(
                DistinctBiomeKinds: 4,
                DistinctLandscapeKinds: 3,
                PointOfInterestTileCount: 2,
                RouteTileCount: 2,
                GameplayRichTileCount: 3),
            Managed: managed,
            Native: native,
            ParityTileCount: 4,
            MaxHeightDelta: 0.01f,
            MaxColorDelta: 0.01f,
            NativeSpeedup: 2.0,
            MeasurementPassCount: 2,
            Thresholds: TerrainTileBenchmarkThresholds.Default,
            Reason: "sample benchmark artifact");
    }

    private static bool JsonStringEquals(JsonObject root, string propertyName, string expected)
    {
        return root.TryGetPropertyValue(propertyName, out JsonNode? node) &&
            string.Equals(node?.GetValue<string>(), expected, StringComparison.Ordinal);
    }

    private static bool JsonIntEquals(JsonObject root, string propertyName, int expected)
    {
        return root.TryGetPropertyValue(propertyName, out JsonNode? node) &&
            node?.GetValue<int>() == expected;
    }

    private static bool JsonBoolEquals(JsonObject root, string propertyName, bool expected)
    {
        return root.TryGetPropertyValue(propertyName, out JsonNode? node) &&
            node?.GetValue<bool>() == expected;
    }

    private static bool JsonDoublePositive(JsonObject? root, string propertyName)
    {
        return root is not null &&
            root.TryGetPropertyValue(propertyName, out JsonNode? node) &&
            node?.GetValue<double>() > 0.0;
    }
}
