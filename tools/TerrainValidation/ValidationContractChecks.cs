using System;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Runtime;
using Godot;

internal static class TerrainValidationContractChecks
{
    internal static TerrainEnumContractSmokeReport ValidateTerrainEnumContracts()
    {
        try
        {
            int checkedTypeCount = 0;
            int checkedValueCount = 0;
            string? failureReason = null;

            bool passed =
                CheckEnumContract<TerrainLandscapeKind>(
                    [
                        ("Ocean", 0),
                        ("Coast", 1),
                        ("Lowland", 2),
                        ("Wetland", 3),
                        ("ForestBasin", 4),
                        ("RiverValley", 5),
                        ("Canyon", 6),
                        ("Highlands", 7),
                        ("MountainMassif", 8),
                        ("Snowfield", 9),
                        ("VistaPlateau", 10),
                        ("Lake", 11)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainBiomeKind>(
                    [
                        ("Ocean", 0),
                        ("Coast", 1),
                        ("Island", 2),
                        ("Plains", 3),
                        ("Grassland", 4),
                        ("Desert", 5),
                        ("Oasis", 6),
                        ("Forest", 7),
                        ("Wetland", 8),
                        ("Hills", 9),
                        ("Mountains", 10),
                        ("Snowfield", 11),
                        ("Lake", 12)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainWorldRegionKind>(
                    [
                        ("Ocean", 0),
                        ("Coast", 1),
                        ("Island", 2),
                        ("Plains", 3),
                        ("Grassland", 4),
                        ("Desert", 5),
                        ("Oasis", 6),
                        ("Lowland", 7),
                        ("Forest", 8),
                        ("Wetland", 9),
                        ("Hills", 10),
                        ("RiverValley", 11),
                        ("Canyon", 12),
                        ("Highlands", 13),
                        ("Mountains", 14),
                        ("Snow", 15),
                        ("ScenicPlateau", 16),
                        ("Lake", 17)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainPointOfInterestKind>(
                    [
                        ("SettlementCandidate", 0),
                        ("Vista", 1),
                        ("RiverCrossing", 2),
                        ("MountainPass", 3),
                        ("CoastalLanding", 4),
                        ("ResourceGrove", 5),
                        ("AncientSite", 6),
                        ("CanyonOverlook", 7),
                        ("Oasis", 8)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainSettlementTier>(
                    [
                        ("None", 0),
                        ("Village", 1),
                        ("Town", 2),
                        ("OasisHub", 3)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainRouteKind>(
                    [
                        ("PrimaryTrail", 0),
                        ("RiverRoad", 1),
                        ("RidgePass", 2),
                        ("CoastalPath", 3),
                        ("ScenicTrail", 4)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainScatterKind>(
                    [
                        ("Tree", 0),
                        ("Rock", 1),
                        ("Understory", 2),
                        ("ResourceNode", 3),
                        ("HazardOutcrop", 4),
                        ("GrassTuft", 5),
                        ("DesertShrub", 6),
                        ("CactusCluster", 7),
                        ("ReedCluster", 8),
                        ("SnowClump", 9),
                        ("AlpinePine", 10),
                        ("CoastalPalm", 11),
                        ("Driftwood", 12),
                        ("MangroveRoot", 13),
                        ("LakeReed", 14),
                        ("WaterLily", 15),
                        ("Landmark", 16)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainLandmarkKind>(
                    [
                        ("Settlement", 0),
                        ("Vista", 1),
                        ("RiverCrossing", 2),
                        ("MountainPass", 3),
                        ("AncientStone", 4),
                        ("CoastalLanding", 5),
                        ("ResourceGrove", 6),
                        ("CanyonOverlook", 7),
                        ("Oasis", 8),
                        ("Village", 9),
                        ("Town", 10),
                        ("OasisHub", 11),
                        ("VillageHouse", 12),
                        ("TownBlock", 13),
                        ("OasisCanopy", 14),
                        ("SettlementPlaza", 15),
                        ("OasisPool", 16),
                        ("Waterfall", 17),
                        ("RoadMarker", 18),
                        ("BridgeSpan", 19),
                        ("DuneCrest", 20),
                        ("DesertMonolith", 21),
                        ("CanyonNeedle", 22),
                        ("IceSpire", 23),
                        ("NaturalArch", 24),
                        ("GeothermalSpring", 25),
                        ("GlacialRidge", 26),
                        ("VillageWell", 27),
                        ("MarketStall", 28),
                        ("WatchTower", 29),
                        ("OasisGarden", 30),
                        ("SettlementGateway", 31)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainMapLayer>(
                    [
                        ("Biome", 0),
                        ("Height", 1),
                        ("River", 2),
                        ("Moisture", 3),
                        ("Temperature", 4),
                        ("ScenicPotential", 5),
                        ("Traversability", 6),
                        ("Exposure", 7),
                        ("ResourcePotential", 8),
                        ("HazardPotential", 9),
                        ("EncounterPotential", 10),
                        ("Landscape", 11),
                        ("TraversalCost", 12)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainWaterKind>(
                    [
                        ("None", 0),
                        ("Ocean", 1),
                        ("Coast", 2),
                        ("Lake", 3),
                        ("River", 4),
                        ("Oasis", 5)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainGameplayTag>(
                    [
                        ("None", 0),
                        ("Traversable", 1),
                        ("Scenic", 2),
                        ("ResourceRich", 4),
                        ("Hazardous", 8),
                        ("EncounterRich", 16),
                        ("WaterAccess", 32),
                        ("Coastal", 64),
                        ("SettlementFriendly", 128),
                        ("HighElevation", 256),
                        ("Cold", 512),
                        ("Arid", 1024)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason) &&
                CheckEnumContract<TerrainPointOfInterestVisualKind>(
                    [
                        ("Settlement", 0),
                        ("VistaSpire", 1),
                        ("RiverCrossing", 2),
                        ("MountainPass", 3),
                        ("CoastalLanding", 4),
                        ("ResourceGrove", 5),
                        ("AncientSite", 6),
                        ("CanyonOverlook", 7),
                        ("Oasis", 8),
                        ("Village", 9),
                        ("Town", 10),
                        ("OasisHub", 11)
                    ],
                    ref checkedTypeCount,
                    ref checkedValueCount,
                    out failureReason);

            return new TerrainEnumContractSmokeReport(
                passed,
                checkedTypeCount,
                checkedValueCount,
                passed
                    ? "public terrain enum names and numeric values match the stable contract"
                    : failureReason ?? "terrain enum contract failed");
        }
        catch (Exception ex)
        {
            return new TerrainEnumContractSmokeReport(
                false,
                0,
                0,
                $"terrain enum contract smoke threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static TerrainProfileHashSmokeReport ValidateTerrainProfileHashContract(TerrainGenerationProfile profile)
    {
        const string ExpectedDemoProfileHash = "e74e3118b219c64a022f93dbfe0ea0f33c0342394fa2ac7499a0d99abbc5bf58";
        string hash = profile.StableHash();
        bool formatPassed =
            hash.Length == 64 &&
            string.Equals(hash, hash.ToLowerInvariant(), StringComparison.Ordinal) &&
            HashContainsOnlyHex(hash);
        bool expectedHashPassed = string.Equals(hash, ExpectedDemoProfileHash, StringComparison.Ordinal);

        (string Name, TerrainGenerationProfile Profile)[] variants =
        [
            ("Seed", profile with { Seed = profile.Seed + 1 }),
            ("ChunkSize", profile with { ChunkSize = profile.ChunkSize + 1.0f }),
            ("BaseResolution", profile with { BaseResolution = profile.BaseResolution + 1 }),
            ("StreamRadiusChunks", profile with { StreamRadiusChunks = profile.StreamRadiusChunks + 1 }),
            ("CollisionRadiusChunks", profile with { CollisionRadiusChunks = profile.CollisionRadiusChunks + 1 }),
            ("MaxLod", profile with { MaxLod = profile.MaxLod + 1 }),
            ("HeightScale", profile with { HeightScale = profile.HeightScale + 1.0f }),
            ("SeaLevel", profile with { SeaLevel = profile.SeaLevel + 1.0f }),
            ("ContinentScale", profile with { ContinentScale = profile.ContinentScale + 1.0f }),
            ("MountainScale", profile with { MountainScale = profile.MountainScale + 1.0f }),
            ("MountainWeight", profile with { MountainWeight = profile.MountainWeight + 0.01f }),
            ("ValleyWeight", profile with { ValleyWeight = profile.ValleyWeight + 0.01f }),
            ("DetailWeight", profile with { DetailWeight = profile.DetailWeight + 0.01f }),
            ("VistaFrequency", profile with { VistaFrequency = profile.VistaFrequency + 0.01f }),
            ("RiverStrength", profile with { RiverStrength = profile.RiverStrength + 0.01f }),
            ("RiverCarveDepth", profile with { RiverCarveDepth = profile.RiverCarveDepth + 1.0f }),
            ("TerraceStrength", profile with { TerraceStrength = profile.TerraceStrength + 1.0f }),
            ("SkirtDepth", profile with { SkirtDepth = profile.SkirtDepth + 1.0f }),
            ("MaxCompletedTilesPerFrame", profile with { MaxCompletedTilesPerFrame = profile.MaxCompletedTilesPerFrame + 1 }),
            ("MaxQueuedTileJobs", profile with { MaxQueuedTileJobs = profile.MaxQueuedTileJobs + 1 }),
            ("MaxCachedTileData", profile with { MaxCachedTileData = profile.MaxCachedTileData + 1 }),
            ("GenerateCollision", profile with { GenerateCollision = !profile.GenerateCollision }),
            ("UseNativeSamplerWhenAvailable", profile with { UseNativeSamplerWhenAvailable = !profile.UseNativeSamplerWhenAvailable }),
            ("ScatterRuleSetHash", profile with { ScatterRuleSetHash = "alt-scatter-rule-set" }),
            ("SettlementVisualRuleSetHash", profile with { SettlementVisualRuleSetHash = "alt-settlement-visual-rule-set" }),
            ("PointOfInterestRuleSetHash", profile with { PointOfInterestRuleSetHash = "alt-poi-rule-set" }),
            ("RouteRuleSetHash", profile with { RouteRuleSetHash = "alt-route-rule-set" }),
            ("ScenicLandmarkRuleSetHash", profile with { ScenicLandmarkRuleSetHash = "alt-scenic-rule-set" })
        ];

        int sensitiveFieldCount = 0;
        string? insensitiveField = null;
        foreach ((string name, TerrainGenerationProfile variant) in variants)
        {
            if (string.Equals(hash, variant.StableHash(), StringComparison.Ordinal))
            {
                insensitiveField = name;
                break;
            }

            sensitiveFieldCount++;
        }

        bool fieldSensitivityPassed = sensitiveFieldCount == variants.Length;
        bool passed = formatPassed && expectedHashPassed && fieldSensitivityPassed;
        string reason = passed
            ? "terrain generation profile hash is stable, formatted, and sensitive to every profile field"
            : ProfileHashFailureReason(formatPassed, expectedHashPassed, fieldSensitivityPassed, insensitiveField);

        return new TerrainProfileHashSmokeReport(
            passed,
            hash,
            ExpectedDemoProfileHash,
            formatPassed,
            expectedHashPassed,
            fieldSensitivityPassed,
            sensitiveFieldCount,
            variants.Length,
            reason);
    }

    internal static TerrainValidationCliContractSmokeReport ValidateValidationCliContract()
    {
        bool tierSelectionPassed =
            TerrainValidationCliHelpers.ParseValidationTier(["--validation-tier", "pr"], out string prError).Name == "pr" &&
            string.IsNullOrEmpty(prError) &&
            TerrainValidationCliHelpers.ParseValidationTier(["--validation-tier", "nightly"], out string nightlyError).Name == "nightly" &&
            string.IsNullOrEmpty(nightlyError) &&
            TerrainValidationCliHelpers.ParseValidationTier(["--validation-tier", "release"], out string releaseError).Name == "release" &&
            string.IsNullOrEmpty(releaseError);

        bool fixedTierConfigurationPassed =
            TierMatches(TerrainValidationTierSpec.Pr, "pr", seedCount: 1, smokeAllSeeds: false, nativeSmoke: false, benchmarkTiles: false, benchmarkTileCount: 48) &&
            TierMatches(TerrainValidationTierSpec.Nightly, "nightly", seedCount: 10, smokeAllSeeds: true, nativeSmoke: false, benchmarkTiles: false, benchmarkTileCount: 48) &&
            TierMatches(TerrainValidationTierSpec.Release, "release", seedCount: 25, smokeAllSeeds: true, nativeSmoke: true, benchmarkTiles: true, benchmarkTileCount: 48);

        bool customFallbackPassed =
            TerrainValidationCliHelpers.ParseValidationTier(Array.Empty<string>(), out string customError).IsCustom &&
            string.IsNullOrEmpty(customError);

        TerrainValidationTierSpec skipRejected = TerrainValidationCliHelpers.ParseValidationTier(
            ["--validation-tier", "pr", "--skip-artifact-smoke"],
            out string skipError);
        bool skipOverrideRejected =
            skipRejected.IsCustom &&
            skipError.Contains("--skip-* flags", StringComparison.Ordinal);

        TerrainValidationTierSpec seedRejected = TerrainValidationCliHelpers.ParseValidationTier(
            ["--validation-tier", "nightly", "--seed", "1"],
            out string seedError);
        bool seedOverrideRejected =
            seedRejected.IsCustom &&
            seedError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

        TerrainValidationTierSpec worldRejected = TerrainValidationCliHelpers.ParseValidationTier(
            ["--validation-tier", "release", "--world-size", "4096"],
            out string worldError);
        bool worldOverrideRejected =
            worldRejected.IsCustom &&
            worldError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

        TerrainValidationTierSpec nativeRejected = TerrainValidationCliHelpers.ParseValidationTier(
            ["--validation-tier", "pr", "--native-smoke"],
            out string nativeError);
        bool nativeOverrideRejected =
            nativeRejected.IsCustom &&
            nativeError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

        TerrainValidationTierSpec smokeAllSeedsRejected = TerrainValidationCliHelpers.ParseValidationTier(
            ["--validation-tier", "pr", "--smoke-all-seeds"],
            out string smokeAllSeedsError);
        bool smokeAllSeedsOverrideRejected =
            smokeAllSeedsRejected.IsCustom &&
            smokeAllSeedsError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

        TerrainValidationTierSpec benchmarkRejected = TerrainValidationCliHelpers.ParseValidationTier(
            ["--validation-tier", "release", "--benchmark-tiles"],
            out string benchmarkError);
        bool benchmarkOverrideRejected =
            benchmarkRejected.IsCustom &&
            benchmarkError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

        TerrainValidationTierSpec unknownRejected = TerrainValidationCliHelpers.ParseValidationTier(
            ["--validation-tier", "fast"],
            out string unknownError);
        bool unknownTierRejected =
            unknownRejected.IsCustom &&
            unknownError.Contains("unknown --validation-tier", StringComparison.Ordinal) &&
            unknownError.Contains("pr, nightly, release", StringComparison.Ordinal);

        bool passed =
            tierSelectionPassed &&
            fixedTierConfigurationPassed &&
            customFallbackPassed &&
            skipOverrideRejected &&
            seedOverrideRejected &&
            worldOverrideRejected &&
            nativeOverrideRejected &&
            smokeAllSeedsOverrideRejected &&
            benchmarkOverrideRejected &&
            unknownTierRejected;

        string reason = passed
            ? "validation tiers remain fixed gates and reject weakening overrides"
            : "validation tier parsing accepted an invalid override or rejected a valid tier";

        return new TerrainValidationCliContractSmokeReport(
            passed,
            tierSelectionPassed,
            fixedTierConfigurationPassed,
            customFallbackPassed,
            skipOverrideRejected,
            seedOverrideRejected,
            worldOverrideRejected,
            nativeOverrideRejected,
            smokeAllSeedsOverrideRejected,
            benchmarkOverrideRejected,
            unknownTierRejected,
            reason);
    }

    internal static TerrainThresholdContractSmokeReport ValidateTerrainDefaultThresholdContracts()
    {
        TerrainWorldPlanningThresholds planning = TerrainWorldPlanningThresholds.OpenWorldDefault;
        bool planningPassed =
            planning.MinPointsOfInterest == 18 &&
            planning.MinPointOfInterestKinds == 5 &&
            planning.MinRoutes == 48 &&
            planning.MinRouteKinds == 3 &&
            ExactFloatEquals(planning.MinConnectedPointRatio, 0.95f) &&
            ExactFloatEquals(planning.MinConnectedSettlementRatio, 0.95f) &&
            planning.MinSettlementRoutes == 8 &&
            ExactFloatEquals(planning.MinPointOfInterestWorldCoverage, 0.70f) &&
            ExactFloatEquals(planning.MinRouteWorldCoverage, 0.70f) &&
            ExactFloatEquals(planning.MinAverageRouteTraversability, 0.34f) &&
            ExactFloatEquals(planning.MinAverageRouteScenicPotential, 0.20f) &&
            planning.MinVillages == 2 &&
            planning.MinTowns == 2 &&
            planning.MinOasisHubs == 1;

        TerrainQualityThresholds quality = TerrainQualityThresholds.OpenWorldDefault;
        bool qualityPassed =
            ExactFloatEquals(quality.MinLandRatio, 0.38f) &&
            ExactFloatEquals(quality.MaxLandRatio, 0.82f) &&
            ExactFloatEquals(quality.MinRiverRatio, 0.035f) &&
            ExactFloatEquals(quality.MinScenicRatio, 0.045f) &&
            ExactFloatEquals(quality.MinTraversableLandRatio, 0.28f) &&
            quality.MinDistinctLandscapeKinds == 6 &&
            quality.MinDistinctBiomeKinds == 7 &&
            ExactFloatEquals(quality.MinPlainsGrasslandRatio, 0.10f) &&
            ExactFloatEquals(quality.MinDesertOasisRatio, 0.005f) &&
            ExactFloatEquals(quality.MinIslandCoastRatio, 0.015f) &&
            ExactFloatEquals(quality.MinHillMountainRatio, 0.004f) &&
            ExactFloatEquals(quality.MinSnowRatio, 0.002f) &&
            ExactFloatEquals(quality.MinLakeRatio, 0.002f);

        TerrainExperienceThresholds experience = TerrainExperienceThresholds.OpenWorldDefault;
        bool experiencePassed =
            ExactFloatEquals(experience.MinEncounterRichRegionRatio, 0.22f) &&
            ExactFloatEquals(experience.MinResourceRichRegionRatio, 0.18f) &&
            ExactFloatEquals(experience.MinHazardRichRegionRatio, 0.12f) &&
            ExactFloatEquals(experience.MinAverageEncounterPotential, 0.34f) &&
            ExactFloatEquals(experience.MinAverageResourcePotential, 0.30f) &&
            ExactFloatEquals(experience.MinRouteRhythmScore, 0.46f) &&
            ExactFloatEquals(experience.MinPointOfInterestValue, 0.58f) &&
            ExactFloatEquals(experience.MinRiskRewardBalance, 0.42f) &&
            ExactFloatEquals(experience.MinScenicAnchorRatio, 0.28f);

        TerrainTileBenchmarkThresholds benchmark = TerrainTileBenchmarkThresholds.Default;
        bool benchmarkPassed =
            ExactDoubleEquals(benchmark.MaxManagedMillisecondsPerTile, TerrainPerformanceContract.MaxManagedMillisecondsPerTile) &&
            ExactDoubleEquals(benchmark.MaxNativeMillisecondsPerTile, TerrainPerformanceContract.MaxNativeMillisecondsPerTile) &&
            ExactDoubleEquals(benchmark.MaxManagedP50Milliseconds, TerrainPerformanceContract.MaxManagedP50Milliseconds) &&
            ExactDoubleEquals(benchmark.MaxManagedP95Milliseconds, TerrainPerformanceContract.MaxManagedP95Milliseconds) &&
            ExactDoubleEquals(benchmark.MaxManagedP99Milliseconds, TerrainPerformanceContract.MaxManagedP99Milliseconds) &&
            ExactDoubleEquals(benchmark.MaxNativeP50Milliseconds, TerrainPerformanceContract.MaxNativeP50Milliseconds) &&
            ExactDoubleEquals(benchmark.MaxNativeP95Milliseconds, TerrainPerformanceContract.MaxNativeP95Milliseconds) &&
            ExactDoubleEquals(benchmark.MaxNativeP99Milliseconds, TerrainPerformanceContract.MaxNativeP99Milliseconds) &&
            ExactDoubleEquals(benchmark.MaxAllocatedKilobytesPerTile, TerrainPerformanceContract.MaxAllocatedKilobytesPerTile) &&
            ExactDoubleEquals(benchmark.MinNativeSpeedup, TerrainPerformanceContract.MinNativeSpeedup) &&
            benchmark.MinParityTileCount == TerrainPerformanceContract.MinParityTileCount &&
            benchmark.MinBenchmarkBiomeKinds == TerrainPerformanceContract.MinBenchmarkBiomeKinds &&
            benchmark.MinBenchmarkLandscapeKinds == TerrainPerformanceContract.MinBenchmarkLandscapeKinds &&
            benchmark.MinBenchmarkPointOfInterestTiles == TerrainPerformanceContract.MinBenchmarkPointOfInterestTiles &&
            benchmark.MinBenchmarkRouteTiles == TerrainPerformanceContract.MinBenchmarkRouteTiles &&
            benchmark.MinBenchmarkGameplayRichTiles == TerrainPerformanceContract.MinBenchmarkGameplayRichTiles &&
            ExactFloatEquals(benchmark.MaxParityHeightDelta, TerrainDeterminismContract.TileParityHeightEpsilon) &&
            ExactFloatEquals(benchmark.MaxParityColorDelta, TerrainDeterminismContract.TileParityColorEpsilon);

        bool passed = planningPassed && qualityPassed && experiencePassed && benchmarkPassed;
        string reason = passed
            ? "default planning, quality, experience, and benchmark thresholds match the stable open-world contract"
            : ThresholdContractFailureReason(planningPassed, qualityPassed, experiencePassed, benchmarkPassed);

        return new TerrainThresholdContractSmokeReport(
            passed,
            planningPassed,
            qualityPassed,
            experiencePassed,
            benchmarkPassed,
            reason);
    }

    internal static TerrainDefaultStateContractSmokeReport ValidateTerrainDefaultStateContracts()
    {
        TerrainRouteCorridorSample corridorNone = TerrainRouteCorridorSample.None;
        bool corridorNonePassed =
            !corridorNone.HasInfluence &&
            corridorNone.Kind == TerrainRouteKind.PrimaryTrail &&
            ExactFloatEquals(corridorNone.Influence, 0.0f) &&
            ExactFloatEquals(corridorNone.CoreStrength, 0.0f) &&
            float.IsPositiveInfinity(corridorNone.Distance) &&
            ExactFloatEquals(corridorNone.TargetHeight, 0.0f) &&
            ExactFloatEquals(corridorNone.ScenicPotential, 0.0f) &&
            ExactFloatEquals(corridorNone.Traversability, 0.0f) &&
            corridorNone.Direction == Vector2.Zero;

        TerrainRouteCorridorIndex corridorIndexEmpty = TerrainRouteCorridorIndex.Empty;
        bool corridorIndexEmptyPassed =
            corridorIndexEmpty.CacheKey == 0 &&
            !corridorIndexEmpty.HasSegments &&
            corridorIndexEmpty.GetSegments(default).Length == 0 &&
            !corridorIndexEmpty.Sample(Vector2.Zero, default(TerrainTileCoord)).HasInfluence;

        TerrainPointOfInterestIndex poiIndexEmpty = TerrainPointOfInterestIndex.Empty;
        bool poiIndexEmptyPassed =
            poiIndexEmpty.CacheKey == 0 &&
            !poiIndexEmpty.HasPoints &&
            poiIndexEmpty.GetPoints(default).Length == 0;

        TerrainWaterSurfaceData waterSurfaceEmpty = TerrainWaterSurfaceData.Empty;
        bool waterSurfaceEmptyPassed =
            waterSurfaceEmpty.Vertices.Length == 0 &&
            waterSurfaceEmpty.Normals.Length == 0 &&
            waterSurfaceEmpty.Uvs.Length == 0 &&
            waterSurfaceEmpty.Colors.Length == 0 &&
            waterSurfaceEmpty.Indices.Length == 0 &&
            waterSurfaceEmpty.LakeCellCount == 0 &&
            waterSurfaceEmpty.RiverCellCount == 0 &&
            waterSurfaceEmpty.OasisCellCount == 0 &&
            ExactFloatEquals(waterSurfaceEmpty.MinHeight, 0.0f) &&
            ExactFloatEquals(waterSurfaceEmpty.MaxHeight, 0.0f) &&
            !waterSurfaceEmpty.HasSurface &&
            waterSurfaceEmpty.CellCount == 0;

        TerrainWorldPlanSnapshot planSnapshotEmpty = TerrainWorldPlanSnapshot.Empty;
        bool planSnapshotEmptyPassed =
            planSnapshotEmpty.Center == Vector2.Zero &&
            ExactFloatEquals(planSnapshotEmpty.WorldSize, 0.0f) &&
            planSnapshotEmpty.GridResolution == 0 &&
            planSnapshotEmpty.Regions.Length == 0 &&
            planSnapshotEmpty.PointsOfInterest.Length == 0 &&
            planSnapshotEmpty.Routes.Length == 0;

        bool passed =
            corridorNonePassed &&
            corridorIndexEmptyPassed &&
            poiIndexEmptyPassed &&
            waterSurfaceEmptyPassed &&
            planSnapshotEmptyPassed;
        string reason = passed
            ? "default route corridor, POI index, water surface, and empty plan snapshot states match the stable contract"
            : DefaultStateContractFailureReason(
                corridorNonePassed,
                corridorIndexEmptyPassed,
                poiIndexEmptyPassed,
                waterSurfaceEmptyPassed,
                planSnapshotEmptyPassed);

        return new TerrainDefaultStateContractSmokeReport(
            passed,
            corridorNonePassed,
            corridorIndexEmptyPassed,
            poiIndexEmptyPassed,
            waterSurfaceEmptyPassed,
            planSnapshotEmptyPassed,
            reason);
    }

    private static bool CheckEnumContract<TEnum>(
        (string Name, int Value)[] expected,
        ref int checkedTypeCount,
        ref int checkedValueCount,
        out string? failureReason)
        where TEnum : struct, Enum
    {
        Type enumType = typeof(TEnum);
        string[] names = Enum.GetNames<TEnum>();
        TEnum[] values = Enum.GetValues<TEnum>();
        if (names.Length != expected.Length || values.Length != expected.Length)
        {
            failureReason = $"{enumType.Name} member count changed ({names.Length}/{expected.Length})";
            return false;
        }

        var seenValues = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < expected.Length; i++)
        {
            int actualValue = Convert.ToInt32(values[i]);
            if (!string.Equals(names[i], expected[i].Name, StringComparison.Ordinal) ||
                actualValue != expected[i].Value)
            {
                failureReason = $"{enumType.Name}.{names[i]} drifted at index {i}: actual {names[i]}={actualValue}, expected {expected[i].Name}={expected[i].Value}";
                return false;
            }

            if (!seenValues.Add(actualValue))
            {
                failureReason = $"{enumType.Name} reused enum value {actualValue}";
                return false;
            }
        }

        checkedTypeCount++;
        checkedValueCount += expected.Length;
        failureReason = null;
        return true;
    }

    private static bool HashContainsOnlyHex(string hash)
    {
        for (int i = 0; i < hash.Length; i++)
        {
            char c = hash[i];
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!hex)
            {
                return false;
            }
        }

        return true;
    }

    private static string ProfileHashFailureReason(
        bool formatPassed,
        bool expectedHashPassed,
        bool fieldSensitivityPassed,
        string? insensitiveField)
    {
        if (!formatPassed)
        {
            return "terrain profile hash was not a 64-character lowercase hex SHA-256 string";
        }

        if (!expectedHashPassed)
        {
            return "terrain demo profile hash drifted from the stable content identity contract";
        }

        if (!fieldSensitivityPassed)
        {
            return $"terrain profile hash did not change when field '{insensitiveField}' changed";
        }

        return "terrain profile hash contract failed";
    }

    private static bool TierMatches(
        TerrainValidationTierSpec tier,
        string name,
        int seedCount,
        bool smokeAllSeeds,
        bool nativeSmoke,
        bool benchmarkTiles,
        int benchmarkTileCount)
    {
        return string.Equals(tier.Name, name, StringComparison.Ordinal) &&
            tier.SeedCount == seedCount &&
            tier.SmokeAllSeeds == smokeAllSeeds &&
            tier.NativeSmoke == nativeSmoke &&
            tier.BenchmarkTiles == benchmarkTiles &&
            tier.BenchmarkTileCount == benchmarkTileCount;
    }

    private static string ThresholdContractFailureReason(
        bool planningPassed,
        bool qualityPassed,
        bool experiencePassed,
        bool benchmarkPassed)
    {
        if (!planningPassed)
        {
            return "TerrainWorldPlanningThresholds.OpenWorldDefault drifted";
        }

        if (!qualityPassed)
        {
            return "TerrainQualityThresholds.OpenWorldDefault drifted";
        }

        if (!experiencePassed)
        {
            return "TerrainExperienceThresholds.OpenWorldDefault drifted";
        }

        if (!benchmarkPassed)
        {
            return "TerrainTileBenchmarkThresholds.Default drifted from TerrainPerformanceContract/TerrainDeterminismContract";
        }

        return "default terrain threshold contract failed";
    }

    private static string DefaultStateContractFailureReason(
        bool corridorNonePassed,
        bool corridorIndexEmptyPassed,
        bool poiIndexEmptyPassed,
        bool waterSurfaceEmptyPassed,
        bool planSnapshotEmptyPassed)
    {
        if (!corridorNonePassed)
        {
            return "TerrainRouteCorridorSample.None drifted";
        }

        if (!corridorIndexEmptyPassed)
        {
            return "TerrainRouteCorridorIndex.Empty drifted";
        }

        if (!poiIndexEmptyPassed)
        {
            return "TerrainPointOfInterestIndex.Empty drifted";
        }

        if (!waterSurfaceEmptyPassed)
        {
            return "TerrainWaterSurfaceData.Empty drifted";
        }

        if (!planSnapshotEmptyPassed)
        {
            return "TerrainWorldPlanSnapshot.Empty drifted";
        }

        return "default terrain state contract failed";
    }

    private static bool ExactFloatEquals(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= TerrainDeterminismContract.ExactFloatEpsilon;
    }

    private static bool ExactDoubleEquals(double expected, double actual)
    {
        return Math.Abs(expected - actual) <= TerrainDeterminismContract.ExactFloatEpsilon;
    }
}
