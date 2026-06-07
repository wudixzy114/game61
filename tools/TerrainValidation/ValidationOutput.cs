using System;
using Dao.Terrain;
using Dao.Terrain.Generation;

internal static class TerrainValidationOutput
{
    internal static void PrintSeedResult(TerrainValidationResult result, bool detailed)
    {
        TerrainQualityReport quality = result.QualityGate.Report;
        TerrainWorldPlanningReport planning = result.PlanningGate.Report;
        TerrainExperienceReport experience = result.ExperienceGate.Report;
        Console.WriteLine(
            $"Seed {result.Seed}: {(result.Passed ? "PASS" : "FAIL")} " +
            $"land {quality.LandRatio:0.000}, scenic {quality.ScenicRatio:0.000}, " +
            $"traversable {quality.TraversableLandRatio:0.000}, POIs {planning.PointOfInterestCount}, " +
            $"settlements V/T/O {planning.VillageCount}/{planning.TownCount}/{planning.OasisHubCount}, " +
            $"routes {planning.RouteCount}, connected {planning.ConnectedPointRatio:0.000}, " +
            $"settlement net {planning.ConnectedSettlementRatio:0.000}/{planning.SettlementRouteCount}, " +
            $"coverage {planning.PointOfInterestWorldCoverage:0.000}/{planning.RouteWorldCoverage:0.000}, " +
            $"encounter {experience.AverageEncounterPotential:0.000}, rhythm {experience.RouteRhythmScore:0.000}, " +
            $"experience {(result.ExperienceGate.Passed ? "PASS" : "FAIL")}, archetypes {(result.ArchetypeGate.Passed ? "PASS" : "FAIL")}");

        if (result.Passed && !detailed)
        {
            return;
        }

        Console.WriteLine($"World size: {result.Plan.WorldSize:0.##}");
        Console.WriteLine($"Planning grid: {result.Plan.GridResolution} x {result.Plan.GridResolution}");
        Console.WriteLine("Terrain quality gate:");
        Console.Write(result.QualityGate.Summary);
        Console.WriteLine($"Height range: {quality.MinHeight:0.0} to {quality.MaxHeight:0.0}");
        Console.WriteLine($"Land/scenic/traversable: {quality.LandRatio:0.000} / {quality.ScenicRatio:0.000} / {quality.TraversableLandRatio:0.000}");
        Console.WriteLine($"Landscape kinds: {quality.DistinctLandscapeKinds}");
        Console.WriteLine("Open world planning gate:");
        Console.Write(result.PlanningGate.Summary);
        Console.WriteLine($"POIs/routes: {planning.PointOfInterestCount} / {planning.RouteCount}");
        Console.WriteLine($"Villages/towns/oasis hubs: {planning.VillageCount} / {planning.TownCount} / {planning.OasisHubCount}");
        Console.WriteLine($"Connected point ratio: {planning.ConnectedPointRatio:0.000}");
        Console.WriteLine($"Connected settlement ratio / direct settlement routes: {planning.ConnectedSettlementRatio:0.000} / {planning.SettlementRouteCount}");
        Console.WriteLine($"World coverage POIs/routes: {planning.PointOfInterestWorldCoverage:0.000} / {planning.RouteWorldCoverage:0.000}");
        Console.WriteLine($"Average route scenic/traversability: {planning.AverageRouteScenicPotential:0.000} / {planning.AverageRouteTraversability:0.000}");
        Console.WriteLine("Open world experience gate:");
        Console.Write(result.ExperienceGate.Summary);
        Console.WriteLine($"Encounter/resource/hazard rich regions: {experience.EncounterRichRegionRatio:0.000} / {experience.ResourceRichRegionRatio:0.000} / {experience.HazardRichRegionRatio:0.000}");
        Console.WriteLine($"Average exposure/resource/hazard/encounter: {experience.AverageExposure:0.000} / {experience.AverageResourcePotential:0.000} / {experience.AverageHazardPotential:0.000} / {experience.AverageEncounterPotential:0.000}");
        Console.WriteLine($"Route rhythm / POI value / risk reward / scenic anchors: {experience.RouteRhythmScore:0.000} / {experience.PointOfInterestValue:0.000} / {experience.RiskRewardBalance:0.000} / {experience.ScenicAnchorRatio:0.000}");
        Console.WriteLine("Runtime archetype gate:");
        Console.WriteLine(result.ArchetypeGate.Summary);
    }

    internal static void PrintCorridorSmoke(TerrainRouteCorridorSmokeReport report)
    {
        Console.WriteLine(
            $"Route corridor tile smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"seed {report.Seed}, tile {report.Coord}, segments {report.SegmentCount}, " +
            $"influenced vertices {report.InfluencedVertexCount}, max height delta {report.MaxHeightDelta:0.000}, " +
            $"max color delta {report.MaxColorDelta:0.000}, segment snapshot {(report.SegmentSnapshotIsolated ? "pass" : "fail")} " +
            $"({report.Reason})");
    }

    internal static void PrintRouteScatterSmoke(TerrainRouteScatterSmokeReport report)
    {
        Console.WriteLine(
            $"Route scatter smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, " +
            $"road markers/bridges {report.RoadMarkerCount}/{report.BridgeSpanCount}, " +
            $"total {report.RouteLandmarkCount} ({report.Reason})");
    }

    internal static void PrintPoiTileSmoke(TerrainPoiTileSmokeReport report)
    {
        Console.WriteLine(
            $"POI tile landmark smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"materialized {report.MaterializedPointCount}/{report.ExpectedPointCount}, " +
            $"tiles {report.TileCount}, kinds {report.DistinctLandmarkKinds}/{report.DistinctScatterLandmarkKinds}, " +
            $"village/town/oasis hub landmarks {report.VillageLandmarkCount}/{report.TownLandmarkCount}/{report.OasisHubLandmarkCount}, " +
            $"scatter {report.VillageScatterCount}/{report.TownScatterCount}/{report.OasisHubScatterCount}, " +
            $"interior scatter {report.SettlementInteriorScatterCount}/{report.ExpectedSettlementPointCount}, " +
            $"interior kinds H/B/C/P/W {report.VillageHouseScatterCount}/{report.TownBlockScatterCount}/{report.OasisCanopyScatterCount}/{report.SettlementPlazaScatterCount}/{report.OasisPoolScatterCount}, " +
            $"services well/market/tower/garden {report.VillageWellScatterCount}/{report.MarketStallScatterCount}/{report.WatchTowerScatterCount}/{report.OasisGardenScatterCount}, " +
            $"gateways {report.SettlementGatewayScatterCount}/{report.ExpectedSettlementPointCount}, " +
            $"footprint vertices {report.FootprintInfluencedVertexCount}, max footprint delta {report.FootprintMaxHeightDelta:0.000}/{report.FootprintMaxColorDelta:0.000}, " +
            $"layout color vertices {report.LayoutColorVertexCount}, max layout color {report.LayoutMaxColorDelta:0.000}, " +
            $"landmark scatter {report.LandmarkScatterCount}, index snapshot {(report.PoiIndexSnapshotIsolated ? "pass" : "fail")} " +
            $"({report.Reason})");
    }

    internal static void PrintGameplayScatterSmoke(TerrainGameplayScatterSmokeReport report)
    {
        Console.WriteLine(
            $"Gameplay scatter smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, " +
            $"understory/resource/hazard {report.UnderstoryCount}/{report.ResourceNodeCount}/{report.HazardOutcropCount}, " +
            $"total {report.TotalGameplayScatterCount} ({report.Reason})");
    }

    internal static void PrintBiomeScatterSmoke(TerrainBiomeScatterSmokeReport report)
    {
        Console.WriteLine(
            $"Biome scatter smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, categories {report.MaterializedCategoryCount}/{report.RequiredCategoryCount}, " +
            $"grass/desert/cactus/reeds/snow/alpine/palms/driftwood/mangrove/lake reeds/lilies " +
            $"{report.GrassTuftCount}/{report.DesertShrubCount}/{report.CactusClusterCount}/{report.ReedClusterCount}/{report.SnowClumpCount}/{report.AlpinePineCount}/{report.CoastalPalmCount}/{report.DriftwoodCount}/{report.MangroveRootCount}/{report.LakeReedCount}/{report.WaterLilyCount}, " +
            $"water cells lake/river/oasis {report.LakeWaterCellCount}/{report.RiverWaterCellCount}/{report.OasisWaterCellCount}, " +
            $"total {report.BiomeScatterCount} ({report.Reason})");
    }

    internal static void PrintScenicLandmarkSmoke(TerrainScenicLandmarkSmokeReport report)
    {
        Console.WriteLine(
            $"Scenic landmark smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, " +
            $"waterfalls/dunes/monoliths/needles/ice/arches/springs/glacial " +
            $"{report.WaterfallCount}/{report.DuneCrestCount}/{report.DesertMonolithCount}/{report.CanyonNeedleCount}/{report.IceSpireCount}/{report.NaturalArchCount}/{report.GeothermalSpringCount}/{report.GlacialRidgeCount}, " +
            $"distinct {report.DistinctGeneratedKindCount}, scenic landmarks {report.ScenicLandmarkCount} ({report.Reason})");
    }

    internal static void PrintArtifactSmoke(TerrainArtifactSmokeReport report)
    {
        Console.WriteLine(
            $"Open world artifact smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"image {report.ImageSize} px, json {report.JsonBytes / 1024.0:0.0} KB, map {report.MapBytes / 1024.0:0.0} KB, traversal {report.TraversalCostMapBytes / 1024.0:0.0} KB, report {report.ReportBytes / 1024.0:0.0} KB, " +
            $"colors {report.DistinctColorBuckets}, overlay pixels {report.OverlayChangedPixels}, " +
            $"max overlay delta {report.MaxOverlayColorDelta:0.000}, raster snapshot {(report.MapRasterSnapshotIsolated ? "pass" : "fail")}, traversal colors/blocked {report.TraversalCostColorBuckets}/{report.TraversalCostBlockedSamples}, " +
            $"grid {report.TraversalCostGridSize}px finite/blocked {report.TraversalCostGridFiniteSamples}/{report.TraversalCostGridBlockedSamples}, " +
            $"snapshot {(report.TraversalCostGridSnapshotIsolated ? "pass" : "fail")}, sections {(report.ReportContainsRequiredSections ? "yes" : "no")} ({report.Reason})");
        Console.WriteLine($"Artifact paths: {report.JsonPath}, {report.MapPath}, {report.TraversalCostMapPath}, {report.ReportPath}");
    }

    internal static void PrintPlanJsonSmoke(TerrainPlanJsonSmokeReport report)
    {
        Console.WriteLine(
            $"Plan JSON roundtrip smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"json {report.JsonBytes / 1024.0:0.0} KB, file {report.FileBytes / 1024.0:0.0} KB, " +
            $"metadata/schema {(report.MetadataPassed ? "pass" : "fail")}/{(report.SchemaShapePassed ? "pass" : "fail")}, " +
            $"string/file {(report.StringLoadPassed && report.StringRoundtripMatches ? "pass" : "fail")}/{(report.FileLoadPassed && report.FileRoundtripMatches ? "pass" : "fail")}, " +
            $"compat api 1.0/1.1/1.2/1.3/1.4/1.5/1.6 {(report.LegacyApiVersionAccepted ? "pass" : "fail")}/{(report.PreviousApiVersionAccepted ? "pass" : "fail")}/{(report.ApiVersion12Accepted ? "pass" : "fail")}/{(report.ApiVersion13Accepted ? "pass" : "fail")}/{(report.ApiVersion14Accepted ? "pass" : "fail")}/{(report.ApiVersion15Accepted ? "pass" : "fail")}/{(report.ApiVersion16Accepted ? "pass" : "fail")}, " +
            $"drift seed/hash/version/enum {(report.SeedMismatchRejected ? "pass" : "fail")}/{(report.ProfileHashMismatchRejected ? "pass" : "fail")}/{(report.VersionDriftRejected ? "pass" : "fail")}/{(report.EnumNameDriftRejected && report.EnumValueDriftRejected ? "pass" : "fail")}, " +
            $"isolation/runtime {(report.RoundtripIsolationPassed ? "pass" : "fail")}/{(report.SetWorldPlanPassed ? "pass" : "fail")} ({report.Reason})");
    }

    internal static void PrintEnumContractSmoke(TerrainEnumContractSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain enum contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"types {report.CheckedTypeCount}, values {report.CheckedValueCount} ({report.Reason})");
    }

    internal static void PrintPublicApiShapeSmoke(TerrainPublicApiShapeSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain public API shape smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"types {report.CheckedTypeCount}, members {report.CheckedMemberCount} ({report.Reason})");
    }

    internal static void PrintProfileHashSmoke(TerrainProfileHashSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain profile hash smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"hash {report.Hash}, format {(report.FormatPassed ? "pass" : "fail")}, " +
            $"expected {(report.ExpectedHashPassed ? "pass" : "fail")}, " +
            $"fields {report.SensitiveFieldCount}/{report.ExpectedFieldCount} ({report.Reason})");
    }

    internal static void PrintRuntimeApiSmoke(TerrainRuntimeApiSmokeReport report)
    {
        Console.WriteLine(
            $"Runtime TerrainWorld API smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"field base/overlay/delta {(report.BaseFieldMatchesSampler ? "pass" : "fail")}/{(report.SampleFieldMatchesSampler ? "pass" : "fail")}/{(report.OverlayFieldDeltaPassed ? "pass" : "fail")}, " +
            $"surface base/overlay/delta {(report.BaseSurfaceMatchesSampler ? "pass" : "fail")}/{(report.SampleSurfaceMatchesSampler ? "pass" : "fail")}/{(report.OverlaySurfaceDeltaPassed ? "pass" : "fail")}, " +
            $"surface axes base/overlay {(report.BaseSurfacePositionAxesPassed ? "pass" : "fail")}/{(report.SurfacePositionAxesPassed ? "pass" : "fail")}, " +
            $"api {TerrainApiVersion.Contract}/{TerrainApiVersion.Version}/{(report.ApiVersionPassed ? "pass" : "fail")}, " +
            $"determinism {TerrainDeterminismContract.Contract}/{(report.DeterminismContractPassed ? "pass" : "fail")}, " +
            $"performance {TerrainPerformanceContract.Contract}/{TerrainPerformanceContract.TileBenchmarkHardwareBaseline}/{(report.PerformanceContractPassed ? "pass" : "fail")}, " +
            $"integration iface/signals {(report.IntegrationInterfacesPassed ? "pass" : "fail")}/{(report.SignalContractsPassed ? "pass" : "fail")}, " +
            $"plan empty/ready {(report.NoPlanTryGetPassed && report.NoPlanSnapshotPassed && report.EmptyPlanCollectionsPassed ? "pass" : "fail")}/{(report.PlanTryGetPassed && report.PlanSnapshotTryGetPassed ? "pass" : "fail")}, " +
            $"POIs/routes {report.PointOfInterestCount}/{report.RouteCount}, " +
            $"traversable base/overlay {(report.BaseTraversabilityQueryPassed ? "pass" : "fail")}/{(report.TraversabilityQueryPassed ? "pass" : "fail")}, " +
            $"water base/overlay {(report.BaseAboveWaterQueryPassed ? "pass" : "fail")}/{(report.AboveWaterQueryPassed ? "pass" : "fail")}, " +
            $"semantic POI/POIsum/tagreg/route/routesum/corridor water-base/water tags-base/tags traversal-base/traversal {(report.PointQueryPassed ? "pass" : "fail")}/{(report.PointSummaryQueryPassed ? "pass" : "fail")}/{(report.GameplayTagRegionQueryPassed ? "pass" : "fail")}/{(report.RouteQueryPassed ? "pass" : "fail")}/{(report.RouteSummaryQueryPassed ? "pass" : "fail")}/{(report.RouteCorridorQueryPassed ? "pass" : "fail")}/{(report.BaseWaterStateQueryPassed ? "pass" : "fail")}/{(report.WaterStateQueryPassed ? "pass" : "fail")}/{(report.BaseGameplayTagsQueryPassed ? "pass" : "fail")}/{(report.GameplayTagsQueryPassed ? "pass" : "fail")}/{(report.BaseTraversalCostQueryPassed ? "pass" : "fail")}/{(report.TraversalCostQueryPassed ? "pass" : "fail")}, " +
            $"placement/nav {(report.PlacementCandidatesQueryPassed ? "pass" : "fail")}/{(report.RoutePlacementQueryPassed ? "pass" : "fail")}/{(report.NavigationGridPassed ? "pass" : "fail")}/{(report.NavigationTileGridPassed ? "pass" : "fail")}/{(report.ModifiedNavigationTileGridPassed ? "pass" : "fail")}/{(report.NavigationRegionQueryPassed ? "pass" : "fail")}/{(report.ModifiedNavigationRegionQueryPassed ? "pass" : "fail")}/{(report.NavigationWaypointGraphPassed ? "pass" : "fail")}/{(report.NavigationWaypointGraphIsolated ? "pass" : "fail")}/{(report.NoPlanRoutePathPassed ? "pass" : "fail")}/{(report.RouteGraphSnapshotPassed ? "pass" : "fail")}/{(report.ModifiedRouteGraphPassed ? "pass" : "fail")}/{(report.RoutePathQueryPassed ? "pass" : "fail")}/{(report.RouteGraphSnapshotIsolated ? "pass" : "fail")}, " +
            $"streaming {(report.StreamingSnapshotPassed ? "pass" : "fail")}, " +
            $"snapshots POI/routes/plan {(report.PointSnapshotIsolated ? "pass" : "fail")}/{(report.RouteSnapshotIsolated ? "pass" : "fail")}/{(report.WorldPlanSnapshotIsolated ? "pass" : "fail")} " +
            $"({report.Reason})");
    }

    internal static void PrintAnchorContractSmoke(TerrainAnchorContractSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain anchor contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"POIs/routes {report.PointAnchorCount}/{report.RouteAnchorCount}, " +
            $"counts {(report.PointCountPassed ? "pass" : "fail")}/{(report.RouteCountPassed ? "pass" : "fail")}, " +
            $"groups/meta {(report.PoiGroupMetaPassed ? "pass" : "fail")}/{(report.RouteGroupMetaPassed ? "pass" : "fail")}, " +
            $"names {(report.PoiContractNamesPassed ? "pass" : "fail")}/{(report.RouteContractNamesPassed ? "pass" : "fail")}, " +
            $"waypoints/rebuild/builder/overlay/meta-keys/constants {(report.RouteWaypointSnapshotPassed ? "pass" : "fail")}/{(report.DescriptorRebuildPassed ? "pass" : "fail")}/{(report.BuilderPlanSnapshotPassed ? "pass" : "fail")}/{(report.OverlayPlanSnapshotPassed ? "pass" : "fail")}/{(report.MetaKeySnapshotPassed ? "pass" : "fail")}/{(report.AnchorNodeConstantsPassed ? "pass" : "fail")} " +
            $"({report.Reason})");
    }

    internal static void PrintRuntimeWorldSmoke(TerrainRuntimeWorldSmokeReport report)
    {
        Console.WriteLine(
            $"Runtime TerrainWorld smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"POIs {report.MaterializedPointCount}/{report.PointOfInterestCount}, routes {report.RouteCount}, tiles {report.SampledTileCount}, " +
            $"async {report.AsyncPlanMilliseconds:0.0} ms/{(report.AsyncPlanMatchesSync ? "match" : "mismatch")}, " +
            $"cancel {report.AsyncPlanCancellationMilliseconds:0.0} ms/{(report.AsyncPlanCancellationPassed ? "pass" : "fail")}, " +
            $"indices route/POI {(report.HasCorridorIndex ? "yes" : "no")}/{(report.HasPointIndex ? "yes" : "no")}, " +
            $"markers/bridges {report.RoadMarkerCount}/{report.BridgeSpanCount}, settlement scatter {report.SettlementInteriorScatterCount}, " +
            $"gates Q/P/E/A {(report.QualityGatePassed ? "pass" : "fail")}/{(report.PlanningGatePassed ? "pass" : "fail")}/{(report.ExperienceGatePassed ? "pass" : "fail")}/{(report.ArchetypeGatePassed ? "pass" : "fail")}, " +
            $"mod query/invalidation {(report.ModificationQueryConsistencyPassed ? "pass" : "fail")}/{(report.ModificationInvalidationPassed ? "pass" : "fail")}, " +
            $"set-plan invalidation {(report.SetWorldPlanInvalidationPassed ? "pass" : "fail")} " +
            $"({report.Reason})");
    }

    internal static void PrintNativeSamplerSmoke(TerrainNativeSamplerSmokeReport report)
    {
        Console.WriteLine(
            $"Native sampler smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"seed {report.Seed}, profile {report.ProfileHash}, " +
            $"available {report.Available}, tile {report.Coord}, resolution {report.Resolution}, " +
            $"field v2 {report.FieldGridAvailable}/{report.FieldGridContainsDerivedData}, " +
            $"samples {report.ComparedSampleCount}, max delta {report.MaxHeightDelta:0.000}, " +
            $"avg delta {report.AverageHeightDelta:0.000}, field values {report.ComparedFieldValueCount}, " +
            $"field delta {report.MaxFieldDelta:0.000}/{report.AverageFieldDelta:0.000}, " +
            $"field class mismatches {report.FieldClassificationMismatchCount}, tile vertices {report.TileVertexCount}, " +
            $"tile delta {report.TileMaxHeightDelta:0.000}/{report.TileMaxColorDelta:0.000} ({report.Reason})");
    }

    internal static void PrintTileBenchmark(TerrainTileBenchmarkReport report)
    {
        Console.WriteLine(
            $"Tile generation benchmark: {(report.Passed ? "PASS" : "FAIL")} native available/selected {report.NativeAvailable}/{report.NativeSelectedForTileGeneration}, " +
            $"seed {report.Seed}, profile {report.ProfileHash}, modes {report.ManagedBackendMode}/{report.NativeBackendMode}, " +
            $"tiles {report.MeasuredTileCount}/{report.RequestedTileCount}, " +
            $"passes {report.MeasurementPassCount}, native speedup {report.NativeSpeedup:0.00}x, parity tiles {report.ParityTileCount}, " +
            $"max parity delta {report.MaxHeightDelta:0.000}/{report.MaxColorDelta:0.000} ({report.Reason})");
        Console.WriteLine(
            $"Benchmark thresholds ({TerrainPerformanceContract.Contract}/{TerrainPerformanceContract.TileBenchmarkHardwareBaseline}): managed <= {report.Thresholds.MaxManagedMillisecondsPerTile:0.00} ms/tile, " +
            $"native <= {report.Thresholds.MaxNativeMillisecondsPerTile:0.00} ms/tile, " +
            $"managed p50/p95/p99 <= {report.Thresholds.MaxManagedP50Milliseconds:0.00}/{report.Thresholds.MaxManagedP95Milliseconds:0.00}/{report.Thresholds.MaxManagedP99Milliseconds:0.00} ms, " +
            $"native p50/p95/p99 <= {report.Thresholds.MaxNativeP50Milliseconds:0.00}/{report.Thresholds.MaxNativeP95Milliseconds:0.00}/{report.Thresholds.MaxNativeP99Milliseconds:0.00} ms, " +
            $"alloc <= {report.Thresholds.MaxAllocatedKilobytesPerTile:0.0} KB/tile, " +
            $"speedup >= {report.Thresholds.MinNativeSpeedup:0.00}x");
        PrintTileBenchmarkPass("Managed", report.Managed);
        if (report.NativeAvailable)
        {
            PrintTileBenchmarkPass(report.NativeSelectedForTileGeneration ? "Native" : "Native-enabled adaptive", report.Native);
        }
    }

    internal static void PrintTileBenchmarkPass(string label, TerrainTileBenchmarkPass pass)
    {
        Console.WriteLine(
            $"{label} tile build: {pass.TileCount} tiles in {pass.ElapsedMilliseconds:0.0} ms, " +
            $"{pass.TilesPerSecond:0.0} tiles/s, {pass.MillisecondsPerTile:0.00} ms/tile, " +
            $"p50/p95/p99 {pass.P50Milliseconds:0.00}/{pass.P95Milliseconds:0.00}/{pass.P99Milliseconds:0.00} ms, " +
            $"alloc {pass.AllocatedMegabytes:0.00} MB ({pass.AllocatedKilobytesPerTile:0.0} KB/tile), " +
            $"vertices {pass.TotalVertices}, scatter {pass.TotalScatter}, landmarks {pass.TotalLandmarks}");
    }

    internal static void PrintValidationCliContractSmoke(TerrainValidationCliContractSmokeReport report)
    {
        Console.WriteLine(
            $"Validation CLI contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"tiers/fixed/custom {report.TierSelectionPassed}/{report.FixedTierConfigurationPassed}/{report.CustomFallbackPassed}, " +
            $"reject skip/seed/world/native/smoke-all/benchmark/unknown {report.SkipOverrideRejected}/{report.SeedOverrideRejected}/" +
            $"{report.WorldOverrideRejected}/{report.NativeOverrideRejected}/{report.SmokeAllSeedsOverrideRejected}/" +
            $"{report.BenchmarkOverrideRejected}/{report.UnknownTierRejected} " +
            $"({report.Reason})");
    }

    internal static void PrintThresholdContractSmoke(TerrainThresholdContractSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain threshold contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"planning/quality/experience/benchmark {report.PlanningThresholdsPassed}/{report.QualityThresholdsPassed}/{report.ExperienceThresholdsPassed}/{report.BenchmarkThresholdsPassed} " +
            $"({report.Reason})");
    }

    internal static void PrintDefaultStateContractSmoke(TerrainDefaultStateContractSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain default state contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"corridor-none/index-empty/poi-empty/water-empty/plan-empty " +
            $"{report.CorridorNonePassed}/{report.CorridorIndexEmptyPassed}/{report.PointOfInterestIndexEmptyPassed}/" +
            $"{report.WaterSurfaceEmptyPassed}/{report.PlanSnapshotEmptyPassed} " +
            $"({report.Reason})");
    }

    internal static void PrintBenchmarkArtifactSmoke(TerrainBenchmarkArtifactSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain benchmark artifact smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"schema/file/roundtrip {report.JsonSchemaPassed}/{report.FileSavePassed}/{report.FileRoundtripPassed}, " +
            $"json {report.JsonBytes / 1024.0:0.0} KB ({report.Reason})");
        if (!string.IsNullOrWhiteSpace(report.JsonPath))
        {
            Console.WriteLine($"Benchmark artifact smoke path: {report.JsonPath}");
        }
    }

    internal static void PrintApiLayeringSmoke(TerrainApiLayeringSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain API layering smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"files {report.ScannedFileCount}, violations {report.ViolationCount} ({report.Reason})");

        foreach (string violation in report.Violations)
        {
            Console.WriteLine($"  {violation}");
        }
    }

    internal static void PrintEditorPluginSmoke(TerrainEditorPluginSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain editor plugin smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"cfg/script/panel/settings/catalog/scene {report.PluginConfigExists}/{report.PluginScriptExists}/{report.DockPanelExists}/{report.DefaultSettingsResourceExists}/{report.DefaultVisualCatalogResourceExists}/{report.MainSceneExists}, " +
            $"wiring cfg/dock/panel/scene-settings/scene-catalog/demo-settings/demo-catalog/default {report.PluginConfigWiresScript}/{report.PluginScriptWiresDock}/{report.DockPanelSupportsPreviewExportValidation}/{report.MainSceneWiresDefaultSettings}/{report.MainSceneWiresDefaultVisualCatalog}/{report.DemoScriptSupportsSettingsResource}/{report.DemoScriptSupportsVisualCatalogResource}/{report.DockPanelUsesDefaultSettingsResource} " +
            $"({report.Reason})");
    }

    internal static void PrintVisualCatalogSmoke(TerrainVisualCatalogSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain visual catalog smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"mesh/scene {report.MeshEntryLookupPassed}/{report.SceneEntryLookupPassed}, " +
            $"scene-only {report.SceneOnlyEntriesAreAccepted}, missing {report.MissingEntryDetectionPassed}, " +
            $"report/resources {report.ValidationReportPassed}/{report.ReferencedResourceCollectionPassed}, " +
            $"metadata/runtime {report.VisualEntryMetadataPassed}/{report.RuntimeScenePathPassed} " +
            $"({report.Reason})");
    }

    internal static void PrintModificationLayerSmoke(TerrainModificationLayerSmokeReport report)
    {
        Console.WriteLine(
            $"Terrain modification layer smoke: {(report.Passed ? "PASS" : "FAIL")} " +
            $"field/tiles/route/scatter/landmark/collision {report.FieldOverlayPassed}/{report.AffectedTilesPassed}/{report.RouteStatePassed}/{report.ScatterMaterializationPassed}/{report.LandmarkMaterializationPassed}/{report.CollisionMaterializationPassed}, " +
            $"json/file {report.JsonRoundtripPassed}/{report.FileRoundtripPassed}, " +
            $"isolation/drift {report.SnapshotIsolationPassed}/{report.DriftRejectionPassed}, " +
            $"scatterdiag {report.ScatterDiagnostic} " +
            $"({report.Reason})");
    }

    internal static void PrintAggregate(
        TerrainValidationAggregate aggregate,
        int seedCount,
        int seedFailures,
        int totalFailures,
        int auxiliaryCheckCount,
        int auxiliaryFailureCount,
        TerrainRouteCorridorSmokeReport? corridorSmokeReport,
        TerrainRouteScatterSmokeReport? routeScatterSmokeReport,
        TerrainPoiTileSmokeReport? poiTileSmokeReport,
        TerrainGameplayScatterSmokeReport? gameplayScatterSmokeReport,
        TerrainBiomeScatterSmokeReport? biomeScatterSmokeReport,
        TerrainScenicLandmarkSmokeReport? scenicLandmarkSmokeReport,
        TerrainArtifactSmokeReport? artifactSmokeReport,
        TerrainPlanJsonSmokeReport? planJsonSmokeReport,
        TerrainEnumContractSmokeReport? enumContractSmokeReport,
        TerrainPublicApiShapeSmokeReport? publicApiShapeSmokeReport,
        TerrainProfileHashSmokeReport? profileHashSmokeReport,
        TerrainValidationCliContractSmokeReport? validationCliContractSmokeReport,
        TerrainThresholdContractSmokeReport? thresholdContractSmokeReport,
        TerrainDefaultStateContractSmokeReport? defaultStateContractSmokeReport,
        TerrainBenchmarkArtifactSmokeReport? benchmarkArtifactSmokeReport,
        TerrainApiLayeringSmokeReport? apiLayeringSmokeReport,
        TerrainEditorPluginSmokeReport? editorPluginSmokeReport,
        TerrainVisualCatalogSmokeReport? visualCatalogSmokeReport,
        TerrainModificationLayerSmokeReport? modificationLayerSmokeReport,
        TerrainRuntimeApiSmokeReport? runtimeApiSmokeReport,
        TerrainAnchorContractSmokeReport? anchorSmokeReport,
        TerrainRuntimeWorldSmokeReport? runtimeWorldSmokeReport,
        TerrainNativeSamplerSmokeReport? nativeSmokeReport,
        TerrainTileBenchmarkReport? tileBenchmarkReport)
    {
        Console.WriteLine();
        Console.WriteLine($"Open world terrain validation: {(seedFailures == 0 ? "PASS" : "FAIL")} ({seedCount - seedFailures}/{seedCount} seeds passed)");
        if (auxiliaryCheckCount > 0)
        {
            Console.WriteLine($"Auxiliary checks: {(auxiliaryFailureCount == 0 ? "PASS" : "FAIL")} ({auxiliaryCheckCount - auxiliaryFailureCount}/{auxiliaryCheckCount} checks passed)");
        }

        Console.WriteLine($"Overall validation: {(totalFailures == 0 ? "PASS" : "FAIL")}");
        Console.WriteLine($"Land ratio min/avg/max: {aggregate.MinLandRatio:0.000} / {aggregate.AverageLandRatio:0.000} / {aggregate.MaxLandRatio:0.000}");
        Console.WriteLine($"Scenic ratio min/avg/max: {aggregate.MinScenicRatio:0.000} / {aggregate.AverageScenicRatio:0.000} / {aggregate.MaxScenicRatio:0.000}");
        Console.WriteLine($"Traversable land min/avg/max: {aggregate.MinTraversableLandRatio:0.000} / {aggregate.AverageTraversableLandRatio:0.000} / {aggregate.MaxTraversableLandRatio:0.000}");
        Console.WriteLine($"POI count min/avg/max: {aggregate.MinPoiCount} / {aggregate.AveragePoiCount:0.0} / {aggregate.MaxPoiCount}");
        Console.WriteLine($"Route count min/avg/max: {aggregate.MinRouteCount} / {aggregate.AverageRouteCount:0.0} / {aggregate.MaxRouteCount}");
        Console.WriteLine($"Connected ratio min/avg/max: {aggregate.MinConnectedPointRatio:0.000} / {aggregate.AverageConnectedPointRatio:0.000} / {aggregate.MaxConnectedPointRatio:0.000}");
        Console.WriteLine($"Connected settlement ratio min/avg/max: {aggregate.MinConnectedSettlementRatio:0.000} / {aggregate.AverageConnectedSettlementRatio:0.000} / {aggregate.MaxConnectedSettlementRatio:0.000}");
        Console.WriteLine($"Settlement routes min/avg/max: {aggregate.MinSettlementRouteCount} / {aggregate.AverageSettlementRouteCount:0.0} / {aggregate.MaxSettlementRouteCount}");
        Console.WriteLine($"POI coverage min/avg/max: {aggregate.MinPointOfInterestWorldCoverage:0.000} / {aggregate.AveragePointOfInterestWorldCoverage:0.000} / {aggregate.MaxPointOfInterestWorldCoverage:0.000}");
        Console.WriteLine($"Route coverage min/avg/max: {aggregate.MinRouteWorldCoverage:0.000} / {aggregate.AverageRouteWorldCoverage:0.000} / {aggregate.MaxRouteWorldCoverage:0.000}");
        Console.WriteLine($"Route scenic min/avg/max: {aggregate.MinRouteScenicPotential:0.000} / {aggregate.AverageRouteScenicPotential:0.000} / {aggregate.MaxRouteScenicPotential:0.000}");
        Console.WriteLine($"Route traversability min/avg/max: {aggregate.MinRouteTraversability:0.000} / {aggregate.AverageRouteTraversability:0.000} / {aggregate.MaxRouteTraversability:0.000}");
        Console.WriteLine($"Village count min/avg/max: {aggregate.MinVillageCount} / {aggregate.AverageVillageCount:0.0} / {aggregate.MaxVillageCount}");
        Console.WriteLine($"Town count min/avg/max: {aggregate.MinTownCount} / {aggregate.AverageTownCount:0.0} / {aggregate.MaxTownCount}");
        Console.WriteLine($"Oasis hub count min/avg/max: {aggregate.MinOasisHubCount} / {aggregate.AverageOasisHubCount:0.0} / {aggregate.MaxOasisHubCount}");
        Console.WriteLine($"Encounter potential min/avg/max: {aggregate.MinEncounterPotential:0.000} / {aggregate.AverageEncounterPotential:0.000} / {aggregate.MaxEncounterPotential:0.000}");
        Console.WriteLine($"Route rhythm min/avg/max: {aggregate.MinRouteRhythmScore:0.000} / {aggregate.AverageRouteRhythmScore:0.000} / {aggregate.MaxRouteRhythmScore:0.000}");
        Console.WriteLine($"Risk reward min/avg/max: {aggregate.MinRiskRewardBalance:0.000} / {aggregate.AverageRiskRewardBalance:0.000} / {aggregate.MaxRiskRewardBalance:0.000}");
        Console.WriteLine($"Experience readiness: {(aggregate.ExperienceFailureCount == 0 ? "PASS" : "FAIL")} ({seedCount - aggregate.ExperienceFailureCount}/{seedCount} seeds passed)");
        Console.WriteLine($"Runtime archetype readiness: {(aggregate.ArchetypeFailureCount == 0 ? "PASS" : "FAIL")} ({seedCount - aggregate.ArchetypeFailureCount}/{seedCount} seeds covered)");
        if (corridorSmokeReport is not null)
        {
            Console.WriteLine($"Route corridor tile smoke: {(corridorSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (routeScatterSmokeReport is not null)
        {
            Console.WriteLine($"Route scatter smoke: {(routeScatterSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (poiTileSmokeReport is not null)
        {
            Console.WriteLine($"POI tile landmark smoke: {(poiTileSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (gameplayScatterSmokeReport is not null)
        {
            Console.WriteLine($"Gameplay scatter smoke: {(gameplayScatterSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (biomeScatterSmokeReport is not null)
        {
            Console.WriteLine($"Biome scatter smoke: {(biomeScatterSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (scenicLandmarkSmokeReport is not null)
        {
            Console.WriteLine($"Scenic landmark smoke: {(scenicLandmarkSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (artifactSmokeReport is not null)
        {
            Console.WriteLine($"Open world artifact smoke: {(artifactSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (planJsonSmokeReport is not null)
        {
            Console.WriteLine($"Plan JSON roundtrip smoke: {(planJsonSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (enumContractSmokeReport is not null)
        {
            Console.WriteLine($"Terrain enum contract smoke: {(enumContractSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (publicApiShapeSmokeReport is not null)
        {
            Console.WriteLine($"Terrain public API shape smoke: {(publicApiShapeSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (profileHashSmokeReport is not null)
        {
            Console.WriteLine($"Terrain profile hash smoke: {(profileHashSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (validationCliContractSmokeReport is not null)
        {
            Console.WriteLine($"Validation CLI contract smoke: {(validationCliContractSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (thresholdContractSmokeReport is not null)
        {
            Console.WriteLine($"Terrain threshold contract smoke: {(thresholdContractSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (defaultStateContractSmokeReport is not null)
        {
            Console.WriteLine($"Terrain default state contract smoke: {(defaultStateContractSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (benchmarkArtifactSmokeReport is not null)
        {
            Console.WriteLine($"Terrain benchmark artifact smoke: {(benchmarkArtifactSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (apiLayeringSmokeReport is not null)
        {
            Console.WriteLine($"Terrain API layering smoke: {(apiLayeringSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (editorPluginSmokeReport is not null)
        {
            Console.WriteLine($"Terrain editor plugin smoke: {(editorPluginSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (visualCatalogSmokeReport is not null)
        {
            Console.WriteLine($"Terrain visual catalog smoke: {(visualCatalogSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (modificationLayerSmokeReport is not null)
        {
            Console.WriteLine($"Terrain modification layer smoke: {(modificationLayerSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (runtimeApiSmokeReport is not null)
        {
            Console.WriteLine($"Runtime TerrainWorld API smoke: {(runtimeApiSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (anchorSmokeReport is not null)
        {
            Console.WriteLine($"Terrain anchor contract smoke: {(anchorSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (runtimeWorldSmokeReport is not null)
        {
            Console.WriteLine($"Runtime TerrainWorld smoke: {(runtimeWorldSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (nativeSmokeReport is not null)
        {
            Console.WriteLine($"Native sampler smoke: {(nativeSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
        }

        if (tileBenchmarkReport is not null)
        {
            Console.WriteLine(
                $"Tile generation benchmark: {(tileBenchmarkReport.Value.Passed ? "PASS" : "FAIL")} managed {tileBenchmarkReport.Value.Managed.MillisecondsPerTile:0.00} ms/tile, " +
                $"native {(tileBenchmarkReport.Value.NativeAvailable ? tileBenchmarkReport.Value.Native.MillisecondsPerTile.ToString("0.00") : "n/a")} ms/tile");
        }
    }
}
