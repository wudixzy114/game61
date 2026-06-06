using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Godot;

internal static class TerrainValidationBenchmarkChecks
{
    private const int TileBenchmarkMeasurementPasses = 5;

    internal static TerrainNativeSamplerSmokeReport ValidateNativeSamplerParity(TerrainGenerationProfile profile)
    {
        TerrainGenerationProfile nativeProfile = profile with { UseNativeSamplerWhenAvailable = true };
        TerrainTileCoord coord = new(0, 0);
        int resolution = nativeProfile.ResolutionForLod(0);
        int width = resolution + 1;
        int expectedCount = width * width;

        if (!NativeTerrainBridge.TrySampleHeightGrid(coord, resolution, nativeProfile, out float[] nativeHeights))
        {
            return new TerrainNativeSamplerSmokeReport(
                false,
                profile.Seed,
                profile.StableHash(),
                false,
                coord,
                resolution,
                0,
                false,
                false,
                0,
                0.0f,
                0.0f,
                0,
                0.0f,
                0.0f,
                0,
                0.0f,
                0.0f,
                "native height grid unavailable");
        }

        int expectedFieldFloatCount = expectedCount * TerrainWorldFieldSampler.NativeFieldGridStride;
        float[] nativeFieldSamples = new float[expectedFieldFloatCount];
        bool fieldGridAvailable = NativeTerrainBridge.TrySampleFieldGrid(
            coord,
            resolution,
            nativeProfile,
            nativeFieldSamples,
            expectedFieldFloatCount,
            out bool fieldGridContainsDerivedData);
        Vector2 origin = coord.Origin(nativeProfile.ChunkSize);
        float step = nativeProfile.ChunkSize / resolution;
        float maxDelta = 0.0f;
        double deltaSum = 0.0;
        int compared = Math.Min(expectedCount, nativeHeights.Length);
        float maxFieldDelta = 0.0f;
        double fieldDeltaSum = 0.0;
        int comparedFieldValues = 0;
        int fieldClassificationMismatchCount = 0;

        for (int z = 0; z < width; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = z * width + x;
                if (index >= compared)
                {
                    break;
                }

                Vector2 world = new(origin.X + x * step, origin.Y + z * step);
                TerrainWorldField managedField = TerrainWorldFieldSampler.Sample(world, nativeProfile);
                float delta = Math.Abs(nativeHeights[index] - managedField.Height);
                maxDelta = Math.Max(maxDelta, delta);
                deltaSum += delta;

                if (fieldGridAvailable && fieldGridContainsDerivedData)
                {
                    TerrainWorldField nativeField = TerrainWorldFieldSampler.SampleNativeFieldGrid(
                        world,
                        nativeProfile,
                        nativeFieldSamples,
                        index,
                        containsDerivedFields: true);
                    AccumulateFieldDelta(managedField.Height, nativeField.Height, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.Continent, nativeField.Continent, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.Basin, nativeField.Basin, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.Shelf, nativeField.Shelf, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.Mountains, nativeField.Mountains, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.BroadElevation, nativeField.BroadElevation, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.River, nativeField.River, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.Moisture, nativeField.Moisture, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.Temperature, nativeField.Temperature, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.ScenicPotential, nativeField.ScenicPotential, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.Traversability, nativeField.Traversability, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.Exposure, nativeField.Exposure, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.ResourcePotential, nativeField.ResourcePotential, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.HazardPotential, nativeField.HazardPotential, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);
                    AccumulateFieldDelta(managedField.EncounterPotential, nativeField.EncounterPotential, ref maxFieldDelta, ref fieldDeltaSum, ref comparedFieldValues);

                    if (managedField.BiomeKind != nativeField.BiomeKind || managedField.LandscapeKind != nativeField.LandscapeKind)
                    {
                        fieldClassificationMismatchCount++;
                    }
                }
            }
        }

        float averageDelta = compared == 0 ? 0.0f : (float)(deltaSum / compared);
        float averageFieldDelta = comparedFieldValues == 0 ? 0.0f : (float)(fieldDeltaSum / comparedFieldValues);
        TerrainGenerationProfile managedProfile = profile with { UseNativeSamplerWhenAvailable = false };
        TerrainTileData managedTile = TerrainTileBuilder.Build(coord, lod: 0, managedProfile, includeCollision: false);
        TerrainTileData nativeTile = TerrainTileBuilder.Build(coord, lod: 0, nativeProfile, includeCollision: false);
        int tileVertexCount = Math.Min(managedTile.Vertices.Length, nativeTile.Vertices.Length);
        float tileMaxHeightDelta = 0.0f;
        float tileMaxColorDelta = 0.0f;

        for (int i = 0; i < tileVertexCount; i++)
        {
            tileMaxHeightDelta = Math.Max(tileMaxHeightDelta, Math.Abs(nativeTile.Vertices[i].Y - managedTile.Vertices[i].Y));
            tileMaxColorDelta = Math.Max(tileMaxColorDelta, ColorDistance(nativeTile.Colors[i], managedTile.Colors[i]));
        }

        bool gridPassed =
            compared == expectedCount &&
            maxDelta <= TerrainDeterminismContract.NativeHeightMaxEpsilon &&
            averageDelta <= TerrainDeterminismContract.NativeHeightAverageEpsilon;
        bool tilePassed =
            tileVertexCount == managedTile.Vertices.Length &&
            tileVertexCount == nativeTile.Vertices.Length &&
            tileMaxHeightDelta <= TerrainDeterminismContract.NativeTileHeightEpsilon &&
            tileMaxColorDelta <= TerrainDeterminismContract.NativeTileColorEpsilon;
        int expectedComparedFieldValues = expectedCount * 15;
        bool fieldGridPassed =
            fieldGridAvailable &&
            fieldGridContainsDerivedData &&
            comparedFieldValues == expectedComparedFieldValues &&
            maxFieldDelta <= TerrainDeterminismContract.NativeFieldMaxEpsilon &&
            averageFieldDelta <= TerrainDeterminismContract.NativeFieldAverageEpsilon &&
            fieldClassificationMismatchCount == 0;
        bool passed = gridPassed && fieldGridPassed && tilePassed;
        string reason = passed
            ? "native height grid, derived field grid, and tile output match managed path tolerance"
            : !gridPassed
                ? "native height grid diverged from managed sampler"
                : !fieldGridAvailable
                    ? "native field grid unavailable"
                    : !fieldGridContainsDerivedData
                        ? "native field grid did not expose derived v2 fields"
                        : !fieldGridPassed
                            ? "native derived field grid diverged from managed sampler"
                            : "native tile output diverged from managed path";

        return new TerrainNativeSamplerSmokeReport(
            passed,
            profile.Seed,
            profile.StableHash(),
            true,
            coord,
            resolution,
            compared,
            fieldGridAvailable,
            fieldGridContainsDerivedData,
            comparedFieldValues,
            maxFieldDelta,
            averageFieldDelta,
            fieldClassificationMismatchCount,
            maxDelta,
            averageDelta,
            tileVertexCount,
            tileMaxHeightDelta,
            tileMaxColorDelta,
            reason);
    }

    internal static TerrainTileBenchmarkReport BenchmarkTerrainTiles(
        TerrainGenerationProfile profile,
        TerrainWorldPlan plan,
        int requestedTileCount)
    {
        TerrainTileCoord[] coords = SelectBenchmarkTileCoords(profile, plan, requestedTileCount);
        TerrainTileBenchmarkCoverage coverage = AnalyzeBenchmarkTileCoverage(profile, plan, coords);
        TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
        TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);
        TerrainGenerationProfile managedProfile = profile with { UseNativeSamplerWhenAvailable = false };
        TerrainGenerationProfile nativeProfile = profile with { UseNativeSamplerWhenAvailable = true };
        bool nativeAvailable = NativeTerrainBridge.IsAvailable;
        bool nativeSelected = nativeAvailable &&
            TerrainTileBuilder.ShouldUseNativeSamplerForTileGeneration(nativeProfile, lod: 0);
        TerrainTileBenchmarkThresholds thresholds = TerrainTileBenchmarkThresholds.Default;
        string profileHash = profile.StableHash();
        const string managedBackendMode = "managed";
        string nativeBackendMode = nativeAvailable
            ? (nativeSelected ? "native" : "native-enabled-adaptive")
            : "unavailable";

        if (coords.Length == 0)
        {
            return new TerrainTileBenchmarkReport(
                false,
                profile.Seed,
                profileHash,
                managedBackendMode,
                nativeBackendMode,
                nativeAvailable,
                nativeSelected,
                requestedTileCount,
                0,
                coverage,
                default,
                default,
                0,
                0.0f,
                0.0f,
                0.0,
                TileBenchmarkMeasurementPasses,
                thresholds,
                "no benchmark tile coordinates selected");
        }

        TerrainTileBuilder.Build(coords[0], lod: 0, managedProfile, includeCollision: false, corridorIndex, poiIndex);
        if (nativeAvailable)
        {
            TerrainTileBuilder.Build(coords[0], lod: 0, nativeProfile, includeCollision: false, corridorIndex, poiIndex);
        }

        TerrainTileBenchmarkPass managed = default;
        TerrainTileBenchmarkPass native = default;
        MeasureStableTileBuildPasses(
            coords,
            managedProfile,
            nativeProfile,
            nativeAvailable,
            corridorIndex,
            poiIndex,
            TileBenchmarkMeasurementPasses,
            ref managed,
            ref native);
        native = nativeAvailable
            ? native
            : default;

        int parityTileCount = 0;
        float maxHeightDelta = 0.0f;
        float maxColorDelta = 0.0f;
        if (nativeAvailable)
        {
            MeasureBenchmarkTileParity(
                coords,
                managedProfile,
                nativeProfile,
                corridorIndex,
                poiIndex,
                maxTiles: 8,
                out parityTileCount,
                out maxHeightDelta,
                out maxColorDelta);
        }

        double speedup = nativeAvailable && native.ElapsedMilliseconds > 0.0
            ? managed.ElapsedMilliseconds / native.ElapsedMilliseconds
            : 0.0;
        bool passed = EvaluateTileBenchmark(
            coords.Length,
            coverage,
            nativeAvailable,
            nativeSelected,
            managed,
            native,
            parityTileCount,
            maxHeightDelta,
            maxColorDelta,
            speedup,
            thresholds,
            out string reason);

        return new TerrainTileBenchmarkReport(
            passed,
            profile.Seed,
            profileHash,
            managedBackendMode,
            nativeBackendMode,
            nativeAvailable,
            nativeSelected,
            requestedTileCount,
            coords.Length,
            coverage,
            managed,
            native,
            parityTileCount,
            maxHeightDelta,
            maxColorDelta,
            speedup,
            TileBenchmarkMeasurementPasses,
            thresholds,
            reason);
    }

    private static void AccumulateFieldDelta(
        float managedValue,
        float nativeValue,
        ref float maxDelta,
        ref double deltaSum,
        ref int comparedValueCount)
    {
        float delta = Math.Abs(nativeValue - managedValue);
        maxDelta = Math.Max(maxDelta, delta);
        deltaSum += delta;
        comparedValueCount++;
    }

    private static void MeasureStableTileBuildPasses(
        TerrainTileCoord[] coords,
        TerrainGenerationProfile managedProfile,
        TerrainGenerationProfile nativeProfile,
        bool nativeAvailable,
        TerrainRouteCorridorIndex corridorIndex,
        TerrainPointOfInterestIndex poiIndex,
        int measurementPasses,
        ref TerrainTileBenchmarkPass bestManaged,
        ref TerrainTileBenchmarkPass bestNative)
    {
        int passes = Math.Max(1, measurementPasses);
        for (int pass = 0; pass < passes; pass++)
        {
            bool nativeFirst = nativeAvailable && pass % 2 == 1;
            if (nativeFirst)
            {
                TerrainTileBenchmarkPass native = MeasureTileBuildPass(coords, nativeProfile, corridorIndex, poiIndex);
                TerrainTileBenchmarkPass managed = MeasureTileBuildPass(coords, managedProfile, corridorIndex, poiIndex);
                bestNative = BestTileBenchmarkPass(bestNative, native);
                bestManaged = BestTileBenchmarkPass(bestManaged, managed);
            }
            else
            {
                TerrainTileBenchmarkPass managed = MeasureTileBuildPass(coords, managedProfile, corridorIndex, poiIndex);
                bestManaged = BestTileBenchmarkPass(bestManaged, managed);

                if (nativeAvailable)
                {
                    TerrainTileBenchmarkPass native = MeasureTileBuildPass(coords, nativeProfile, corridorIndex, poiIndex);
                    bestNative = BestTileBenchmarkPass(bestNative, native);
                }
            }
        }
    }

    private static TerrainTileBenchmarkPass BestTileBenchmarkPass(
        TerrainTileBenchmarkPass currentBest,
        TerrainTileBenchmarkPass candidate)
    {
        if (candidate.TileCount <= 0)
        {
            return currentBest;
        }

        if (currentBest.TileCount <= 0 ||
            candidate.MillisecondsPerTile < currentBest.MillisecondsPerTile)
        {
            return candidate;
        }

        return currentBest;
    }

    private static bool EvaluateTileBenchmark(
        int measuredTileCount,
        TerrainTileBenchmarkCoverage coverage,
        bool nativeAvailable,
        bool nativeSelected,
        TerrainTileBenchmarkPass managed,
        TerrainTileBenchmarkPass native,
        int parityTileCount,
        float maxHeightDelta,
        float maxColorDelta,
        double nativeSpeedup,
        TerrainTileBenchmarkThresholds thresholds,
        out string reason)
    {
        if (measuredTileCount <= 0 || managed.TileCount != measuredTileCount)
        {
            reason = "managed benchmark did not measure the requested tile set";
            return false;
        }

        int requiredBiomeKinds = RequiredBenchmarkCoverage(thresholds.MinBenchmarkBiomeKinds, measuredTileCount, tilesPerRequirement: 6);
        if (coverage.DistinctBiomeKinds < requiredBiomeKinds)
        {
            reason = $"benchmark biome coverage {coverage.DistinctBiomeKinds} below {requiredBiomeKinds}";
            return false;
        }

        int requiredLandscapeKinds = RequiredBenchmarkCoverage(thresholds.MinBenchmarkLandscapeKinds, measuredTileCount, tilesPerRequirement: 7);
        if (coverage.DistinctLandscapeKinds < requiredLandscapeKinds)
        {
            reason = $"benchmark landscape coverage {coverage.DistinctLandscapeKinds} below {requiredLandscapeKinds}";
            return false;
        }

        int requiredPoiTiles = RequiredBenchmarkCoverage(thresholds.MinBenchmarkPointOfInterestTiles, measuredTileCount, tilesPerRequirement: 5);
        if (coverage.PointOfInterestTileCount < requiredPoiTiles)
        {
            reason = $"benchmark POI tiles {coverage.PointOfInterestTileCount} below {requiredPoiTiles}";
            return false;
        }

        int requiredRouteTiles = RequiredBenchmarkCoverage(thresholds.MinBenchmarkRouteTiles, measuredTileCount, tilesPerRequirement: 5);
        if (coverage.RouteTileCount < requiredRouteTiles)
        {
            reason = $"benchmark route tiles {coverage.RouteTileCount} below {requiredRouteTiles}";
            return false;
        }

        int requiredGameplayRichTiles = RequiredBenchmarkCoverage(thresholds.MinBenchmarkGameplayRichTiles, measuredTileCount, tilesPerRequirement: 4);
        if (coverage.GameplayRichTileCount < requiredGameplayRichTiles)
        {
            reason = $"benchmark gameplay-rich tiles {coverage.GameplayRichTileCount} below {requiredGameplayRichTiles}";
            return false;
        }

        if (managed.MillisecondsPerTile > thresholds.MaxManagedMillisecondsPerTile)
        {
            reason = $"managed tile time {managed.MillisecondsPerTile:0.00} ms/tile exceeded {thresholds.MaxManagedMillisecondsPerTile:0.00}";
            return false;
        }

        if (!TileBenchmarkPercentilesWithinThresholds(
                managed,
                thresholds.MaxManagedP50Milliseconds,
                thresholds.MaxManagedP95Milliseconds,
                thresholds.MaxManagedP99Milliseconds,
                out reason))
        {
            reason = $"managed tile percentile {reason}";
            return false;
        }

        if (managed.AllocatedKilobytesPerTile > thresholds.MaxAllocatedKilobytesPerTile)
        {
            reason = $"managed allocation {managed.AllocatedKilobytesPerTile:0.0} KB/tile exceeded {thresholds.MaxAllocatedKilobytesPerTile:0.0}";
            return false;
        }

        if (!nativeAvailable)
        {
            reason = "native sampler unavailable; managed tile build stayed within benchmark thresholds";
            return true;
        }

        if (!nativeSelected)
        {
            reason = "native sampler available but adaptive selector kept the faster managed backend within thresholds";
            return true;
        }

        if (native.TileCount != measuredTileCount)
        {
            reason = "native benchmark did not measure the requested tile set";
            return false;
        }

        if (native.MillisecondsPerTile > thresholds.MaxNativeMillisecondsPerTile)
        {
            reason = $"native tile time {native.MillisecondsPerTile:0.00} ms/tile exceeded {thresholds.MaxNativeMillisecondsPerTile:0.00}";
            return false;
        }

        if (!TileBenchmarkPercentilesWithinThresholds(
                native,
                thresholds.MaxNativeP50Milliseconds,
                thresholds.MaxNativeP95Milliseconds,
                thresholds.MaxNativeP99Milliseconds,
                out reason))
        {
            reason = $"native tile percentile {reason}";
            return false;
        }

        if (native.AllocatedKilobytesPerTile > thresholds.MaxAllocatedKilobytesPerTile)
        {
            reason = $"native allocation {native.AllocatedKilobytesPerTile:0.0} KB/tile exceeded {thresholds.MaxAllocatedKilobytesPerTile:0.0}";
            return false;
        }

        if (nativeSpeedup < thresholds.MinNativeSpeedup)
        {
            reason = $"native speedup {nativeSpeedup:0.00}x below {thresholds.MinNativeSpeedup:0.00}x";
            return false;
        }

        int requiredParityTiles = Math.Min(thresholds.MinParityTileCount, measuredTileCount);
        if (parityTileCount < requiredParityTiles)
        {
            reason = $"native parity checked {parityTileCount} tiles, expected at least {requiredParityTiles}";
            return false;
        }

        if (maxHeightDelta > thresholds.MaxParityHeightDelta || maxColorDelta > thresholds.MaxParityColorDelta)
        {
            reason = $"native parity delta {maxHeightDelta:0.000}/{maxColorDelta:0.000} exceeded {thresholds.MaxParityHeightDelta:0.000}/{thresholds.MaxParityColorDelta:0.000}";
            return false;
        }

        reason = "native-enabled render tile build benchmark stayed within thresholds";
        return true;
    }

    private static bool TileBenchmarkPercentilesWithinThresholds(
        TerrainTileBenchmarkPass pass,
        double maxP50Milliseconds,
        double maxP95Milliseconds,
        double maxP99Milliseconds,
        out string reason)
    {
        if (pass.P50Milliseconds > maxP50Milliseconds)
        {
            reason = $"P50 {pass.P50Milliseconds:0.00} ms exceeded {maxP50Milliseconds:0.00}";
            return false;
        }

        if (pass.P95Milliseconds > maxP95Milliseconds)
        {
            reason = $"P95 {pass.P95Milliseconds:0.00} ms exceeded {maxP95Milliseconds:0.00}";
            return false;
        }

        if (pass.P99Milliseconds > maxP99Milliseconds)
        {
            reason = $"P99 {pass.P99Milliseconds:0.00} ms exceeded {maxP99Milliseconds:0.00}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static int RequiredBenchmarkCoverage(int threshold, int measuredTileCount, int tilesPerRequirement)
    {
        if (measuredTileCount <= 0)
        {
            return threshold;
        }

        int scaled = Math.Max(1, measuredTileCount / Math.Max(1, tilesPerRequirement));
        return Math.Min(threshold, scaled);
    }

    private static TerrainTileBenchmarkPass MeasureTileBuildPass(
        TerrainTileCoord[] coords,
        TerrainGenerationProfile profile,
        TerrainRouteCorridorIndex corridorIndex,
        TerrainPointOfInterestIndex poiIndex)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        var tileMilliseconds = new double[coords.Length];
        long totalVertices = 0;
        long totalIndices = 0;
        long totalScatter = 0;
        long totalLandmarks = 0;
        double heightChecksum = 0.0;
        int tileIndex = 0;

        foreach (TerrainTileCoord coord in coords)
        {
            long tileStart = Stopwatch.GetTimestamp();
            TerrainTileData data = TerrainTileBuilder.Build(coord, lod: 0, profile, includeCollision: false, corridorIndex, poiIndex);
            long tileEnd = Stopwatch.GetTimestamp();
            tileMilliseconds[tileIndex++] = TicksToMilliseconds(tileEnd - tileStart);
            totalVertices += data.Vertices.Length;
            totalIndices += data.Indices.Length;
            totalScatter += data.ScatterInstances.Length;
            totalLandmarks += data.Landmarks.Length;
            heightChecksum += data.MinHeight + data.MaxHeight;
            if (data.Vertices.Length > 0)
            {
                heightChecksum += data.Vertices[data.Vertices.Length - 1].Y;
            }
        }

        stopwatch.Stop();
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        return new TerrainTileBenchmarkPass(
            coords.Length,
            totalVertices,
            totalIndices,
            totalScatter,
            totalLandmarks,
            stopwatch.Elapsed.TotalMilliseconds,
            Percentile(tileMilliseconds, 50.0),
            Percentile(tileMilliseconds, 95.0),
            Percentile(tileMilliseconds, 99.0),
            Math.Max(0, allocatedAfter - allocatedBefore),
            heightChecksum);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 0)
        {
            return 0.0;
        }

        double[] sorted = (double[])values.Clone();
        Array.Sort(sorted);
        double clamped = Math.Clamp(percentile, 0.0, 100.0);
        double rank = (clamped / 100.0) * (sorted.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return sorted[lower];
        }

        double t = rank - lower;
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * t);
    }

    private static void MeasureBenchmarkTileParity(
        TerrainTileCoord[] coords,
        TerrainGenerationProfile managedProfile,
        TerrainGenerationProfile nativeProfile,
        TerrainRouteCorridorIndex corridorIndex,
        TerrainPointOfInterestIndex poiIndex,
        int maxTiles,
        out int comparedTileCount,
        out float maxHeightDelta,
        out float maxColorDelta)
    {
        comparedTileCount = Math.Min(Math.Max(0, maxTiles), coords.Length);
        maxHeightDelta = 0.0f;
        maxColorDelta = 0.0f;

        for (int tile = 0; tile < comparedTileCount; tile++)
        {
            TerrainTileData managedTile = TerrainTileBuilder.Build(coords[tile], lod: 0, managedProfile, includeCollision: false, corridorIndex, poiIndex);
            TerrainTileData nativeTile = TerrainTileBuilder.Build(coords[tile], lod: 0, nativeProfile, includeCollision: false, corridorIndex, poiIndex);
            int vertexCount = Math.Min(managedTile.Vertices.Length, nativeTile.Vertices.Length);

            for (int i = 0; i < vertexCount; i++)
            {
                maxHeightDelta = Math.Max(maxHeightDelta, Math.Abs(nativeTile.Vertices[i].Y - managedTile.Vertices[i].Y));
                maxColorDelta = Math.Max(maxColorDelta, ColorDistance(nativeTile.Colors[i], managedTile.Colors[i]));
            }
        }
    }

    private static TerrainTileCoord[] SelectBenchmarkTileCoords(
        TerrainGenerationProfile profile,
        TerrainWorldPlan plan,
        int requestedTileCount)
    {
        int maxCoords = Math.Max(1, requestedTileCount);
        var coords = new List<TerrainTileCoord>(maxCoords);
        var seen = new HashSet<TerrainTileCoord>();
        int poiQuota = Math.Min(maxCoords, Math.Max(1, maxCoords / 5));
        int routeQuota = Math.Min(maxCoords, coords.Count + Math.Max(1, maxCoords / 5));

        var poiCandidates = new List<GameplayScatterRegionCandidate>(plan.PointsOfInterest.Length);
        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            float settlementWeight = point.SettlementTier switch
            {
                TerrainSettlementTier.Town => 0.26f,
                TerrainSettlementTier.OasisHub => 0.24f,
                TerrainSettlementTier.Village => 0.18f,
                _ => 0.0f
            };
            float score = point.Score * 0.42f +
                point.ScenicPotential * 0.24f +
                point.Traversability * 0.12f +
                settlementWeight;
            poiCandidates.Add(new GameplayScatterRegionCandidate(point.WorldPosition, score));
        }

        AddSortedBenchmarkCoords(poiCandidates, profile, coords, seen, poiQuota);

        var routeCandidates = new List<GameplayScatterRegionCandidate>(plan.Routes.Length * 4);
        foreach (TerrainWorldRoute route in plan.Routes)
        {
            if (route.Waypoints.Length == 0)
            {
                continue;
            }

            float routeScore = route.AverageScenicPotential * 0.42f +
                route.AverageTraversability * 0.30f +
                (1.0f / Math.Max(1.0f, route.Cost)) * 0.04f;
            routeCandidates.Add(new GameplayScatterRegionCandidate(route.Waypoints[0], routeScore * 0.94f));
            routeCandidates.Add(new GameplayScatterRegionCandidate(route.Waypoints[route.Waypoints.Length / 2], routeScore));
            routeCandidates.Add(new GameplayScatterRegionCandidate(route.Waypoints[^1], routeScore * 0.90f));

            int stride = Math.Max(1, route.Waypoints.Length / 4);
            for (int i = stride; i < route.Waypoints.Length; i += stride)
            {
                routeCandidates.Add(new GameplayScatterRegionCandidate(route.Waypoints[i], routeScore * 0.96f));
            }
        }

        AddSortedBenchmarkCoords(routeCandidates, profile, coords, seen, routeQuota);

        var biomeCandidates = CreateBenchmarkGroupBuckets<TerrainBiomeKind>();
        var landscapeCandidates = CreateBenchmarkGroupBuckets<TerrainLandscapeKind>();
        var candidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length);
        foreach (TerrainWorldRegion region in plan.Regions)
        {
            if (region.RegionKind == TerrainWorldRegionKind.Ocean)
            {
                continue;
            }

            float score = BenchmarkRegionStressScore(region);
            int biomeIndex = Mathf.Clamp((int)region.BiomeKind, 0, biomeCandidates.Length - 1);
            int landscapeIndex = Mathf.Clamp((int)region.LandscapeKind, 0, landscapeCandidates.Length - 1);
            biomeCandidates[biomeIndex].Add(new GameplayScatterRegionCandidate(region.WorldPosition, score));
            landscapeCandidates[landscapeIndex].Add(new GameplayScatterRegionCandidate(region.WorldPosition, score));
            candidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, score));
        }

        AddBestBenchmarkGroupCoords(biomeCandidates, profile, coords, seen, maxCoords);
        AddBestBenchmarkGroupCoords(landscapeCandidates, profile, coords, seen, maxCoords);
        AddSortedBenchmarkCoords(candidates, profile, coords, seen, maxCoords);

        int radius = 0;
        while (coords.Count < maxCoords)
        {
            for (int z = -radius; z <= radius && coords.Count < maxCoords; z++)
            {
                for (int x = -radius; x <= radius && coords.Count < maxCoords; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(z)) != radius)
                    {
                        continue;
                    }

                    TerrainTileCoord coord = new(x, z);
                    if (seen.Add(coord))
                    {
                        coords.Add(coord);
                    }
                }
            }

            radius++;
        }

        return coords.ToArray();
    }

    private static TerrainTileBenchmarkCoverage AnalyzeBenchmarkTileCoverage(
        TerrainGenerationProfile profile,
        TerrainWorldPlan plan,
        TerrainTileCoord[] coords)
    {
        var biomeKinds = new HashSet<TerrainBiomeKind>();
        var landscapeKinds = new HashSet<TerrainLandscapeKind>();
        var poiTiles = new HashSet<TerrainTileCoord>();
        var routeTiles = new HashSet<TerrainTileCoord>();

        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            poiTiles.Add(WorldToCoord(point.WorldPosition, profile));
        }

        foreach (TerrainWorldRoute route in plan.Routes)
        {
            foreach (Vector2 waypoint in route.Waypoints)
            {
                routeTiles.Add(WorldToCoord(waypoint, profile));
            }
        }

        int poiTileCount = 0;
        int routeTileCount = 0;
        int gameplayRichTileCount = 0;
        foreach (TerrainTileCoord coord in coords)
        {
            Vector2 origin = coord.Origin(profile.ChunkSize);
            var center = new Vector2(origin.X + profile.ChunkSize * 0.5f, origin.Y + profile.ChunkSize * 0.5f);
            TerrainWorldField field = TerrainWorldFieldSampler.Sample(center, profile);
            biomeKinds.Add(field.BiomeKind);
            landscapeKinds.Add(field.LandscapeKind);

            if (poiTiles.Contains(coord))
            {
                poiTileCount++;
            }

            if (routeTiles.Contains(coord))
            {
                routeTileCount++;
            }

            if (IsBenchmarkGameplayRich(field))
            {
                gameplayRichTileCount++;
            }
        }

        return new TerrainTileBenchmarkCoverage(
            biomeKinds.Count,
            landscapeKinds.Count,
            poiTileCount,
            routeTileCount,
            gameplayRichTileCount);
    }

    private static List<GameplayScatterRegionCandidate>[] CreateBenchmarkGroupBuckets<TEnum>()
        where TEnum : struct, Enum
    {
        TEnum[] values = Enum.GetValues<TEnum>();
        var buckets = new List<GameplayScatterRegionCandidate>[values.Length];
        for (int i = 0; i < buckets.Length; i++)
        {
            buckets[i] = new List<GameplayScatterRegionCandidate>();
        }

        return buckets;
    }

    private static void AddBestBenchmarkGroupCoords(
        List<GameplayScatterRegionCandidate>[] groups,
        TerrainGenerationProfile profile,
        List<TerrainTileCoord> coords,
        HashSet<TerrainTileCoord> seen,
        int maxCoords)
    {
        foreach (List<GameplayScatterRegionCandidate> group in groups)
        {
            if (coords.Count >= maxCoords || group.Count == 0)
            {
                continue;
            }

            group.Sort((a, b) => b.Score.CompareTo(a.Score));
            TryAddBenchmarkCoord(coords, seen, group[0].WorldPosition, profile, maxCoords);
        }
    }

    private static void AddSortedBenchmarkCoords(
        List<GameplayScatterRegionCandidate> candidates,
        TerrainGenerationProfile profile,
        List<TerrainTileCoord> coords,
        HashSet<TerrainTileCoord> seen,
        int maxCoords)
    {
        if (coords.Count >= maxCoords || candidates.Count == 0)
        {
            return;
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        foreach (GameplayScatterRegionCandidate candidate in candidates)
        {
            if (TryAddBenchmarkCoord(coords, seen, candidate.WorldPosition, profile, maxCoords))
            {
                return;
            }
        }
    }

    private static float BenchmarkRegionStressScore(TerrainWorldRegion region)
    {
        float biomeStress = region.BiomeKind switch
        {
            TerrainBiomeKind.Desert => 0.18f,
            TerrainBiomeKind.Oasis => 0.18f,
            TerrainBiomeKind.Snowfield => 0.16f,
            TerrainBiomeKind.Wetland => 0.15f,
            TerrainBiomeKind.Lake => 0.15f,
            TerrainBiomeKind.Coast => 0.13f,
            TerrainBiomeKind.Island => 0.13f,
            _ => 0.0f
        };
        float landscapeStress = region.LandscapeKind switch
        {
            TerrainLandscapeKind.Canyon => 0.18f,
            TerrainLandscapeKind.MountainMassif => 0.17f,
            TerrainLandscapeKind.Snowfield => 0.16f,
            TerrainLandscapeKind.Lake => 0.15f,
            TerrainLandscapeKind.RiverValley => 0.14f,
            TerrainLandscapeKind.Coast => 0.12f,
            _ => 0.0f
        };

        return region.ScenicPotential * 0.24f +
            region.EncounterPotential * 0.22f +
            region.ResourcePotential * 0.16f +
            region.HazardPotential * 0.22f +
            region.Exposure * 0.10f +
            region.Traversability * 0.06f +
            biomeStress +
            landscapeStress;
    }

    private static bool IsBenchmarkGameplayRich(TerrainWorldField field)
    {
        return field.EncounterPotential >= 0.52f ||
            field.ResourcePotential >= 0.50f ||
            field.HazardPotential >= 0.42f ||
            field.ScenicPotential >= 0.62f;
    }

    private static bool TryAddBenchmarkCoord(
        List<TerrainTileCoord> coords,
        HashSet<TerrainTileCoord> seen,
        Vector2 world,
        TerrainGenerationProfile profile,
        int maxCoords)
    {
        if (coords.Count >= maxCoords)
        {
            return true;
        }

        TerrainTileCoord coord = WorldToCoord(world, profile);
        if (seen.Add(coord))
        {
            coords.Add(coord);
        }

        return coords.Count >= maxCoords;
    }

    internal static TerrainTileCoord WorldToCoord(Vector2 world, TerrainGenerationProfile profile)
    {
        return new TerrainTileCoord(
            Mathf.FloorToInt(world.X / profile.ChunkSize),
            Mathf.FloorToInt(world.Y / profile.ChunkSize));
    }

    private static float ColorDistance(Color a, Color b)
    {
        float dr = a.R - b.R;
        float dg = a.G - b.G;
        float db = a.B - b.B;
        float da = a.A - b.A;
        return MathF.Sqrt((dr * dr) + (dg * dg) + (db * db) + (da * da));
    }
}
