using System;
using System.Buffers;
using System.Diagnostics;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static int ComputeSurfaceProcessingMaxDegreeOfParallelism()
    {
        int processors = System.Environment.ProcessorCount;
        if (processors <= 1)
        {
            return 1;
        }

        return Math.Min(4, Math.Max(2, processors / 2));
    }

    private static int ComputeSurfaceProcessingParallelBuildSlotCount()
    {
        int processors = System.Environment.ProcessorCount;
        int workersPerBuild = Math.Max(1, SurfaceProcessingMaxDegreeOfParallelism);
        return Math.Max(1, processors / workersPerBuild);
    }

    private static bool ShouldUseParallelSurfaceProcessing(
        bool useNativeFields,
        bool useNativeHeights,
        int vertexCount)
    {
        return !useNativeHeights &&
            (useNativeFields || vertexCount >= ParallelSurfaceProcessingVertexThreshold) &&
            vertexCount >= ParallelSurfaceProcessingVertexThreshold &&
            SurfaceProcessingMaxDegreeOfParallelism > 1;
    }

    private static TerrainTileSamplingDecision CalibrateNativeSamplerSelection(
        TerrainGenerationProfile profile,
        int lod,
        int resolution)
    {
        if (!NativeTerrainBridge.SupportsFieldGridSampler)
        {
            return TerrainTileSamplingDecision.Managed("native field grid unavailable");
        }

        TerrainGenerationProfile managedProfile = profile with { UseNativeSamplerWhenAvailable = false };
        TerrainGenerationProfile nativeProfile = profile with { UseNativeSamplerWhenAvailable = true };
        TerrainTileCoord[] coords = NativeSamplerCalibrationCoords(profile.Seed);

        if (!TryWarmUpFieldBackend(managedProfile, resolution, TerrainTileSamplingBackendMode.Managed, coords[0]) ||
            !TryWarmUpFieldBackend(nativeProfile, resolution, TerrainTileSamplingBackendMode.Native, coords[0]))
        {
            return TerrainTileSamplingDecision.Managed("native field sampling warmup failed");
        }

        double bestManagedMilliseconds = double.PositiveInfinity;
        double bestNativeMilliseconds = double.PositiveInfinity;
        for (int pass = 0; pass < NativeSamplerSelectionMeasurementPasses; pass++)
        {
            if ((pass & 1) == 0)
            {
                bestManagedMilliseconds = Math.Min(
                    bestManagedMilliseconds,
                    MeasureFieldSamplingMillisecondsPerTile(managedProfile, resolution, TerrainTileSamplingBackendMode.Managed, coords));
                bestNativeMilliseconds = Math.Min(
                    bestNativeMilliseconds,
                    MeasureFieldSamplingMillisecondsPerTile(nativeProfile, resolution, TerrainTileSamplingBackendMode.Native, coords));
            }
            else
            {
                bestNativeMilliseconds = Math.Min(
                    bestNativeMilliseconds,
                    MeasureFieldSamplingMillisecondsPerTile(nativeProfile, resolution, TerrainTileSamplingBackendMode.Native, coords));
                bestManagedMilliseconds = Math.Min(
                    bestManagedMilliseconds,
                    MeasureFieldSamplingMillisecondsPerTile(managedProfile, resolution, TerrainTileSamplingBackendMode.Managed, coords));
            }
        }

        if (!double.IsFinite(bestManagedMilliseconds) ||
            !double.IsFinite(bestNativeMilliseconds) ||
            bestNativeMilliseconds <= 0.0)
        {
            return TerrainTileSamplingDecision.Managed("native field sampling measurement failed");
        }

        double speedup = bestManagedMilliseconds / bestNativeMilliseconds;
        bool useNative = speedup >= NativeSamplerSelectionMinSpeedup;
        return new TerrainTileSamplingDecision(
            useNative,
            bestManagedMilliseconds,
            bestNativeMilliseconds,
            speedup,
            resolution,
            useNative
                ? "native field grid won calibration"
                : "managed field sampler won calibration");
    }

    private static bool TryWarmUpFieldBackend(
        TerrainGenerationProfile profile,
        int resolution,
        TerrainTileSamplingBackendMode backendMode,
        TerrainTileCoord coord)
    {
        try
        {
            return TryMeasureFieldSampling(profile, resolution, backendMode, [coord], out _);
        }
        catch
        {
            return false;
        }
    }

    private static double MeasureFieldSamplingMillisecondsPerTile(
        TerrainGenerationProfile profile,
        int resolution,
        TerrainTileSamplingBackendMode backendMode,
        TerrainTileCoord[] coords)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        bool measured = TryMeasureFieldSampling(profile, resolution, backendMode, coords, out double checksum);
        stopwatch.Stop();
        NativeSamplerSelectionMeasurementSink = checksum;
        return !measured || coords.Length == 0
            ? double.PositiveInfinity
            : stopwatch.Elapsed.TotalMilliseconds / coords.Length;
    }

    private static bool TryMeasureFieldSampling(
        TerrainGenerationProfile profile,
        int resolution,
        TerrainTileSamplingBackendMode backendMode,
        ReadOnlySpan<TerrainTileCoord> coords,
        out double checksum)
    {
        checksum = 0.0;
        int vertexCountPerSide = resolution + 1;
        int vertexCount = vertexCountPerSide * vertexCountPerSide;
        float step = profile.ChunkSize / resolution;

        if (backendMode == TerrainTileSamplingBackendMode.Native)
        {
            int nativeFieldSampleCount = vertexCount * TerrainWorldFieldSampler.NativeFieldGridStride;
            float[] nativeFieldSamples = ArrayPool<float>.Shared.Rent(nativeFieldSampleCount);
            try
            {
                foreach (TerrainTileCoord coord in coords)
                {
                    if (!NativeTerrainBridge.TrySampleFieldGrid(
                            coord,
                            resolution,
                            profile,
                            nativeFieldSamples,
                            nativeFieldSampleCount,
                            out bool containsDerivedFields))
                    {
                        return false;
                    }

                    checksum += AccumulateNativeFieldGridChecksum(
                        coord,
                        profile,
                        resolution,
                        vertexCountPerSide,
                        step,
                        nativeFieldSamples,
                        containsDerivedFields);
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(nativeFieldSamples);
            }

            return true;
        }

        float landBalanceOffset = TerrainWorldFieldSampler.LandBalanceOffsetFor(profile);
        foreach (TerrainTileCoord coord in coords)
        {
            checksum += AccumulateManagedFieldGridChecksum(
                coord,
                profile,
                resolution,
                vertexCountPerSide,
                step,
                landBalanceOffset);
        }

        return true;
    }

    private static double AccumulateManagedFieldGridChecksum(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float landBalanceOffset)
    {
        double checksum = 0.0;
        Vector2 origin = coord.Origin(profile.ChunkSize);
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                Vector2 world = new(origin.X + x * step, origin.Y + z * step);
                TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile, landBalanceOffset);
                checksum += FieldSamplingChecksum(field, Index(x, z, vertexCountPerSide));
            }
        }

        return checksum;
    }

    private static double AccumulateNativeFieldGridChecksum(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] nativeFieldSamples,
        bool containsDerivedFields)
    {
        double checksum = 0.0;
        Vector2 origin = coord.Origin(profile.ChunkSize);
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int index = Index(x, z, vertexCountPerSide);
                Vector2 world = new(origin.X + x * step, origin.Y + z * step);
                TerrainWorldField field = TerrainWorldFieldSampler.SampleNativeFieldGrid(
                    world,
                    profile,
                    nativeFieldSamples,
                    index,
                    containsDerivedFields);
                checksum += FieldSamplingChecksum(field, index);
            }
        }

        return checksum;
    }

    private static double FieldSamplingChecksum(TerrainWorldField field, int index)
    {
        return field.Height +
            field.River +
            field.ScenicPotential +
            field.Traversability +
            field.EncounterPotential +
            (int)field.BiomeKind * 0.03125 +
            (int)field.LandscapeKind * 0.015625 +
            index * 0.000001;
    }

    private static TerrainTileCoord[] NativeSamplerCalibrationCoords(int seed)
    {
        int xOffset = (int)(Hash01(seed, seed * 17 + 31, 5939, seed + 761) * 5.0f) - 2;
        int zOffset = (int)(Hash01(seed * 3 - 7, seed + 97, 5987, seed + 769) * 5.0f) - 2;
        return
        [
            new TerrainTileCoord(0, 0),
            new TerrainTileCoord(1 + xOffset, -1 + zOffset)
        ];
    }
}
