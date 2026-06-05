using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Dao.Terrain.Runtime;
using Dao.Terrain.Streaming;
using Godot;

TerrainGenerationProfile profile = CreateDemoProfile();
TerrainValidationTierSpec validationTier = ParseValidationTier(args, out string validationTierError);
if (!string.IsNullOrEmpty(validationTierError))
{
    Console.Error.WriteLine(validationTierError);
    return 2;
}

float worldSize = GetFloatArg(args, "--world-size", 12_288.0f);
int seed = GetIntArg(args, "--seed", profile.Seed);
int seedStep = Math.Max(1, GetIntArg(args, "--seed-step", 10_007));
bool verbose = HasFlag(args, "--verbose");
bool skipCorridorSmoke = HasFlag(args, "--skip-corridor-smoke");
bool skipRouteScatterSmoke = HasFlag(args, "--skip-route-scatter-smoke");
bool skipPoiTileSmoke = HasFlag(args, "--skip-poi-tile-smoke");
bool skipGameplayScatterSmoke = HasFlag(args, "--skip-gameplay-scatter-smoke");
bool skipBiomeScatterSmoke = HasFlag(args, "--skip-biome-scatter-smoke");
bool skipScenicLandmarkSmoke = HasFlag(args, "--skip-scenic-landmark-smoke");
bool skipArtifactSmoke = HasFlag(args, "--skip-artifact-smoke");
bool skipPlanJsonSmoke = HasFlag(args, "--skip-plan-json-smoke");
bool skipEnumContractSmoke = HasFlag(args, "--skip-enum-contract-smoke");
bool skipRuntimeApiSmoke = HasFlag(args, "--skip-runtime-api-smoke");
bool skipAnchorSmoke = HasFlag(args, "--skip-anchor-smoke");
bool skipRuntimeWorldSmoke = HasFlag(args, "--skip-runtime-world-smoke");
int seedCount = validationTier.IsCustom
    ? Math.Max(1, GetIntArg(args, "--seed-count", 1))
    : validationTier.SeedCount;
bool smokeAllSeeds = validationTier.IsCustom
    ? HasFlag(args, "--smoke-all-seeds")
    : validationTier.SmokeAllSeeds;
bool nativeSmoke = validationTier.IsCustom
    ? HasFlag(args, "--native-smoke")
    : validationTier.NativeSmoke;
bool benchmarkTiles = validationTier.IsCustom
    ? HasFlag(args, "--benchmark-tiles")
    : validationTier.BenchmarkTiles;
int benchmarkTileCount = validationTier.IsCustom
    ? Math.Max(1, GetIntArg(args, "--benchmark-tile-count", 48))
    : validationTier.BenchmarkTileCount;
int artifactImageSize = Math.Clamp(GetIntArg(args, "--artifact-image-size", 256), 64, 2048);
string? artifactOutputDirectoryArg = GetArg(args, "--artifact-output-dir");
string artifactOutputDirectory = artifactOutputDirectoryArg ??
    (smokeAllSeeds && seedCount > 1
        ? DefaultBatchArtifactOutputDirectory(seed, seedCount)
        : DefaultArtifactOutputDirectory(seed));

int seedFailures = 0;
int totalFailures = 0;
int auxiliaryCheckCount = 0;
int auxiliaryFailureCount = 0;
TerrainValidationAggregate aggregate = new();
TerrainRouteCorridorSmokeReport? corridorSmokeReport = null;
TerrainRouteScatterSmokeReport? routeScatterSmokeReport = null;
TerrainPoiTileSmokeReport? poiTileSmokeReport = null;
TerrainGameplayScatterSmokeReport? gameplayScatterSmokeReport = null;
TerrainBiomeScatterSmokeReport? biomeScatterSmokeReport = null;
TerrainScenicLandmarkSmokeReport? scenicLandmarkSmokeReport = null;
TerrainArtifactSmokeReport? artifactSmokeReport = null;
TerrainPlanJsonSmokeReport? planJsonSmokeReport = null;
TerrainEnumContractSmokeReport? enumContractSmokeReport = null;
TerrainPublicApiShapeSmokeReport? publicApiShapeSmokeReport = null;
TerrainProfileHashSmokeReport? profileHashSmokeReport = null;
TerrainValidationCliContractSmokeReport? validationCliContractSmokeReport = null;
TerrainThresholdContractSmokeReport? thresholdContractSmokeReport = null;
TerrainDefaultStateContractSmokeReport? defaultStateContractSmokeReport = null;
TerrainRuntimeApiSmokeReport? runtimeApiSmokeReport = null;
TerrainAnchorContractSmokeReport? anchorSmokeReport = null;
TerrainRuntimeWorldSmokeReport? runtimeWorldSmokeReport = null;
TerrainNativeSamplerSmokeReport? nativeSmokeReport = null;
TerrainTileBenchmarkReport? tileBenchmarkReport = null;
TerrainGenerationProfile benchmarkProfile = profile with { Seed = seed };
TerrainWorldPlan? benchmarkPlan = null;
const int TileBenchmarkMeasurementPasses = 5;

PrintValidationTier(validationTier, seedCount, smokeAllSeeds, nativeSmoke, benchmarkTiles, benchmarkTileCount);

for (int i = 0; i < seedCount; i++)
{
    TerrainGenerationProfile seedProfile = profile with { Seed = seed + i * seedStep };
    TerrainValidationResult result = ValidateSeed(seedProfile, worldSize);
    if (i == 0)
    {
        benchmarkProfile = seedProfile;
        benchmarkPlan = result.Plan;
    }

    aggregate.Add(result);

    if (!result.Passed)
    {
        seedFailures++;
        totalFailures++;
    }

    PrintSeedResult(result, seedCount == 1 || verbose);
    bool runSeedSmokes = i == 0 || smokeAllSeeds;

    if (runSeedSmokes && !skipCorridorSmoke)
    {
        corridorSmokeReport = ValidateRouteCorridorTileEffect(seedProfile, result.Plan);
        PrintCorridorSmoke(corridorSmokeReport.Value);
        RecordAuxiliaryCheck(corridorSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipRouteScatterSmoke)
    {
        routeScatterSmokeReport = ValidateRouteScatterMaterialization(seedProfile, result.Plan);
        PrintRouteScatterSmoke(routeScatterSmokeReport.Value);
        RecordAuxiliaryCheck(routeScatterSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipPoiTileSmoke)
    {
        poiTileSmokeReport = ValidatePoiTileMaterialization(seedProfile, result.Plan);
        PrintPoiTileSmoke(poiTileSmokeReport.Value);
        RecordAuxiliaryCheck(poiTileSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipGameplayScatterSmoke)
    {
        gameplayScatterSmokeReport = ValidateGameplayScatterMaterialization(seedProfile, result.Plan);
        PrintGameplayScatterSmoke(gameplayScatterSmokeReport.Value);
        RecordAuxiliaryCheck(gameplayScatterSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipBiomeScatterSmoke)
    {
        biomeScatterSmokeReport = ValidateBiomeScatterMaterialization(seedProfile, result.Plan);
        PrintBiomeScatterSmoke(biomeScatterSmokeReport.Value);
        RecordAuxiliaryCheck(biomeScatterSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipScenicLandmarkSmoke)
    {
        scenicLandmarkSmokeReport = ValidateScenicLandmarkMaterialization(seedProfile, result.Plan);
        PrintScenicLandmarkSmoke(scenicLandmarkSmokeReport.Value);
        RecordAuxiliaryCheck(scenicLandmarkSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipArtifactSmoke)
    {
        string seedArtifactOutputDirectory = ArtifactOutputDirectoryForSeed(
            artifactOutputDirectory,
            seedProfile.Seed,
            smokeAllSeeds && seedCount > 1);
        artifactSmokeReport = ValidateOpenWorldArtifactExport(seedProfile, result.Plan, seedArtifactOutputDirectory, artifactImageSize);
        PrintArtifactSmoke(artifactSmokeReport.Value);
        RecordAuxiliaryCheck(artifactSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipPlanJsonSmoke)
    {
        planJsonSmokeReport = ValidateTerrainPlanJsonRoundtrip(seedProfile, result.Plan);
        PrintPlanJsonSmoke(planJsonSmokeReport.Value);
        RecordAuxiliaryCheck(planJsonSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipRuntimeApiSmoke)
    {
        runtimeApiSmokeReport = ValidateTerrainWorldRuntimeApiFacade(seedProfile, result.Plan);
        PrintRuntimeApiSmoke(runtimeApiSmokeReport.Value);
        RecordAuxiliaryCheck(runtimeApiSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipAnchorSmoke)
    {
        anchorSmokeReport = ValidateTerrainAnchorContract(seedProfile, result.Plan);
        PrintAnchorContractSmoke(anchorSmokeReport.Value);
        RecordAuxiliaryCheck(anchorSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (runSeedSmokes && !skipRuntimeWorldSmoke)
    {
        runtimeWorldSmokeReport = ValidateRuntimeWorldPlanMaterialization(seedProfile, worldSize);
        PrintRuntimeWorldSmoke(runtimeWorldSmokeReport.Value);
        RecordAuxiliaryCheck(runtimeWorldSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }

    if (i == 0)
    {
        // Run native parity and tile benchmark immediately after the benchmark seed is ready,
        // before the remaining multi-seed sweep adds more thermal/noise variance.
        if (nativeSmoke)
        {
            nativeSmokeReport = ValidateNativeSamplerParity(benchmarkProfile);
            PrintNativeSamplerSmoke(nativeSmokeReport.Value);
            RecordAuxiliaryCheck(nativeSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
        }

        if (benchmarkTiles && benchmarkPlan is not null)
        {
            tileBenchmarkReport = BenchmarkTerrainTiles(benchmarkProfile, benchmarkPlan, benchmarkTileCount);
            PrintTileBenchmark(tileBenchmarkReport.Value);
            RecordAuxiliaryCheck(tileBenchmarkReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
        }
    }
}

if (!skipEnumContractSmoke)
{
    enumContractSmokeReport = ValidateTerrainEnumContracts();
    PrintEnumContractSmoke(enumContractSmokeReport.Value);
    RecordAuxiliaryCheck(enumContractSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);

    publicApiShapeSmokeReport = ValidateTerrainPublicApiShapeContracts();
    PrintPublicApiShapeSmoke(publicApiShapeSmokeReport.Value);
    RecordAuxiliaryCheck(publicApiShapeSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
}

profileHashSmokeReport = ValidateTerrainProfileHashContract(profile);
PrintProfileHashSmoke(profileHashSmokeReport.Value);
RecordAuxiliaryCheck(profileHashSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);

validationCliContractSmokeReport = ValidateValidationCliContract();
PrintValidationCliContractSmoke(validationCliContractSmokeReport.Value);
RecordAuxiliaryCheck(validationCliContractSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);

thresholdContractSmokeReport = ValidateTerrainDefaultThresholdContracts();
PrintThresholdContractSmoke(thresholdContractSmokeReport.Value);
RecordAuxiliaryCheck(thresholdContractSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);

defaultStateContractSmokeReport = ValidateTerrainDefaultStateContracts();
PrintDefaultStateContractSmoke(defaultStateContractSmokeReport.Value);
RecordAuxiliaryCheck(defaultStateContractSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);

PrintAggregate(
    aggregate,
    seedCount,
    seedFailures,
    totalFailures,
    auxiliaryCheckCount,
    auxiliaryFailureCount,
    corridorSmokeReport,
    routeScatterSmokeReport,
    poiTileSmokeReport,
    gameplayScatterSmokeReport,
    biomeScatterSmokeReport,
    scenicLandmarkSmokeReport,
    artifactSmokeReport,
    planJsonSmokeReport,
    enumContractSmokeReport,
    publicApiShapeSmokeReport,
    profileHashSmokeReport,
    validationCliContractSmokeReport,
    thresholdContractSmokeReport,
    defaultStateContractSmokeReport,
    runtimeApiSmokeReport,
    anchorSmokeReport,
    runtimeWorldSmokeReport,
    nativeSmokeReport,
    tileBenchmarkReport);
return totalFailures == 0 ? 0 : 1;

static TerrainValidationResult ValidateSeed(TerrainGenerationProfile profile, float worldSize)
{
    TerrainWorldPlan plan = TerrainWorldPlanner.CreateOpenWorldPlan(profile, Vector2.Zero, worldSize);
    TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
    TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
    TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
    TerrainPointOfInterestArchetypeValidationReport archetypeGate = TerrainPointOfInterestArchetypeCatalog.ValidatePlanReadiness(plan);
    return new TerrainValidationResult(profile.Seed, plan, qualityGate, planningGate, experienceGate, archetypeGate);
}

static void RecordAuxiliaryCheck(
    bool passed,
    ref int totalFailures,
    ref int auxiliaryCheckCount,
    ref int auxiliaryFailureCount)
{
    auxiliaryCheckCount++;
    if (!passed)
    {
        auxiliaryFailureCount++;
        totalFailures++;
    }
}

static void PrintSeedResult(TerrainValidationResult result, bool detailed)
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

static TerrainRouteCorridorSmokeReport ValidateRouteCorridorTileEffect(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    TerrainWorldRoute route = default;
    bool foundRoute = false;
    foreach (TerrainWorldRoute candidate in plan.Routes)
    {
        if (candidate.Waypoints.Length >= 2)
        {
            route = candidate;
            foundRoute = true;
            break;
        }
    }

    if (!foundRoute)
    {
        return new TerrainRouteCorridorSmokeReport(false, profile.Seed, default, 0, 0.0f, 0.0f, 0, false, "no route with at least two waypoints");
    }

    Vector2 midpoint = route.Waypoints[route.Waypoints.Length / 2];
    TerrainTileCoord coord = TerrainTileCoord.FromWorldPosition(new Vector3(midpoint.X, 0.0f, midpoint.Y), profile.ChunkSize);
    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    TerrainRouteCorridorSegment[] segments = corridorIndex.GetSegments(coord);
    if (segments.Length == 0)
    {
        return new TerrainRouteCorridorSmokeReport(false, profile.Seed, coord, 0, 0.0f, 0.0f, 0, false, "selected route chunk had no indexed corridor segments");
    }

    bool segmentSnapshotIsolated = CorridorSegmentSnapshotIsolated(corridorIndex, coord, segments);
    TerrainTileData baseline = TerrainTileBuilder.Build(coord, lod: 0, profile, includeCollision: false);
    TerrainTileData withCorridor = TerrainTileBuilder.Build(coord, lod: 0, profile, includeCollision: false, corridorIndex);

    float maxHeightDelta = 0.0f;
    float maxColorDelta = 0.0f;
    int influencedVertices = 0;
    Vector2 origin = coord.Origin(profile.ChunkSize);
    int vertexCount = Math.Min(baseline.Vertices.Length, withCorridor.Vertices.Length);

    for (int i = 0; i < vertexCount; i++)
    {
        Vector3 baselineVertex = baseline.Vertices[i];
        Vector2 world = new(origin.X + baselineVertex.X, origin.Y + baselineVertex.Z);
        TerrainRouteCorridorSample sample = corridorIndex.Sample(world, segments);
        if (sample.HasInfluence)
        {
            influencedVertices++;
        }

        maxHeightDelta = Math.Max(maxHeightDelta, Math.Abs(withCorridor.Vertices[i].Y - baselineVertex.Y));
        maxColorDelta = Math.Max(maxColorDelta, ColorDistance(withCorridor.Colors[i], baseline.Colors[i]));
    }

    bool passed =
        influencedVertices > 0 &&
        (maxHeightDelta >= 0.05f || maxColorDelta >= 0.01f) &&
        segmentSnapshotIsolated;
    string reason = passed
        ? "route corridor affected the generated tile without leaking index segment state"
        : "route corridor produced no measurable tile change or leaked mutable segment state";

    return new TerrainRouteCorridorSmokeReport(
        passed,
        profile.Seed,
        coord,
        segments.Length,
        maxHeightDelta,
        maxColorDelta,
        influencedVertices,
        segmentSnapshotIsolated,
        reason);
}

static bool CorridorSegmentSnapshotIsolated(
    TerrainRouteCorridorIndex corridorIndex,
    TerrainTileCoord coord,
    TerrainRouteCorridorSegment[] segments)
{
    TerrainRouteCorridorSegment original = segments[0];
    segments[0] = default;
    TerrainRouteCorridorSegment[] secondRead = corridorIndex.GetSegments(coord);
    return secondRead.Length == segments.Length &&
        ExactPositionEquals(secondRead[0].From, original.From) &&
        ExactPositionEquals(secondRead[0].To, original.To) &&
        secondRead[0].Kind == original.Kind &&
        ExactFloatEquals(secondRead[0].CoreWidth, original.CoreWidth) &&
        ExactFloatEquals(secondRead[0].ShoulderWidth, original.ShoulderWidth);
}

static void PrintCorridorSmoke(TerrainRouteCorridorSmokeReport report)
{
    Console.WriteLine(
        $"Route corridor tile smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"seed {report.Seed}, tile {report.Coord}, segments {report.SegmentCount}, " +
        $"influenced vertices {report.InfluencedVertexCount}, max height delta {report.MaxHeightDelta:0.000}, " +
        $"max color delta {report.MaxColorDelta:0.000}, segment snapshot {(report.SegmentSnapshotIsolated ? "pass" : "fail")} " +
        $"({report.Reason})");
}

static TerrainRouteScatterSmokeReport ValidateRouteScatterMaterialization(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    var coords = new HashSet<TerrainTileCoord>();
    AddRouteScatterCandidateCoords(plan, profile, coords, maxCoords: 96);

    if (coords.Count == 0)
    {
        return new TerrainRouteScatterSmokeReport(false, 0, 0, 0, 0, 0, "no route scatter candidate tiles found");
    }

    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    if (!corridorIndex.HasSegments)
    {
        return new TerrainRouteScatterSmokeReport(false, coords.Count, 0, 0, 0, 0, "route corridor index had no segments");
    }

    TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);
    int sampledTiles = 0;
    int roadMarkerCount = 0;
    int bridgeSpanCount = 0;

    foreach (TerrainTileCoord coord in coords)
    {
        TerrainTileData data = TerrainTileBuilder.Build(
            coord,
            lod: 0,
            profile,
            includeCollision: false,
            corridorIndex,
            poiIndex);

        sampledTiles++;
        foreach (TerrainScatterInstance scatter in data.ScatterInstances)
        {
            if (scatter.Kind != TerrainScatterKind.Landmark)
            {
                continue;
            }

            if (scatter.LandmarkKind == TerrainLandmarkKind.RoadMarker)
            {
                roadMarkerCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.BridgeSpan)
            {
                bridgeSpanCount++;
            }
        }
    }

    int routeLandmarkCount = roadMarkerCount + bridgeSpanCount;
    bool passed = roadMarkerCount >= 8 && bridgeSpanCount > 0 && routeLandmarkCount >= 12;
    string reason = passed
        ? "route corridors materialized visible road markers and bridge spans"
        : "route corridors did not materialize enough visible route content";

    return new TerrainRouteScatterSmokeReport(
        passed,
        coords.Count,
        sampledTiles,
        roadMarkerCount,
        bridgeSpanCount,
        routeLandmarkCount,
        reason);
}

static void AddRouteScatterCandidateCoords(
    TerrainWorldPlan plan,
    TerrainGenerationProfile profile,
    HashSet<TerrainTileCoord> coords,
    int maxCoords)
{
    foreach (TerrainWorldRoute route in plan.Routes)
    {
        if (coords.Count >= maxCoords)
        {
            return;
        }

        if (route.Waypoints.Length < 2)
        {
            continue;
        }

        AddWorldCoord(coords, route.Waypoints[0], profile);
        AddWorldCoord(coords, route.Waypoints[route.Waypoints.Length / 2], profile);
        AddWorldCoord(coords, route.Waypoints[^1], profile);

        int stride = Math.Max(1, route.Waypoints.Length / 4);
        for (int i = stride; i < route.Waypoints.Length && coords.Count < maxCoords; i += stride)
        {
            AddWorldCoord(coords, route.Waypoints[i], profile);
        }
    }
}

static void PrintRouteScatterSmoke(TerrainRouteScatterSmokeReport report)
{
    Console.WriteLine(
        $"Route scatter smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, " +
        $"road markers/bridges {report.RoadMarkerCount}/{report.BridgeSpanCount}, " +
        $"total {report.RouteLandmarkCount} ({report.Reason})");
}

static TerrainPoiTileSmokeReport ValidatePoiTileMaterialization(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);
    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    var expected = new HashSet<string>(StringComparer.Ordinal);
    var coords = new HashSet<TerrainTileCoord>();

    foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
    {
        expected.Add(PoiLandmarkName(point));
        AddPoiFootprintCoords(coords, point, profile);
    }

    var materialized = new HashSet<string>(StringComparer.Ordinal);
    Span<int> kindCounts = stackalloc int[Enum.GetValues<TerrainLandmarkKind>().Length];
    Span<int> scatterKindCounts = stackalloc int[Enum.GetValues<TerrainLandmarkKind>().Length];
    int landmarkScatterCount = 0;

    foreach (TerrainTileCoord coord in coords)
    {
        TerrainTileData data = TerrainTileBuilder.Build(
            coord,
            lod: 0,
            profile,
            includeCollision: false,
            corridorIndex,
            poiIndex);

        foreach (TerrainLandmarkData landmark in data.Landmarks)
        {
            if (landmark.DebugName.StartsWith("POI_", StringComparison.Ordinal))
            {
                materialized.Add(landmark.DebugName);
                int kindIndex = Mathf.Clamp((int)landmark.Kind, 0, kindCounts.Length - 1);
                kindCounts[kindIndex]++;
            }
        }

        foreach (TerrainScatterInstance scatter in data.ScatterInstances)
        {
            if (scatter.Kind == TerrainScatterKind.Landmark)
            {
                landmarkScatterCount++;
                int kindIndex = Mathf.Clamp((int)scatter.LandmarkKind, 0, scatterKindCounts.Length - 1);
                scatterKindCounts[kindIndex]++;
            }
        }
    }

    TerrainPoiFootprintSmokeReport footprintReport = ValidatePoiFootprintTileEffect(profile, plan, corridorIndex, poiIndex);

    int distinctKinds = 0;
    int distinctScatterKinds = 0;
    for (int i = 0; i < kindCounts.Length; i++)
    {
        if (kindCounts[i] > 0)
        {
            distinctKinds++;
        }

        if (scatterKindCounts[i] > 0)
        {
            distinctScatterKinds++;
        }
    }

    int settlementLandmarkCount = SettlementLandmarkCount(kindCounts);
    int expectedSettlementPointCount = CountSettlementPoints(plan);
    int settlementInteriorScatterCount = SettlementInteriorScatterCount(scatterKindCounts);
    int villageHouseScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.VillageHouse];
    int townBlockScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.TownBlock];
    int oasisCanopyScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.OasisCanopy];
    int settlementPlazaScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.SettlementPlaza];
    int oasisPoolScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.OasisPool];
    int villageWellScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.VillageWell];
    int marketStallScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.MarketStall];
    int watchTowerScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.WatchTower];
    int oasisGardenScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.OasisGarden];
    int settlementGatewayScatterCount = scatterKindCounts[(int)TerrainLandmarkKind.SettlementGateway];
    int settlementServiceScatterCount =
        villageWellScatterCount +
        marketStallScatterCount +
        watchTowerScatterCount +
        oasisGardenScatterCount;
    bool poiIndexSnapshotIsolated = PoiIndexSnapshotIsolated(poiIndex, coords);

    bool passed =
        materialized.Count == expected.Count &&
        distinctKinds >= 5 &&
        distinctScatterKinds >= 9 &&
        kindCounts[(int)TerrainLandmarkKind.Village] > 0 &&
        kindCounts[(int)TerrainLandmarkKind.Town] > 0 &&
        scatterKindCounts[(int)TerrainLandmarkKind.Village] >= kindCounts[(int)TerrainLandmarkKind.Village] &&
        scatterKindCounts[(int)TerrainLandmarkKind.Town] >= kindCounts[(int)TerrainLandmarkKind.Town] &&
        settlementInteriorScatterCount >= expectedSettlementPointCount * 3 &&
        villageHouseScatterCount > 0 &&
        townBlockScatterCount > 0 &&
        settlementPlazaScatterCount > 0 &&
        villageWellScatterCount > 0 &&
        marketStallScatterCount > 0 &&
        watchTowerScatterCount > 0 &&
        settlementServiceScatterCount >= expectedSettlementPointCount &&
        settlementGatewayScatterCount >= expectedSettlementPointCount &&
        (kindCounts[(int)TerrainLandmarkKind.OasisHub] == 0 ||
            (oasisCanopyScatterCount > 0 && oasisPoolScatterCount > 0 && oasisGardenScatterCount > 0)) &&
        landmarkScatterCount >= expected.Count &&
        footprintReport.Passed &&
        poiIndexSnapshotIsolated;
    string reason = passed
        ? "planned POIs materialized as tile landmarks with settlement services and road-connected gateways"
        : "planned POIs missing from tile landmark data or leaked mutable index state";

    return new TerrainPoiTileSmokeReport(
        passed,
        expected.Count,
        materialized.Count,
        coords.Count,
        distinctKinds,
        distinctScatterKinds,
        kindCounts[(int)TerrainLandmarkKind.Village],
        kindCounts[(int)TerrainLandmarkKind.Town],
        kindCounts[(int)TerrainLandmarkKind.OasisHub],
        scatterKindCounts[(int)TerrainLandmarkKind.Village],
        scatterKindCounts[(int)TerrainLandmarkKind.Town],
        scatterKindCounts[(int)TerrainLandmarkKind.OasisHub],
        settlementLandmarkCount,
        expectedSettlementPointCount,
        settlementInteriorScatterCount,
        villageHouseScatterCount,
        townBlockScatterCount,
        oasisCanopyScatterCount,
        settlementPlazaScatterCount,
        oasisPoolScatterCount,
        villageWellScatterCount,
        marketStallScatterCount,
        watchTowerScatterCount,
        oasisGardenScatterCount,
        settlementGatewayScatterCount,
        settlementServiceScatterCount,
        footprintReport.InfluencedVertexCount,
        footprintReport.MaxHeightDelta,
        footprintReport.MaxColorDelta,
        footprintReport.LayoutColorVertexCount,
        footprintReport.LayoutMaxColorDelta,
        landmarkScatterCount,
        poiIndexSnapshotIsolated,
        reason);
}

static bool PoiIndexSnapshotIsolated(TerrainPointOfInterestIndex poiIndex, HashSet<TerrainTileCoord> coords)
{
    foreach (TerrainTileCoord coord in coords)
    {
        TerrainWorldPointOfInterest[] points = poiIndex.GetPoints(coord);
        if (points.Length == 0)
        {
            continue;
        }

        TerrainWorldPointOfInterest original = points[0];
        points[0] = default;
        TerrainWorldPointOfInterest[] secondRead = poiIndex.GetPoints(coord);
        return secondRead.Length == points.Length &&
            secondRead[0].Id == original.Id &&
            secondRead[0].Kind == original.Kind &&
            secondRead[0].WorldPosition == original.WorldPosition;
    }

    return false;
}

static TerrainPoiFootprintSmokeReport ValidatePoiFootprintTileEffect(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan,
    TerrainRouteCorridorIndex corridorIndex,
    TerrainPointOfInterestIndex poiIndex)
{
    TerrainWorldPointOfInterest point = default;
    bool foundPoint = false;
    foreach (TerrainWorldPointOfInterest candidate in plan.PointsOfInterest)
    {
        if (candidate.SettlementTier is TerrainSettlementTier.Village or TerrainSettlementTier.Town or TerrainSettlementTier.OasisHub)
        {
            point = candidate;
            foundPoint = true;
            break;
        }
    }

    if (!foundPoint)
    {
        return new TerrainPoiFootprintSmokeReport(false, 0, 0.0f, 0.0f, 0, 0.0f);
    }

    TerrainTileCoord coord = new(
        Mathf.FloorToInt(point.WorldPosition.X / profile.ChunkSize),
        Mathf.FloorToInt(point.WorldPosition.Y / profile.ChunkSize));
    TerrainTileData baseline = TerrainTileBuilder.Build(coord, lod: 0, profile, includeCollision: false, corridorIndex);
    TerrainTileData withPoi = TerrainTileBuilder.Build(coord, lod: 0, profile, includeCollision: false, corridorIndex, poiIndex);
    Vector2 origin = coord.Origin(profile.ChunkSize);
    float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
    float maxHeightDelta = 0.0f;
    float maxColorDelta = 0.0f;
    float layoutMaxColorDelta = 0.0f;
    int influencedVertices = 0;
    int layoutColorVertices = 0;
    int vertexCount = Math.Min(baseline.Vertices.Length, withPoi.Vertices.Length);

    for (int i = 0; i < vertexCount; i++)
    {
        Vector3 baselineVertex = baseline.Vertices[i];
        Vector2 world = new(origin.X + baselineVertex.X, origin.Y + baselineVertex.Z);
        if (world.DistanceTo(point.WorldPosition) > radius)
        {
            continue;
        }

        influencedVertices++;
        float heightDelta = Math.Abs(withPoi.Vertices[i].Y - baselineVertex.Y);
        float colorDelta = ColorDistance(withPoi.Colors[i], baseline.Colors[i]);
        maxHeightDelta = Math.Max(maxHeightDelta, heightDelta);
        maxColorDelta = Math.Max(maxColorDelta, colorDelta);

        if (colorDelta >= 0.16f)
        {
            layoutColorVertices++;
            layoutMaxColorDelta = Math.Max(layoutMaxColorDelta, colorDelta);
        }
    }

    bool passed =
        influencedVertices > 0 &&
        (maxHeightDelta >= 0.05f || maxColorDelta >= 0.01f) &&
        layoutColorVertices >= 8 &&
        layoutMaxColorDelta >= 0.16f;
    return new TerrainPoiFootprintSmokeReport(passed, influencedVertices, maxHeightDelta, maxColorDelta, layoutColorVertices, layoutMaxColorDelta);
}

static string PoiLandmarkName(TerrainWorldPointOfInterest point)
{
    return $"POI_{point.Id:00}_{point.Kind}";
}

static void AddPoiFootprintCoords(
    HashSet<TerrainTileCoord> coords,
    TerrainWorldPointOfInterest point,
    TerrainGenerationProfile profile)
{
    float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
    int minX = Mathf.FloorToInt((point.WorldPosition.X - radius) / profile.ChunkSize);
    int maxX = Mathf.FloorToInt((point.WorldPosition.X + radius) / profile.ChunkSize);
    int minZ = Mathf.FloorToInt((point.WorldPosition.Y - radius) / profile.ChunkSize);
    int maxZ = Mathf.FloorToInt((point.WorldPosition.Y + radius) / profile.ChunkSize);

    for (int z = minZ; z <= maxZ; z++)
    {
        for (int x = minX; x <= maxX; x++)
        {
            coords.Add(new TerrainTileCoord(x, z));
        }
    }
}

static void PrintPoiTileSmoke(TerrainPoiTileSmokeReport report)
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

static int SettlementLandmarkCount(Span<int> kindCounts)
{
    return kindCounts[(int)TerrainLandmarkKind.Village] +
        kindCounts[(int)TerrainLandmarkKind.Town] +
        kindCounts[(int)TerrainLandmarkKind.OasisHub];
}

static int CountSettlementPoints(TerrainWorldPlan plan)
{
    int count = 0;
    foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
    {
        if (point.SettlementTier is TerrainSettlementTier.Village or TerrainSettlementTier.Town or TerrainSettlementTier.OasisHub)
        {
            count++;
        }
    }

    return count;
}

static int SettlementInteriorScatterCount(Span<int> scatterKindCounts)
{
    return scatterKindCounts[(int)TerrainLandmarkKind.VillageHouse] +
        scatterKindCounts[(int)TerrainLandmarkKind.TownBlock] +
        scatterKindCounts[(int)TerrainLandmarkKind.OasisCanopy] +
        scatterKindCounts[(int)TerrainLandmarkKind.SettlementPlaza] +
        scatterKindCounts[(int)TerrainLandmarkKind.OasisPool] +
        scatterKindCounts[(int)TerrainLandmarkKind.VillageWell] +
        scatterKindCounts[(int)TerrainLandmarkKind.MarketStall] +
        scatterKindCounts[(int)TerrainLandmarkKind.WatchTower] +
        scatterKindCounts[(int)TerrainLandmarkKind.OasisGarden];
}

static TerrainGameplayScatterSmokeReport ValidateGameplayScatterMaterialization(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    var coords = new HashSet<TerrainTileCoord>();
    AddGameplayScatterCandidateCoords(plan, profile, coords, maxCoords: 96);

    if (coords.Count == 0)
    {
        return new TerrainGameplayScatterSmokeReport(false, 0, 0, 0, 0, 0, 0, "no gameplay scatter candidate tiles found");
    }

    TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);
    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    Span<int> scatterCounts = stackalloc int[Enum.GetValues<TerrainScatterKind>().Length];
    int sampledTiles = 0;

    foreach (TerrainTileCoord coord in coords)
    {
        TerrainTileData data = TerrainTileBuilder.Build(
            coord,
            lod: 0,
            profile,
            includeCollision: false,
            corridorIndex,
            poiIndex);

        sampledTiles++;
        foreach (TerrainScatterInstance scatter in data.ScatterInstances)
        {
            int kindIndex = Mathf.Clamp((int)scatter.Kind, 0, scatterCounts.Length - 1);
            scatterCounts[kindIndex]++;
        }
    }

    int understoryCount = scatterCounts[(int)TerrainScatterKind.Understory];
    int resourceNodeCount = scatterCounts[(int)TerrainScatterKind.ResourceNode];
    int hazardOutcropCount = scatterCounts[(int)TerrainScatterKind.HazardOutcrop];
    int totalGameplayScatter = understoryCount + resourceNodeCount + hazardOutcropCount;
    bool passed = understoryCount > 0 && resourceNodeCount > 0 && hazardOutcropCount > 0 && totalGameplayScatter >= 12;
    string reason = passed
        ? "gameplay scatter materialized from resource and hazard fields"
        : "one or more gameplay scatter kinds did not materialize";

    return new TerrainGameplayScatterSmokeReport(
        passed,
        coords.Count,
        sampledTiles,
        understoryCount,
        resourceNodeCount,
        hazardOutcropCount,
        totalGameplayScatter,
        reason);
}

static void AddGameplayScatterCandidateCoords(
    TerrainWorldPlan plan,
    TerrainGenerationProfile profile,
    HashSet<TerrainTileCoord> coords,
    int maxCoords)
{
    var candidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length);
    foreach (TerrainWorldRegion region in plan.Regions)
    {
        if (region.RegionKind == TerrainWorldRegionKind.Ocean)
        {
            continue;
        }

        float score =
            region.ResourcePotential * 0.34f +
            region.HazardPotential * 0.28f +
            region.EncounterPotential * 0.28f +
            region.Exposure * 0.10f;
        candidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, score));
    }

    candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

    foreach (GameplayScatterRegionCandidate candidate in candidates)
    {
        if (coords.Count >= maxCoords)
        {
            return;
        }

        AddWorldCoord(coords, candidate.WorldPosition, profile);
    }

    foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
    {
        if (coords.Count >= maxCoords)
        {
            return;
        }

        AddWorldCoord(coords, point.WorldPosition, profile);
    }

    foreach (TerrainWorldRoute route in plan.Routes)
    {
        if (coords.Count >= maxCoords)
        {
            return;
        }

        if (route.Waypoints.Length == 0)
        {
            continue;
        }

        AddWorldCoord(coords, route.Waypoints[route.Waypoints.Length / 2], profile);
    }
}

static void AddWorldCoord(
    HashSet<TerrainTileCoord> coords,
    Vector2 world,
    TerrainGenerationProfile profile)
{
    coords.Add(new TerrainTileCoord(
        Mathf.FloorToInt(world.X / profile.ChunkSize),
        Mathf.FloorToInt(world.Y / profile.ChunkSize)));
}

static void PrintGameplayScatterSmoke(TerrainGameplayScatterSmokeReport report)
{
    Console.WriteLine(
        $"Gameplay scatter smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, " +
        $"understory/resource/hazard {report.UnderstoryCount}/{report.ResourceNodeCount}/{report.HazardOutcropCount}, " +
        $"total {report.TotalGameplayScatterCount} ({report.Reason})");
}

static TerrainBiomeScatterSmokeReport ValidateBiomeScatterMaterialization(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    var coords = new HashSet<TerrainTileCoord>();
    AddBiomeScatterCandidateCoords(plan, profile, coords, maxCoords: 96);
    foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
    {
        if (point.SettlementTier == TerrainSettlementTier.OasisHub)
        {
            AddPoiFootprintCoords(coords, point, profile);
        }
    }

    if (coords.Count == 0)
    {
        return new TerrainBiomeScatterSmokeReport(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "no biome scatter candidate tiles found");
    }

    TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);
    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    Span<int> scatterCounts = stackalloc int[Enum.GetValues<TerrainScatterKind>().Length];
    int sampledTiles = 0;
    int lakeWaterCellCount = 0;
    int riverWaterCellCount = 0;
    int oasisWaterCellCount = 0;

    foreach (TerrainTileCoord coord in coords)
    {
        TerrainTileData data = TerrainTileBuilder.Build(
            coord,
            lod: 0,
            profile,
            includeCollision: false,
            corridorIndex,
            poiIndex);

        sampledTiles++;
        foreach (TerrainScatterInstance scatter in data.ScatterInstances)
        {
            int kindIndex = Mathf.Clamp((int)scatter.Kind, 0, scatterCounts.Length - 1);
            scatterCounts[kindIndex]++;
        }

        lakeWaterCellCount += data.WaterSurface.LakeCellCount;
        riverWaterCellCount += data.WaterSurface.RiverCellCount;
        oasisWaterCellCount += data.WaterSurface.OasisCellCount;
    }

    int grassTuftCount = scatterCounts[(int)TerrainScatterKind.GrassTuft];
    int desertShrubCount = scatterCounts[(int)TerrainScatterKind.DesertShrub];
    int cactusClusterCount = scatterCounts[(int)TerrainScatterKind.CactusCluster];
    int reedClusterCount = scatterCounts[(int)TerrainScatterKind.ReedCluster];
    int snowClumpCount = scatterCounts[(int)TerrainScatterKind.SnowClump];
    int alpinePineCount = scatterCounts[(int)TerrainScatterKind.AlpinePine];
    int coastalPalmCount = scatterCounts[(int)TerrainScatterKind.CoastalPalm];
    int driftwoodCount = scatterCounts[(int)TerrainScatterKind.Driftwood];
    int mangroveRootCount = scatterCounts[(int)TerrainScatterKind.MangroveRoot];
    int lakeReedCount = scatterCounts[(int)TerrainScatterKind.LakeReed];
    int waterLilyCount = scatterCounts[(int)TerrainScatterKind.WaterLily];
    int biomeScatterCount =
        grassTuftCount +
        desertShrubCount +
        cactusClusterCount +
        reedClusterCount +
        snowClumpCount +
        alpinePineCount +
        coastalPalmCount +
        driftwoodCount +
        mangroveRootCount +
        lakeReedCount +
        waterLilyCount;
    int totalWaterCellCount = lakeWaterCellCount + riverWaterCellCount + oasisWaterCellCount;
    bool expectsGrass = HasBiomeSource(plan, static region =>
        region.BiomeKind is TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland ||
        region.RegionKind is TerrainWorldRegionKind.Plains or TerrainWorldRegionKind.Grassland);
    bool expectsDesert = HasBiomeSource(plan, static region =>
        region.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis ||
        region.RegionKind is TerrainWorldRegionKind.Desert or TerrainWorldRegionKind.Oasis);
    bool expectsWetland = HasBiomeSource(plan, static region =>
        region.BiomeKind is TerrainBiomeKind.Wetland or TerrainBiomeKind.Lake or TerrainBiomeKind.Oasis ||
        region.LandscapeKind is TerrainLandscapeKind.Wetland or TerrainLandscapeKind.Lake ||
        region.RegionKind is TerrainWorldRegionKind.Wetland or TerrainWorldRegionKind.Lake or TerrainWorldRegionKind.Oasis);
    bool expectsSnow = HasBiomeSource(plan, static region =>
        region.BiomeKind == TerrainBiomeKind.Snowfield ||
        region.LandscapeKind == TerrainLandscapeKind.Snowfield ||
        region.RegionKind == TerrainWorldRegionKind.Snow);
    bool expectsCoast = HasBiomeSource(plan, static region =>
        region.BiomeKind is TerrainBiomeKind.Coast or TerrainBiomeKind.Island ||
        region.LandscapeKind == TerrainLandscapeKind.Coast ||
        region.RegionKind is TerrainWorldRegionKind.Coast or TerrainWorldRegionKind.Island);
    bool expectsLake = HasBiomeSource(plan, static region =>
        region.BiomeKind == TerrainBiomeKind.Lake ||
        region.LandscapeKind == TerrainLandscapeKind.Lake ||
        region.RegionKind == TerrainWorldRegionKind.Lake);
    bool expectsRiver = HasBiomeSource(plan, static region =>
        region.River > 0.62f ||
        region.LandscapeKind == TerrainLandscapeKind.RiverValley ||
        region.RegionKind == TerrainWorldRegionKind.RiverValley);
    bool expectsOasis = HasBiomeSource(plan, static region =>
        region.BiomeKind == TerrainBiomeKind.Oasis ||
        region.RegionKind == TerrainWorldRegionKind.Oasis) ||
        HasOasisHub(plan);
    bool grassPassed = !expectsGrass || grassTuftCount > 0;
    bool desertPassed = !expectsDesert || desertShrubCount + cactusClusterCount > 0;
    bool wetlandPassed = !expectsWetland || reedClusterCount + lakeReedCount + waterLilyCount + mangroveRootCount > 0;
    bool snowPassed = !expectsSnow || snowClumpCount + alpinePineCount > 0;
    bool coastPassed = !expectsCoast || coastalPalmCount + driftwoodCount + mangroveRootCount > 0;
    bool lakePassed = !expectsLake || lakeWaterCellCount > 0 || lakeReedCount + waterLilyCount > 0;
    bool riverPassed = !expectsRiver || riverWaterCellCount > 0;
    bool oasisPassed = !expectsOasis || oasisWaterCellCount > 0 || reedClusterCount > 0;
    int requiredCategoryCount = CountTrue(
        expectsGrass,
        expectsDesert,
        expectsWetland,
        expectsSnow,
        expectsCoast,
        expectsLake,
        expectsRiver,
        expectsOasis);
    int materializedCategoryCount = CountTrue(
        expectsGrass && grassPassed,
        expectsDesert && desertPassed,
        expectsWetland && wetlandPassed,
        expectsSnow && snowPassed,
        expectsCoast && coastPassed,
        expectsLake && lakePassed,
        expectsRiver && riverPassed,
        expectsOasis && oasisPassed);
    bool passed =
        requiredCategoryCount > 0 &&
        materializedCategoryCount == requiredCategoryCount &&
        biomeScatterCount >= 72 &&
        totalWaterCellCount >= 48;
    string reason = passed
        ? "biome scatter and local water surfaces materialized for this seed's planned biome and water coverage"
        : BiomeScatterFailureReason(
            grassPassed,
            desertPassed,
            wetlandPassed,
            snowPassed,
            coastPassed,
            lakePassed,
            riverPassed,
            oasisPassed,
            biomeScatterCount,
            totalWaterCellCount);

    return new TerrainBiomeScatterSmokeReport(
        passed,
        coords.Count,
        sampledTiles,
        requiredCategoryCount,
        materializedCategoryCount,
        grassTuftCount,
        desertShrubCount,
        cactusClusterCount,
        reedClusterCount,
        snowClumpCount,
        alpinePineCount,
        coastalPalmCount,
        driftwoodCount,
        mangroveRootCount,
        lakeReedCount,
        waterLilyCount,
        biomeScatterCount,
        lakeWaterCellCount,
        riverWaterCellCount,
        oasisWaterCellCount,
        totalWaterCellCount,
        reason);
}

static bool HasBiomeSource(TerrainWorldPlan plan, Predicate<TerrainWorldRegion> predicate)
{
    foreach (TerrainWorldRegion region in plan.Regions)
    {
        if (predicate(region))
        {
            return true;
        }
    }

    return false;
}

static bool HasOasisHub(TerrainWorldPlan plan)
{
    foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
    {
        if (point.SettlementTier == TerrainSettlementTier.OasisHub)
        {
            return true;
        }
    }

    return false;
}

static int CountTrue(params bool[] values)
{
    int count = 0;
    foreach (bool value in values)
    {
        if (value)
        {
            count++;
        }
    }

    return count;
}

static string BiomeScatterFailureReason(
    bool grassPassed,
    bool desertPassed,
    bool wetlandPassed,
    bool snowPassed,
    bool coastPassed,
    bool lakePassed,
    bool riverPassed,
    bool oasisPassed,
    int biomeScatterCount,
    int totalWaterCellCount)
{
    var missing = new List<string>();
    if (!grassPassed)
    {
        missing.Add("grass");
    }

    if (!desertPassed)
    {
        missing.Add("desert");
    }

    if (!wetlandPassed)
    {
        missing.Add("wetland");
    }

    if (!snowPassed)
    {
        missing.Add("snow");
    }

    if (!coastPassed)
    {
        missing.Add("coast");
    }

    if (!lakePassed)
    {
        missing.Add("lake");
    }

    if (!riverPassed)
    {
        missing.Add("river");
    }

    if (!oasisPassed)
    {
        missing.Add("oasis");
    }

    if (biomeScatterCount < 72)
    {
        missing.Add("biome scatter density");
    }

    if (totalWaterCellCount < 48)
    {
        missing.Add("local water surface cells");
    }

    return $"missing biome/water materialization for {string.Join(", ", missing)}";
}

static void AddBiomeScatterCandidateCoords(
    TerrainWorldPlan plan,
    TerrainGenerationProfile profile,
    HashSet<TerrainTileCoord> coords,
    int maxCoords)
{
    var grassCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 3);
    var desertCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 3);
    var wetlandCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 3);
    var lakeCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 3);
    var riverCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 3);
    var oasisCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 3);
    var snowCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 3);
    var coastCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 3);
    var generalCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length);

    foreach (TerrainWorldRegion region in plan.Regions)
    {
        if (region.RegionKind == TerrainWorldRegionKind.Ocean)
        {
            continue;
        }

        float lowlandBonus = region.LandscapeKind is TerrainLandscapeKind.Lowland or TerrainLandscapeKind.ForestBasin
            ? 0.10f
            : 0.0f;
        float grassScore = region.BiomeKind is TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland
            ? 0.42f + region.ResourcePotential * 0.22f + region.Traversability * 0.16f + lowlandBonus
            : 0.0f;
        float desertScore = region.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis
            ? 0.44f + region.ScenicPotential * 0.16f + region.Exposure * 0.14f + region.Traversability * 0.10f
            : 0.0f;
        float wetlandScore = region.BiomeKind == TerrainBiomeKind.Wetland || region.LandscapeKind == TerrainLandscapeKind.Wetland
            ? 0.46f + region.River * 0.20f + region.ResourcePotential * 0.18f + region.Traversability * 0.08f
            : 0.0f;
        float lakeScore = region.BiomeKind == TerrainBiomeKind.Lake || region.LandscapeKind == TerrainLandscapeKind.Lake
            ? 0.50f + region.ResourcePotential * 0.20f + region.ScenicPotential * 0.12f + region.Traversability * 0.06f
            : 0.0f;
        float riverScore = region.River > 0.62f && region.LandscapeKind != TerrainLandscapeKind.Ocean
            ? 0.48f + region.River * 0.22f + region.ScenicPotential * 0.12f + region.ResourcePotential * 0.10f
            : 0.0f;
        float oasisScore = region.BiomeKind == TerrainBiomeKind.Oasis || region.RegionKind == TerrainWorldRegionKind.Oasis
            ? 0.50f + region.ResourcePotential * 0.18f + region.ScenicPotential * 0.12f + region.Traversability * 0.08f
            : 0.0f;
        float snowScore = region.BiomeKind == TerrainBiomeKind.Snowfield || region.LandscapeKind == TerrainLandscapeKind.Snowfield
            ? 0.46f + region.Exposure * 0.20f + region.ScenicPotential * 0.14f + region.Traversability * 0.06f
            : 0.0f;
        float coastScore = region.BiomeKind is TerrainBiomeKind.Coast or TerrainBiomeKind.Island
            ? 0.44f + region.ScenicPotential * 0.16f + region.Traversability * 0.14f + region.River * 0.08f
            : 0.0f;

        if (grassScore > 0.0f)
        {
            grassCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, grassScore));
        }

        if (desertScore > 0.0f)
        {
            desertCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, desertScore));
        }

        if (wetlandScore > 0.0f)
        {
            wetlandCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, wetlandScore));
        }

        if (lakeScore > 0.0f)
        {
            lakeCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, lakeScore));
        }

        if (riverScore > 0.0f)
        {
            riverCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, riverScore));
        }

        if (oasisScore > 0.0f)
        {
            oasisCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, oasisScore));
        }

        if (snowScore > 0.0f)
        {
            snowCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, snowScore));
        }

        if (coastScore > 0.0f)
        {
            coastCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, coastScore));
        }

        float waterScore = Mathf.Max(Mathf.Max(wetlandScore, lakeScore), Mathf.Max(riverScore, oasisScore));
        float generalScore = Mathf.Max(Mathf.Max(Mathf.Max(grassScore, desertScore), waterScore), Mathf.Max(snowScore, coastScore));
        if (generalScore > 0.0f)
        {
            generalCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, generalScore));
        }
    }

    int categoryQuota = Mathf.Max(7, maxCoords / 9);
    AddSortedCandidateCoords(grassCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(desertCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(wetlandCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(lakeCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(riverCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(oasisCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(snowCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(coastCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(generalCandidates, profile, coords, maxCoords);
}

static void PrintBiomeScatterSmoke(TerrainBiomeScatterSmokeReport report)
{
    Console.WriteLine(
        $"Biome scatter smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, categories {report.MaterializedCategoryCount}/{report.RequiredCategoryCount}, " +
        $"grass/desert/cactus/reeds/snow/alpine/palms/driftwood/mangrove/lake reeds/lilies " +
        $"{report.GrassTuftCount}/{report.DesertShrubCount}/{report.CactusClusterCount}/{report.ReedClusterCount}/{report.SnowClumpCount}/{report.AlpinePineCount}/{report.CoastalPalmCount}/{report.DriftwoodCount}/{report.MangroveRootCount}/{report.LakeReedCount}/{report.WaterLilyCount}, " +
        $"water cells lake/river/oasis {report.LakeWaterCellCount}/{report.RiverWaterCellCount}/{report.OasisWaterCellCount}, " +
        $"total {report.BiomeScatterCount} ({report.Reason})");
}

static TerrainScenicLandmarkSmokeReport ValidateScenicLandmarkMaterialization(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    var coords = new HashSet<TerrainTileCoord>();
    AddScenicLandmarkCandidateCoords(plan, profile, coords, maxCoords: 96);

    if (coords.Count == 0)
    {
        return new TerrainScenicLandmarkSmokeReport(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "no scenic natural landmark candidate tiles found");
    }

    TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);
    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    int sampledTiles = 0;
    int waterfallCount = 0;
    int duneCrestCount = 0;
    int desertMonolithCount = 0;
    int canyonNeedleCount = 0;
    int iceSpireCount = 0;
    int naturalArchCount = 0;
    int geothermalSpringCount = 0;
    int glacialRidgeCount = 0;
    int scenicLandmarkCount = 0;

    foreach (TerrainTileCoord coord in coords)
    {
        TerrainTileData data = TerrainTileBuilder.Build(
            coord,
            lod: 0,
            profile,
            includeCollision: false,
            corridorIndex,
            poiIndex);

        sampledTiles++;
        foreach (TerrainScatterInstance scatter in data.ScatterInstances)
        {
            if (scatter.Kind != TerrainScatterKind.Landmark)
            {
                continue;
            }

            if (scatter.LandmarkKind == TerrainLandmarkKind.Waterfall)
            {
                waterfallCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.DuneCrest)
            {
                duneCrestCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.DesertMonolith)
            {
                desertMonolithCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.CanyonNeedle)
            {
                canyonNeedleCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.IceSpire)
            {
                iceSpireCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.NaturalArch)
            {
                naturalArchCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.GeothermalSpring)
            {
                geothermalSpringCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.GlacialRidge)
            {
                glacialRidgeCount++;
            }

            if (scatter.LandmarkKind is
                TerrainLandmarkKind.Waterfall or
                TerrainLandmarkKind.DuneCrest or
                TerrainLandmarkKind.DesertMonolith or
                TerrainLandmarkKind.CanyonNeedle or
                TerrainLandmarkKind.IceSpire or
                TerrainLandmarkKind.NaturalArch or
                TerrainLandmarkKind.GeothermalSpring or
                TerrainLandmarkKind.GlacialRidge or
                TerrainLandmarkKind.Vista or
                TerrainLandmarkKind.CanyonOverlook)
            {
                scenicLandmarkCount++;
            }
        }
    }

    int biomeScenicLandmarkCount =
        duneCrestCount +
        desertMonolithCount +
        canyonNeedleCount +
        iceSpireCount +
        naturalArchCount +
        geothermalSpringCount +
        glacialRidgeCount;
    int distinctGeneratedKinds = CountPositive(
        waterfallCount,
        duneCrestCount,
        desertMonolithCount,
        canyonNeedleCount,
        iceSpireCount,
        naturalArchCount,
        geothermalSpringCount,
        glacialRidgeCount);
    bool passed =
        waterfallCount > 0 &&
        biomeScenicLandmarkCount > 0 &&
        desertMonolithCount > 0 &&
        canyonNeedleCount > 0 &&
        naturalArchCount > 0 &&
        geothermalSpringCount > 0 &&
        glacialRidgeCount > 0 &&
        distinctGeneratedKinds >= 7 &&
        scenicLandmarkCount >= waterfallCount + biomeScenicLandmarkCount;
    string reason = passed
        ? "scenic natural landmarks materialized across water, desert, canyon, rock, geothermal, and snow terrain"
        : "generated scenic landmark variety did not materialize";

    return new TerrainScenicLandmarkSmokeReport(
        passed,
        coords.Count,
        sampledTiles,
        waterfallCount,
        duneCrestCount,
        desertMonolithCount,
        canyonNeedleCount,
        iceSpireCount,
        naturalArchCount,
        geothermalSpringCount,
        glacialRidgeCount,
        distinctGeneratedKinds,
        scenicLandmarkCount,
        reason);
}

static void AddScenicLandmarkCandidateCoords(
    TerrainWorldPlan plan,
    TerrainGenerationProfile profile,
    HashSet<TerrainTileCoord> coords,
    int maxCoords)
{
    var waterfallCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 2);
    var desertCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 2);
    var rockCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 2);
    var iceCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length / 2);
    var generalCandidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length);

    foreach (TerrainWorldRegion region in plan.Regions)
    {
        if (region.RegionKind == TerrainWorldRegionKind.Ocean)
        {
            continue;
        }

        float elevation = Mathf.SmoothStep(profile.SeaLevel + 96.0f, profile.SeaLevel + profile.HeightScale * 0.70f, region.Height);
        float rockLandscape = region.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau
            ? 0.30f
            : region.LandscapeKind == TerrainLandscapeKind.RiverValley
                ? 0.18f
                : 0.0f;
        float desertLandscape = region.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis
            ? 0.34f
            : 0.0f;
        float iceLandscape = region.BiomeKind == TerrainBiomeKind.Snowfield || region.LandscapeKind == TerrainLandscapeKind.Snowfield
            ? 0.32f
            : 0.0f;
        float waterfallScore =
            region.River * 0.36f +
            region.ScenicPotential * 0.30f +
            elevation * 0.18f +
            region.Exposure * 0.10f +
            (region.LandscapeKind == TerrainLandscapeKind.RiverValley ? 0.16f : 0.0f);
        float desertScore =
            desertLandscape +
            region.ScenicPotential * 0.30f +
            region.Exposure * 0.20f +
            (1.0f - elevation) * 0.06f;
        float rockScore =
            rockLandscape +
            region.ScenicPotential * 0.32f +
            region.Exposure * 0.26f +
            elevation * 0.16f;
        float iceScore =
            iceLandscape +
            region.ScenicPotential * 0.26f +
            region.Exposure * 0.18f +
            elevation * 0.22f;
        float terrainSpectacleScore =
            region.ScenicPotential * 0.34f +
            region.Exposure * 0.24f +
            elevation * 0.18f +
            rockLandscape +
            desertLandscape +
            iceLandscape;

        waterfallCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, waterfallScore));
        if (desertLandscape > 0.0f)
        {
            desertCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, desertScore));
        }

        if (rockLandscape > 0.0f)
        {
            rockCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, rockScore));
        }

        if (iceLandscape > 0.0f)
        {
            iceCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, iceScore));
        }

        generalCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, Mathf.Max(waterfallScore, terrainSpectacleScore)));
    }

    int categoryQuota = Mathf.Max(8, maxCoords / 5);
    AddSortedCandidateCoords(waterfallCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(desertCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(rockCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(iceCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(generalCandidates, profile, coords, maxCoords);
}

static void AddSortedCandidateCoords(
    List<GameplayScatterRegionCandidate> candidates,
    TerrainGenerationProfile profile,
    HashSet<TerrainTileCoord> coords,
    int maxCoords)
{
    if (coords.Count >= maxCoords || candidates.Count == 0)
    {
        return;
    }

    candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
    foreach (GameplayScatterRegionCandidate candidate in candidates)
    {
        if (coords.Count >= maxCoords)
        {
            return;
        }

        AddWorldCoord(coords, candidate.WorldPosition, profile);
    }
}

static void PrintScenicLandmarkSmoke(TerrainScenicLandmarkSmokeReport report)
{
    Console.WriteLine(
        $"Scenic landmark smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, " +
        $"waterfalls/dunes/monoliths/needles/ice/arches/springs/glacial " +
        $"{report.WaterfallCount}/{report.DuneCrestCount}/{report.DesertMonolithCount}/{report.CanyonNeedleCount}/{report.IceSpireCount}/{report.NaturalArchCount}/{report.GeothermalSpringCount}/{report.GlacialRidgeCount}, " +
        $"distinct {report.DistinctGeneratedKindCount}, scenic landmarks {report.ScenicLandmarkCount} ({report.Reason})");
}

static TerrainArtifactSmokeReport ValidateOpenWorldArtifactExport(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan,
    string outputDirectory,
    int imageSize)
{
    TerrainMapRaster baseMap = TerrainMapExporter.CreateRaster(profile, plan.Center, plan.WorldSize, imageSize, TerrainMapLayer.Biome);
    TerrainMapRaster planMap = TerrainWorldPlanExporter.CreatePlanRaster(plan, profile, imageSize, TerrainMapLayer.Biome);
    AnalyzeArtifactMap(baseMap, planMap, out int distinctColorBuckets, out int nonDarkSampleCount, out int overlayChangedPixels, out float maxOverlayColorDelta);
    bool mapRasterSnapshotIsolated = TerrainMapRasterSnapshotIsolated(planMap);
    TerrainMapRaster traversalCostMap = TerrainMapExporter.CreateRaster(profile, plan.Center, plan.WorldSize, imageSize, TerrainMapLayer.TraversalCost);
    AnalyzeTraversalCostMap(traversalCostMap, out int traversalCostColorBuckets, out int traversalCostNonDarkSamples, out int traversalCostBlockedSamples);
    int traversalCostGridSize = Math.Clamp(imageSize / 2, 32, 128);
    TerrainTraversalCostGrid traversalCostGrid = TerrainMapExporter.CreateTraversalCostGrid(
        profile,
        plan.Center,
        plan.WorldSize,
        traversalCostGridSize);
    AnalyzeTraversalCostGrid(
        traversalCostGrid,
        profile,
        out int traversalCostGridFiniteSamples,
        out int traversalCostGridBlockedSamples,
        out float traversalCostGridMinCost,
        out float traversalCostGridMaxCost,
        out bool traversalCostGridSamplesMatchClassifier,
        out bool traversalCostGridSnapshotIsolated);

    TerrainWorldPlanArtifactResult export = TerrainWorldPlanExporter.SaveOpenWorldArtifacts(
        plan,
        profile,
        imageSize,
        outputDirectory,
        TerrainMapLayer.Biome);
    string traversalCostPath = System.IO.Path.Combine(FileSystemPath(outputDirectory), "terrain_traversal_cost.png");
    Error traversalCostSaveError = TerrainMapExporter.SaveRasterPng(traversalCostMap, traversalCostPath);

    string mapFilePath = FileSystemPath(export.MapPath);
    string reportFilePath = FileSystemPath(export.ReportPath);
    string traversalCostFilePath = FileSystemPath(traversalCostPath);
    bool mapExists = System.IO.File.Exists(mapFilePath);
    bool reportExists = System.IO.File.Exists(reportFilePath);
    bool traversalCostExists = System.IO.File.Exists(traversalCostFilePath);
    long mapBytes = mapExists ? new System.IO.FileInfo(mapFilePath).Length : 0L;
    long reportBytes = reportExists ? new System.IO.FileInfo(reportFilePath).Length : 0L;
    long traversalCostBytes = traversalCostExists ? new System.IO.FileInfo(traversalCostFilePath).Length : 0L;
    string reportText = reportExists ? System.IO.File.ReadAllText(reportFilePath) : string.Empty;
    bool reportContainsRequiredSections = ReportContainsRequiredArtifactSections(
        reportText,
        profile,
        plan,
        export.MapPath);

    bool mapHasContent =
        planMap.PixelCount == planMap.Width * planMap.Height &&
        distinctColorBuckets >= 24 &&
        nonDarkSampleCount >= 512 &&
        overlayChangedPixels >= Mathf.Max(256, imageSize) &&
        maxOverlayColorDelta >= 0.04f &&
        mapRasterSnapshotIsolated;
    bool traversalCostMapHasContent =
        traversalCostColorBuckets >= 8 &&
        traversalCostNonDarkSamples >= 512 &&
        traversalCostBlockedSamples >= 16;
    bool traversalCostGridHasContent =
        traversalCostGrid.Width == traversalCostGridSize &&
        traversalCostGrid.Height == traversalCostGridSize &&
        traversalCostGrid.SampleCount == traversalCostGridSize * traversalCostGridSize &&
        traversalCostGridFiniteSamples >= 64 &&
        traversalCostGridBlockedSamples >= 4 &&
        traversalCostGridMaxCost > traversalCostGridMinCost + 0.05f &&
        traversalCostGridSamplesMatchClassifier &&
        traversalCostGridSnapshotIsolated;
    bool filesLookValid =
        export.MapSaveError == Error.Ok &&
        export.ReportSaveError == Error.Ok &&
        traversalCostSaveError == Error.Ok &&
        mapExists &&
        reportExists &&
        traversalCostExists &&
        mapBytes >= 4096 &&
        reportBytes >= 2048 &&
        traversalCostBytes >= 2048;
    bool passed =
        export.Passed &&
        filesLookValid &&
        mapHasContent &&
        traversalCostMapHasContent &&
        traversalCostGridHasContent &&
        reportContainsRequiredSections;
    string reason = passed
        ? "open world map, traversal cost map, and report artifacts exported with visible terrain semantics"
        : ArtifactFailureReason(export, filesLookValid, mapHasContent, traversalCostMapHasContent, traversalCostGridHasContent, reportContainsRequiredSections);

    return new TerrainArtifactSmokeReport(
        passed,
        outputDirectory,
        export.MapPath,
        export.ReportPath,
        traversalCostPath,
        mapBytes,
        reportBytes,
        traversalCostBytes,
        imageSize,
        distinctColorBuckets,
        nonDarkSampleCount,
        overlayChangedPixels,
        maxOverlayColorDelta,
        mapRasterSnapshotIsolated,
        traversalCostColorBuckets,
        traversalCostBlockedSamples,
        traversalCostGridSize,
        traversalCostGridFiniteSamples,
        traversalCostGridBlockedSamples,
        traversalCostGridSnapshotIsolated,
        reportContainsRequiredSections,
        reason);
}

static void AnalyzeArtifactMap(
    TerrainMapRaster baseMap,
    TerrainMapRaster planMap,
    out int distinctColorBuckets,
    out int nonDarkSampleCount,
    out int overlayChangedPixels,
    out float maxOverlayColorDelta)
{
    int width = planMap.Width;
    int height = planMap.Height;
    var colorBuckets = new HashSet<int>();
    distinctColorBuckets = 0;
    nonDarkSampleCount = 0;
    overlayChangedPixels = 0;
    maxOverlayColorDelta = 0.0f;

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            Color planColor = planMap.GetPixel(x, y);
            colorBuckets.Add(QuantizedColorKey(planColor));
            float brightness = (planColor.R + planColor.G + planColor.B) / 3.0f;
            if (brightness > 0.08f)
            {
                nonDarkSampleCount++;
            }

            float overlayDelta = ColorDistance(planColor, baseMap.GetPixel(x, y));
            if (overlayDelta > 0.015f)
            {
                overlayChangedPixels++;
                maxOverlayColorDelta = Mathf.Max(maxOverlayColorDelta, overlayDelta);
            }
        }
    }

    distinctColorBuckets = colorBuckets.Count;
}

static bool TerrainMapRasterSnapshotIsolated(TerrainMapRaster raster)
{
    if (raster.Width <= 0 || raster.Height <= 0 || raster.PixelCount < raster.Width * raster.Height)
    {
        return false;
    }

    Color firstPixel = raster.GetPixel(0, 0);
    Color[] pixelSnapshot = raster.ToPixelArray();
    if (pixelSnapshot.Length == 0)
    {
        return false;
    }

    pixelSnapshot[0] = Colors.Magenta;
    bool snapshotMutationIsolated = ColorDistance(firstPixel, raster.GetPixel(0, 0)) <= TerrainDeterminismContract.ExactFloatEpsilon &&
        ColorDistance(pixelSnapshot[0], raster.GetPixel(0, 0)) > TerrainDeterminismContract.ExactFloatEpsilon;

    Color[] constructorPixels = raster.ToPixelArray();
    TerrainMapRaster constructed = new(raster.Width, raster.Height, constructorPixels);
    constructorPixels[0] = Colors.Cyan;
    bool constructorInputIsolated = ColorDistance(constructed.GetPixel(0, 0), firstPixel) <= TerrainDeterminismContract.ExactFloatEpsilon;

    return snapshotMutationIsolated && constructorInputIsolated;
}

static void AnalyzeTraversalCostMap(
    TerrainMapRaster traversalCostMap,
    out int distinctColorBuckets,
    out int nonDarkSampleCount,
    out int blockedSampleCount)
{
    var colorBuckets = new HashSet<int>();
    nonDarkSampleCount = 0;
    blockedSampleCount = 0;

    for (int y = 0; y < traversalCostMap.Height; y++)
    {
        for (int x = 0; x < traversalCostMap.Width; x++)
        {
            Color color = traversalCostMap.GetPixel(x, y);
            colorBuckets.Add(QuantizedColorKey(color));
            float brightness = (color.R + color.G + color.B) / 3.0f;
            if (brightness > 0.08f)
            {
                nonDarkSampleCount++;
            }

            if (brightness <= 0.12f)
            {
                blockedSampleCount++;
            }
        }
    }

    distinctColorBuckets = colorBuckets.Count;
}

static void AnalyzeTraversalCostGrid(
    TerrainTraversalCostGrid grid,
    TerrainGenerationProfile profile,
    out int finiteSampleCount,
    out int blockedSampleCount,
    out float minFiniteCost,
    out float maxFiniteCost,
    out bool sampledValuesMatchClassifier,
    out bool snapshotIsolated)
{
    finiteSampleCount = 0;
    blockedSampleCount = 0;
    minFiniteCost = float.PositiveInfinity;
    maxFiniteCost = float.NegativeInfinity;
    sampledValuesMatchClassifier = grid.Width > 0 &&
        grid.Height > 0 &&
        grid.SampleCount >= grid.Width * grid.Height;
    snapshotIsolated = false;

    foreach (TerrainTraversalCost sample in grid.Samples)
    {
        if (sample.IsBlocked)
        {
            blockedSampleCount++;
        }

        if (!float.IsPositiveInfinity(sample.Cost))
        {
            finiteSampleCount++;
            minFiniteCost = Math.Min(minFiniteCost, sample.Cost);
            maxFiniteCost = Math.Max(maxFiniteCost, sample.Cost);
        }
    }

    if (grid.SampleCount > 0)
    {
        TerrainTraversalCost firstSample = grid.GetSample(0, 0);
        TerrainTraversalCost[] sampleSnapshot = grid.ToSampleArray();
        sampleSnapshot[0] = default;
        snapshotIsolated = TerrainTraversalCostsMatch(firstSample, grid.GetSample(0, 0)) &&
            !TerrainTraversalCostsMatch(sampleSnapshot[0], grid.GetSample(0, 0));
    }

    if (finiteSampleCount == 0)
    {
        minFiniteCost = 0.0f;
        maxFiniteCost = 0.0f;
    }

    if (!sampledValuesMatchClassifier)
    {
        return;
    }

    foreach ((int x, int y) in TraversalGridProbePoints(grid.Width, grid.Height))
    {
        TerrainTraversalCost actual = grid.GetSample(x, y);
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(actual.WorldPosition, profile);
        TerrainSample surface = TerrainSampler.SampleWithSlope(actual.WorldPosition, profile, spacing: 24.0f);
        TerrainTraversalCost expected = TerrainSemanticClassifier.ClassifyTraversalCost(field, surface, profile);
        if (!TerrainTraversalCostsMatch(expected, actual))
        {
            sampledValuesMatchClassifier = false;
            return;
        }
    }
}

static IEnumerable<(int X, int Y)> TraversalGridProbePoints(int width, int height)
{
    if (width <= 0 || height <= 0)
    {
        yield break;
    }

    yield return (0, 0);
    yield return (width - 1, 0);
    yield return (0, height - 1);
    yield return (width - 1, height - 1);
    yield return (width / 2, height / 2);
}

static int QuantizedColorKey(Color color)
{
    int r = Mathf.Clamp(Mathf.RoundToInt(color.R * 15.0f), 0, 15);
    int g = Mathf.Clamp(Mathf.RoundToInt(color.G * 15.0f), 0, 15);
    int b = Mathf.Clamp(Mathf.RoundToInt(color.B * 15.0f), 0, 15);
    return (r << 8) | (g << 4) | b;
}

static bool ReportContainsRequiredArtifactSections(
    string reportText,
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan,
    string mapPath)
{
    return reportText.Contains("Open World Terrain Plan", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain API Contract: {TerrainApiVersion.Contract}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain API Version: {TerrainApiVersion.Version}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain Plan Contract: {TerrainWorldPlanSerializer.Contract}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain Generator Version: {TerrainWorldPlanSerializer.GeneratorVersion}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain Determinism Contract: {TerrainDeterminismContract.Contract}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain Profile Hash: {profile.StableHash()}", StringComparison.Ordinal) &&
        reportText.Contains(FormattableString.Invariant($"Center: {plan.Center.X:0.##}, {plan.Center.Y:0.##}"), StringComparison.Ordinal) &&
        reportText.Contains(FormattableString.Invariant($"World size: {plan.WorldSize:0.##} meters"), StringComparison.Ordinal) &&
        reportText.Contains(FormattableString.Invariant($"Planning grid: {plan.GridResolution} x {plan.GridResolution}"), StringComparison.Ordinal) &&
        reportText.Contains($"Map: {mapPath}", StringComparison.Ordinal) &&
        reportText.Contains("Terrain Quality Gate", StringComparison.Ordinal) &&
        reportText.Contains("Open World Planning Gate", StringComparison.Ordinal) &&
        reportText.Contains("Open World Experience Gate", StringComparison.Ordinal) &&
        reportText.Contains("Settlement Development", StringComparison.Ordinal) &&
        reportText.Contains("Settlement Network", StringComparison.Ordinal) &&
        reportText.Contains("Connected settlement ratio", StringComparison.Ordinal) &&
        reportText.Contains("Biome Counts", StringComparison.Ordinal) &&
        reportText.Contains("Route Counts", StringComparison.Ordinal) &&
        reportText.Contains("Top Points Of Interest", StringComparison.Ordinal);
}

static string ArtifactFailureReason(
    TerrainWorldPlanArtifactResult export,
    bool filesLookValid,
    bool mapHasContent,
    bool traversalCostMapHasContent,
    bool traversalCostGridHasContent,
    bool reportContainsRequiredSections)
{
    if (!export.PlanningGate.Passed || !export.QualityGate.Passed || !export.ExperienceGate.Passed)
    {
        return "artifact export plan gates did not pass";
    }

    if (!filesLookValid)
    {
        return $"artifact files were not written correctly ({export.MapSaveError}/{export.ReportSaveError})";
    }

    if (!mapHasContent)
    {
        return "artifact map did not contain enough terrain color variety or route/POI overlay pixels";
    }

    if (!traversalCostMapHasContent)
    {
        return "artifact traversal cost map did not contain enough traversal cost variation or blocked terrain";
    }

    if (!traversalCostGridHasContent)
    {
        return "artifact traversal cost grid did not contain stable classifier-backed cost samples";
    }

    if (!reportContainsRequiredSections)
    {
        return "artifact report did not include all required open world sections";
    }

    return "artifact export failed";
}

static void PrintArtifactSmoke(TerrainArtifactSmokeReport report)
{
    Console.WriteLine(
        $"Open world artifact smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"image {report.ImageSize} px, map {report.MapBytes / 1024.0:0.0} KB, traversal {report.TraversalCostMapBytes / 1024.0:0.0} KB, report {report.ReportBytes / 1024.0:0.0} KB, " +
        $"colors {report.DistinctColorBuckets}, overlay pixels {report.OverlayChangedPixels}, " +
        $"max overlay delta {report.MaxOverlayColorDelta:0.000}, raster snapshot {(report.MapRasterSnapshotIsolated ? "pass" : "fail")}, traversal colors/blocked {report.TraversalCostColorBuckets}/{report.TraversalCostBlockedSamples}, " +
        $"grid {report.TraversalCostGridSize}px finite/blocked {report.TraversalCostGridFiniteSamples}/{report.TraversalCostGridBlockedSamples}, " +
        $"snapshot {(report.TraversalCostGridSnapshotIsolated ? "pass" : "fail")}, sections {(report.ReportContainsRequiredSections ? "yes" : "no")} ({report.Reason})");
    Console.WriteLine($"Artifact paths: {report.MapPath}, {report.TraversalCostMapPath}, {report.ReportPath}");
}

static TerrainPlanJsonSmokeReport ValidateTerrainPlanJsonRoundtrip(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    try
    {
        string json = TerrainWorldPlanSerializer.ToJson(plan, profile);
        JsonObject? root = JsonNode.Parse(json) as JsonObject;
        bool metadataPassed = root is not null && PlanJsonMetadataMatches(root, profile, plan);
        bool schemaShapePassed = root is not null && PlanJsonSchemaShapeMatches(root, plan);

        bool stringLoadPassed = TerrainWorldPlanSerializer.TryFromJson(
            json,
            profile,
            out TerrainWorldPlan? loadedPlan,
            out string stringLoadError);
        bool stringRoundtripMatches = stringLoadPassed &&
            loadedPlan is not null &&
            TerrainPlansMatchForJson(plan, loadedPlan);
        bool roundtripIsolationPassed = loadedPlan is not null && RoundtripPlanIsolated(plan, loadedPlan);
        bool setWorldPlanPassed = loadedPlan is not null && RoundtripPlanCanBeAssignedToRuntimeWorld(profile, loadedPlan);

        string outputPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "dao_terrain_validation",
            $"seed_{profile.Seed}",
            "terrain_plan_roundtrip.json");
        Error saveError = TerrainWorldPlanSerializer.SaveJson(plan, profile, outputPath);
        string filePath = FileSystemPath(outputPath);
        bool fileExists = System.IO.File.Exists(filePath);
        long fileBytes = fileExists ? new System.IO.FileInfo(filePath).Length : 0L;
        TerrainWorldPlan? filePlan = null;
        string fileLoadError = string.Empty;
        bool fileLoadPassed =
            saveError == Error.Ok &&
            fileExists &&
            fileBytes >= json.Length;
        if (fileLoadPassed)
        {
            fileLoadPassed = TerrainWorldPlanSerializer.TryLoadJson(
                outputPath,
                profile,
                out filePlan,
                out fileLoadError);
        }

        bool fileRoundtripMatches = fileLoadPassed &&
            filePlan is not null &&
            TerrainPlansMatchForJson(plan, filePlan);

        bool seedMismatchRejected = !TerrainWorldPlanSerializer.TryFromJson(
            json,
            profile with { Seed = profile.Seed + 1 },
            out _,
            out _);
        bool profileHashMismatchRejected = !TerrainWorldPlanSerializer.TryFromJson(
            json,
            profile with { ChunkSize = profile.ChunkSize + 1.0f },
            out _,
            out _);
        bool legacyApiVersionAccepted = AcceptsCompatibleApiVersion(json, profile, "1.0.0");
        bool previousApiVersionAccepted = AcceptsCompatibleApiVersion(json, profile, "1.1.0");
        bool versionDriftRejected = RejectsVersionDrift(json, profile);
        bool enumNameDriftRejected = RejectsEnumNameDrift(json, profile);
        bool enumValueDriftRejected = RejectsEnumValueDrift(json, profile);

        bool passed =
            metadataPassed &&
            schemaShapePassed &&
            stringLoadPassed &&
            stringRoundtripMatches &&
            roundtripIsolationPassed &&
            setWorldPlanPassed &&
            fileLoadPassed &&
            fileRoundtripMatches &&
            seedMismatchRejected &&
            profileHashMismatchRejected &&
            legacyApiVersionAccepted &&
            previousApiVersionAccepted &&
            versionDriftRejected &&
            enumNameDriftRejected &&
            enumValueDriftRejected;
        string reason = passed
            ? "plan JSON schema roundtrips through string and file persistence with version/profile/enum drift checks"
            : PlanJsonFailureReason(
                metadataPassed,
                schemaShapePassed,
                stringLoadPassed,
                stringRoundtripMatches,
                roundtripIsolationPassed,
                setWorldPlanPassed,
                fileLoadPassed,
                fileRoundtripMatches,
                seedMismatchRejected,
                profileHashMismatchRejected,
                legacyApiVersionAccepted,
                previousApiVersionAccepted,
                versionDriftRejected,
                enumNameDriftRejected,
                enumValueDriftRejected,
                saveError,
                stringLoadError,
                fileLoadError);

        return new TerrainPlanJsonSmokeReport(
            passed,
            metadataPassed,
            schemaShapePassed,
            stringLoadPassed,
            stringRoundtripMatches,
            fileLoadPassed,
            fileRoundtripMatches,
            seedMismatchRejected,
            profileHashMismatchRejected,
            legacyApiVersionAccepted,
            previousApiVersionAccepted,
            versionDriftRejected,
            enumNameDriftRejected,
            enumValueDriftRejected,
            roundtripIsolationPassed,
            setWorldPlanPassed,
            json.Length,
            fileBytes,
            reason);
    }
    catch (Exception ex)
    {
        return new TerrainPlanJsonSmokeReport(
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            0,
            0,
            $"plan JSON smoke threw {ex.GetType().Name}: {ex.Message}");
    }
}

static bool PlanJsonMetadataMatches(JsonObject root, TerrainGenerationProfile profile, TerrainWorldPlan plan)
{
    return JsonStringEquals(root, "contract", TerrainWorldPlanSerializer.Contract) &&
        JsonStringEquals(root, "apiContract", TerrainApiVersion.Contract) &&
        JsonStringEquals(root, "apiVersion", TerrainApiVersion.Version) &&
        JsonStringEquals(root, "generatorVersion", TerrainWorldPlanSerializer.GeneratorVersion) &&
        JsonIntEquals(root, "seed", profile.Seed) &&
        JsonStringEquals(root, "profileHash", profile.StableHash()) &&
        JsonArrayCount(root, "regions") == plan.Regions.Length &&
        JsonArrayCount(root, "pointsOfInterest") == plan.PointsOfInterest.Length &&
        JsonArrayCount(root, "routes") == plan.Routes.Length &&
        root["center"] is JsonObject &&
        root["reports"]?["quality"] is JsonObject &&
        root["reports"]?["planning"] is JsonObject &&
        root["reports"]?["experience"] is JsonObject &&
        HasEnumNode(root, "regions", "biome") &&
        HasEnumNode(root, "regions", "landscape") &&
        HasEnumNode(root, "regions", "region") &&
        HasEnumNode(root, "pointsOfInterest", "kind") &&
        HasEnumNode(root, "routes", "kind");
}

static bool PlanJsonSchemaShapeMatches(JsonObject root, TerrainWorldPlan plan)
{
    if (!VectorNodeUsesXz(root["center"] as JsonObject))
    {
        return false;
    }

    JsonObject? firstRegionWorld = FirstObjectProperty(root, "regions", "world");
    if (plan.Regions.Length > 0 && !VectorNodeUsesXz(firstRegionWorld))
    {
        return false;
    }

    JsonObject? firstPointWorld = FirstObjectProperty(root, "pointsOfInterest", "world");
    if (plan.PointsOfInterest.Length > 0 && !VectorNodeUsesXz(firstPointWorld))
    {
        return false;
    }

    JsonObject? firstRoute = FirstArrayObject(root, "routes");
    if (plan.Routes.Length == 0)
    {
        return firstRoute is null;
    }

    if (firstRoute is null || firstRoute["waypoints"] is not JsonArray waypoints)
    {
        return false;
    }

    if (plan.Routes[0].Waypoints.Length == 0)
    {
        return waypoints.Count == 0;
    }

    return waypoints.Count == plan.Routes[0].Waypoints.Length &&
        VectorNodeUsesXz(waypoints[0] as JsonObject);
}

static bool VectorNodeUsesXz(JsonObject? node)
{
    return node is not null &&
        node["x"] is not null &&
        node["z"] is not null &&
        node["y"] is null;
}

static bool JsonStringEquals(JsonObject root, string propertyName, string expected)
{
    return root.TryGetPropertyValue(propertyName, out JsonNode? node) &&
        node is not null &&
        string.Equals(node.GetValue<string>(), expected, StringComparison.Ordinal);
}

static bool JsonIntEquals(JsonObject root, string propertyName, int expected)
{
    return root.TryGetPropertyValue(propertyName, out JsonNode? node) &&
        node is not null &&
        node.GetValue<int>() == expected;
}

static int JsonArrayCount(JsonObject root, string propertyName)
{
    return root.TryGetPropertyValue(propertyName, out JsonNode? node) && node is JsonArray array
        ? array.Count
        : -1;
}

static bool HasEnumNode(JsonObject root, string arrayName, string propertyName)
{
    JsonObject? enumNode = FirstObjectProperty(root, arrayName, propertyName);
    return enumNode is not null &&
        enumNode["name"] is not null &&
        enumNode["value"] is not null;
}

static JsonObject? FirstObjectProperty(JsonObject root, string arrayName, string propertyName)
{
    JsonObject? firstObject = FirstArrayObject(root, arrayName);
    return firstObject?[propertyName] as JsonObject;
}

static JsonObject? FirstArrayObject(JsonObject root, string arrayName)
{
    if (root[arrayName] is not JsonArray array || array.Count == 0)
    {
        return null;
    }

    return array[0] as JsonObject;
}

static bool RejectsEnumNameDrift(string json, TerrainGenerationProfile profile)
{
    JsonObject? root = JsonNode.Parse(json) as JsonObject;
    JsonObject? enumNode = root is null ? null : FirstObjectProperty(root, "regions", "biome");
    if (root is null || enumNode is null)
    {
        return false;
    }

    enumNode["name"] = "__invalid_enum_name__";
    return !TerrainWorldPlanSerializer.TryFromJson(root.ToJsonString(), profile, out _, out _);
}

static bool RejectsVersionDrift(string json, TerrainGenerationProfile profile)
{
    return RejectsStringPropertyDrift(json, profile, "contract", "__terrain_plan_v2__") &&
        RejectsStringPropertyDrift(json, profile, "apiContract", "__terrain_api_v2__") &&
        RejectsStringPropertyDrift(json, profile, "apiVersion", "99.0.0") &&
        RejectsStringPropertyDrift(json, profile, "generatorVersion", "99.0.0");
}

static bool AcceptsCompatibleApiVersion(
    string json,
    TerrainGenerationProfile profile,
    string apiVersion)
{
    JsonObject? root = JsonNode.Parse(json) as JsonObject;
    if (root is null || root["apiVersion"] is null)
    {
        return false;
    }

    root["apiVersion"] = apiVersion;
    return TerrainWorldPlanSerializer.TryFromJson(root.ToJsonString(), profile, out TerrainWorldPlan? plan, out _) &&
        plan is not null;
}

static bool RejectsStringPropertyDrift(
    string json,
    TerrainGenerationProfile profile,
    string propertyName,
    string invalidValue)
{
    JsonObject? root = JsonNode.Parse(json) as JsonObject;
    if (root is null || root[propertyName] is null)
    {
        return false;
    }

    root[propertyName] = invalidValue;
    return !TerrainWorldPlanSerializer.TryFromJson(root.ToJsonString(), profile, out _, out _);
}

static bool RejectsEnumValueDrift(string json, TerrainGenerationProfile profile)
{
    JsonObject? root = JsonNode.Parse(json) as JsonObject;
    JsonObject? enumNode = root is null ? null : FirstObjectProperty(root, "pointsOfInterest", "kind");
    if (root is null || enumNode is null)
    {
        return false;
    }

    enumNode["value"] = 9999;
    return !TerrainWorldPlanSerializer.TryFromJson(root.ToJsonString(), profile, out _, out _);
}

static bool TerrainPlansMatchForJson(TerrainWorldPlan expected, TerrainWorldPlan actual)
{
    if (!ExactPositionEquals(expected.Center, actual.Center) ||
        !PlanFloatEquals(expected.WorldSize, actual.WorldSize) ||
        expected.GridResolution != actual.GridResolution ||
        expected.Regions.Length != actual.Regions.Length ||
        expected.PointsOfInterest.Length != actual.PointsOfInterest.Length ||
        expected.Routes.Length != actual.Routes.Length ||
        !PublicValuePropertiesMatch(expected.QualityReport, actual.QualityReport) ||
        !PublicValuePropertiesMatch(expected.PlanningReport, actual.PlanningReport) ||
        !PublicValuePropertiesMatch(expected.ExperienceReport, actual.ExperienceReport))
    {
        return false;
    }

    for (int i = 0; i < expected.Regions.Length; i++)
    {
        if (!RegionsMatchForJson(expected.Regions[i], actual.Regions[i]))
        {
            return false;
        }
    }

    for (int i = 0; i < expected.PointsOfInterest.Length; i++)
    {
        if (!PointsMatchForJson(expected.PointsOfInterest[i], actual.PointsOfInterest[i]))
        {
            return false;
        }
    }

    for (int i = 0; i < expected.Routes.Length; i++)
    {
        if (!RoutesMatchForJson(expected.Routes[i], actual.Routes[i]))
        {
            return false;
        }
    }

    return true;
}

static bool RegionsMatchForJson(TerrainWorldRegion expected, TerrainWorldRegion actual)
{
    return expected.GridX == actual.GridX &&
        expected.GridY == actual.GridY &&
        ExactPositionEquals(expected.WorldPosition, actual.WorldPosition) &&
        PlanFloatEquals(expected.Height, actual.Height) &&
        PlanFloatEquals(expected.River, actual.River) &&
        PlanFloatEquals(expected.ScenicPotential, actual.ScenicPotential) &&
        PlanFloatEquals(expected.Traversability, actual.Traversability) &&
        PlanFloatEquals(expected.Exposure, actual.Exposure) &&
        PlanFloatEquals(expected.ResourcePotential, actual.ResourcePotential) &&
        PlanFloatEquals(expected.HazardPotential, actual.HazardPotential) &&
        PlanFloatEquals(expected.EncounterPotential, actual.EncounterPotential) &&
        expected.BiomeKind == actual.BiomeKind &&
        expected.LandscapeKind == actual.LandscapeKind &&
        expected.RegionKind == actual.RegionKind;
}

static bool PointsMatchForJson(TerrainWorldPointOfInterest expected, TerrainWorldPointOfInterest actual)
{
    return expected.Id == actual.Id &&
        expected.Kind == actual.Kind &&
        ExactPositionEquals(expected.WorldPosition, actual.WorldPosition) &&
        expected.GridX == actual.GridX &&
        expected.GridY == actual.GridY &&
        PlanFloatEquals(expected.Score, actual.Score) &&
        PlanFloatEquals(expected.Height, actual.Height) &&
        PlanFloatEquals(expected.ScenicPotential, actual.ScenicPotential) &&
        PlanFloatEquals(expected.Traversability, actual.Traversability) &&
        expected.BiomeKind == actual.BiomeKind &&
        expected.LandscapeKind == actual.LandscapeKind &&
        expected.SettlementTier == actual.SettlementTier &&
        string.Equals(expected.DebugName, actual.DebugName, StringComparison.Ordinal);
}

static bool RoutesMatchForJson(TerrainWorldRoute expected, TerrainWorldRoute actual)
{
    if (expected.FromPointId != actual.FromPointId ||
        expected.ToPointId != actual.ToPointId ||
        expected.Kind != actual.Kind ||
        !PlanFloatEquals(expected.Cost, actual.Cost) ||
        !PlanFloatEquals(expected.AverageScenicPotential, actual.AverageScenicPotential) ||
        !PlanFloatEquals(expected.AverageTraversability, actual.AverageTraversability) ||
        expected.Waypoints.Length != actual.Waypoints.Length)
    {
        return false;
    }

    for (int i = 0; i < expected.Waypoints.Length; i++)
    {
        if (!ExactPositionEquals(expected.Waypoints[i], actual.Waypoints[i]))
        {
            return false;
        }
    }

    return true;
}

static bool PublicValuePropertiesMatch<T>(T expected, T actual)
{
    foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
    {
        object? expectedValue = property.GetValue(expected);
        object? actualValue = property.GetValue(actual);
        if (expectedValue is float expectedFloat && actualValue is float actualFloat)
        {
            if (!PlanFloatEquals(expectedFloat, actualFloat))
            {
                return false;
            }
        }
        else if (!Equals(expectedValue, actualValue))
        {
            return false;
        }
    }

    return true;
}

static bool RoundtripPlanIsolated(TerrainWorldPlan original, TerrainWorldPlan roundtrip)
{
    bool isolated = true;
    if (original.Regions.Length > 0 && roundtrip.Regions.Length > 0)
    {
        TerrainWorldRegion originalRegion = original.Regions[0];
        roundtrip.Regions[0] = originalRegion with { Height = originalRegion.Height + 1000.0f };
        isolated = PlanFloatEquals(original.Regions[0].Height, originalRegion.Height);
    }

    if (isolated && original.PointsOfInterest.Length > 0 && roundtrip.PointsOfInterest.Length > 0)
    {
        TerrainWorldPointOfInterest originalPoint = original.PointsOfInterest[0];
        roundtrip.PointsOfInterest[0] = originalPoint with { Id = originalPoint.Id + 1000 };
        isolated = original.PointsOfInterest[0].Id == originalPoint.Id;
    }

    if (isolated && original.Routes.Length > 0 && roundtrip.Routes.Length > 0)
    {
        TerrainWorldRoute originalRoute = original.Routes[0];
        TerrainWorldRoute roundtripRoute = roundtrip.Routes[0];
        roundtrip.Routes[0] = roundtripRoute with { FromPointId = roundtripRoute.FromPointId + 1000 };
        isolated = original.Routes[0].FromPointId == originalRoute.FromPointId;

        if (isolated && originalRoute.Waypoints.Length > 0 && roundtrip.Routes[0].Waypoints.Length > 0)
        {
            Vector2 originalWaypoint = originalRoute.Waypoints[0];
            roundtrip.Routes[0].Waypoints[0] = originalWaypoint + new Vector2(1000.0f, -1000.0f);
            isolated = ExactPositionEquals(original.Routes[0].Waypoints[0], originalWaypoint);
        }
    }

    return isolated;
}

static bool RoundtripPlanCanBeAssignedToRuntimeWorld(
    TerrainGenerationProfile profile,
    TerrainWorldPlan roundtrip)
{
    TerrainWorld world = CreateTerrainWorldFacadeProbe(profile, worldPlan: null);
    world.SetWorldPlan(roundtrip);
    bool assignedPlanPassed =
        world.TryGetWorldPlan(out TerrainWorldPlan? assignedPlan) &&
        assignedPlan is not null &&
        !ReferenceEquals(assignedPlan, roundtrip) &&
        TerrainPlansMatchForJson(roundtrip, assignedPlan) &&
        RuntimeWorldPlanFacadeIsolated(world, assignedPlan, roundtrip);

    return assignedPlanPassed &&
        world.GetPointsOfInterest().Length == roundtrip.PointsOfInterest.Length &&
        world.GetRoutes().Length == roundtrip.Routes.Length &&
        world.TryGetWorldPlanSnapshot(out TerrainWorldPlanSnapshot? snapshot) &&
        snapshot is not null &&
        snapshot.PointsOfInterest.Length == roundtrip.PointsOfInterest.Length &&
        snapshot.Routes.Length == roundtrip.Routes.Length &&
        snapshot.Regions.Length == roundtrip.Regions.Length;
}

static bool PlanFloatEquals(float expected, float actual)
{
    return ExactFloatEquals(expected, actual);
}

static bool ExactFloatEquals(float expected, float actual)
{
    return Math.Abs(expected - actual) <= TerrainDeterminismContract.ExactFloatEpsilon;
}

static bool ExactDoubleEquals(double expected, double actual)
{
    return Math.Abs(expected - actual) <= TerrainDeterminismContract.ExactFloatEpsilon;
}

static bool ExactPositionEquals(Vector2 expected, Vector2 actual)
{
    return expected.DistanceSquaredTo(actual) <= TerrainDeterminismContract.Squared(TerrainDeterminismContract.ExactPositionEpsilon);
}

static bool ContractPositionEquals(Vector2 expected, Vector2 actual)
{
    return expected.DistanceSquaredTo(actual) <= TerrainDeterminismContract.Squared(TerrainDeterminismContract.PositionEpsilon);
}

static string PlanJsonFailureReason(
    bool metadataPassed,
    bool schemaShapePassed,
    bool stringLoadPassed,
    bool stringRoundtripMatches,
    bool roundtripIsolationPassed,
    bool setWorldPlanPassed,
    bool fileLoadPassed,
    bool fileRoundtripMatches,
    bool seedMismatchRejected,
    bool profileHashMismatchRejected,
    bool legacyApiVersionAccepted,
    bool previousApiVersionAccepted,
    bool versionDriftRejected,
    bool enumNameDriftRejected,
    bool enumValueDriftRejected,
    Error saveError,
    string stringLoadError,
    string fileLoadError)
{
    if (!metadataPassed)
    {
        return "plan JSON metadata or required schema nodes did not match the contract";
    }

    if (!schemaShapePassed)
    {
        return "plan JSON vector schema did not use stable x/z coordinate nodes or route waypoint arrays";
    }

    if (!stringLoadPassed)
    {
        return $"plan JSON string load failed: {stringLoadError}";
    }

    if (!stringRoundtripMatches)
    {
        return "plan JSON string roundtrip changed plan data";
    }

    if (!roundtripIsolationPassed)
    {
        return "plan JSON roundtrip reused mutable plan array state";
    }

    if (!setWorldPlanPassed)
    {
        return "plan JSON roundtrip could not be assigned through TerrainWorld.SetWorldPlan";
    }

    if (!fileLoadPassed)
    {
        return $"plan JSON file save/load failed ({saveError}): {fileLoadError}";
    }

    if (!fileRoundtripMatches)
    {
        return "plan JSON file roundtrip changed plan data";
    }

    if (!seedMismatchRejected)
    {
        return "plan JSON accepted a mismatched seed";
    }

    if (!profileHashMismatchRejected)
    {
        return "plan JSON accepted a mismatched profile hash";
    }

    if (!legacyApiVersionAccepted)
    {
        return "plan JSON rejected a compatible terrain-api-v1 1.0.0 plan";
    }

    if (!previousApiVersionAccepted)
    {
        return "plan JSON rejected a compatible terrain-api-v1 1.1.0 plan";
    }

    if (!versionDriftRejected)
    {
        return "plan JSON accepted an incompatible contract or version drift";
    }

    if (!enumNameDriftRejected)
    {
        return "plan JSON accepted an enum name drift";
    }

    if (!enumValueDriftRejected)
    {
        return "plan JSON accepted an enum value drift";
    }

    return "plan JSON roundtrip smoke failed";
}

static void PrintPlanJsonSmoke(TerrainPlanJsonSmokeReport report)
{
    Console.WriteLine(
        $"Plan JSON roundtrip smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"json {report.JsonBytes / 1024.0:0.0} KB, file {report.FileBytes / 1024.0:0.0} KB, " +
        $"metadata/schema {(report.MetadataPassed ? "pass" : "fail")}/{(report.SchemaShapePassed ? "pass" : "fail")}, " +
        $"string/file {(report.StringLoadPassed && report.StringRoundtripMatches ? "pass" : "fail")}/{(report.FileLoadPassed && report.FileRoundtripMatches ? "pass" : "fail")}, " +
        $"compat api 1.0/1.1 {(report.LegacyApiVersionAccepted ? "pass" : "fail")}/{(report.PreviousApiVersionAccepted ? "pass" : "fail")}, " +
        $"drift seed/hash/version/enum {(report.SeedMismatchRejected ? "pass" : "fail")}/{(report.ProfileHashMismatchRejected ? "pass" : "fail")}/{(report.VersionDriftRejected ? "pass" : "fail")}/{(report.EnumNameDriftRejected && report.EnumValueDriftRejected ? "pass" : "fail")}, " +
        $"isolation/runtime {(report.RoundtripIsolationPassed ? "pass" : "fail")}/{(report.SetWorldPlanPassed ? "pass" : "fail")} ({report.Reason})");
}

static TerrainEnumContractSmokeReport ValidateTerrainEnumContracts()
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

static bool CheckEnumContract<TEnum>(
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

    var seenValues = new HashSet<int>();
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

static void PrintEnumContractSmoke(TerrainEnumContractSmokeReport report)
{
    Console.WriteLine(
        $"Terrain enum contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"types {report.CheckedTypeCount}, values {report.CheckedValueCount} ({report.Reason})");
}

static TerrainPublicApiShapeSmokeReport ValidateTerrainPublicApiShapeContracts()
{
    try
    {
        int checkedTypeCount = 0;
        int checkedMemberCount = 0;
        string? failureReason = null;
        bool exportedTypeSetPassed = CheckExportedTerrainTypes(out failureReason);
        if (exportedTypeSetPassed)
        {
            checkedTypeCount++;
        }

        bool passed =
            exportedTypeSetPassed &&
            CheckPublicShape<TerrainGenerationProfile>(
                [
                    ("Seed", typeof(int)),
                    ("ChunkSize", typeof(float)),
                    ("BaseResolution", typeof(int)),
                    ("StreamRadiusChunks", typeof(int)),
                    ("CollisionRadiusChunks", typeof(int)),
                    ("MaxLod", typeof(int)),
                    ("HeightScale", typeof(float)),
                    ("SeaLevel", typeof(float)),
                    ("ContinentScale", typeof(float)),
                    ("MountainScale", typeof(float)),
                    ("MountainWeight", typeof(float)),
                    ("ValleyWeight", typeof(float)),
                    ("DetailWeight", typeof(float)),
                    ("VistaFrequency", typeof(float)),
                    ("RiverStrength", typeof(float)),
                    ("RiverCarveDepth", typeof(float)),
                    ("TerraceStrength", typeof(float)),
                    ("SkirtDepth", typeof(float)),
                    ("MaxCompletedTilesPerFrame", typeof(int)),
                    ("MaxQueuedTileJobs", typeof(int)),
                    ("MaxCachedTileData", typeof(int)),
                    ("GenerateCollision", typeof(bool)),
                    ("UseNativeSamplerWhenAvailable", typeof(bool)),
                    ("ScenicLandmarkRuleSetHash", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainTileCoord>(
                [
                    ("X", typeof(int)),
                    ("Z", typeof(int))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainSample>(
                [
                    ("Height", typeof(float)),
                    ("Continental", typeof(float)),
                    ("Mountain", typeof(float)),
                    ("River", typeof(float)),
                    ("Lake", typeof(float)),
                    ("Moisture", typeof(float)),
                    ("Temperature", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("Traversability", typeof(float)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind)),
                    ("Slope", typeof(float)),
                    ("Color", typeof(Color))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldField>(
                [
                    ("WorldPosition", typeof(Vector2)),
                    ("Height", typeof(float)),
                    ("Continent", typeof(float)),
                    ("Basin", typeof(float)),
                    ("Shelf", typeof(float)),
                    ("Mountains", typeof(float)),
                    ("BroadElevation", typeof(float)),
                    ("River", typeof(float)),
                    ("Lake", typeof(float)),
                    ("Moisture", typeof(float)),
                    ("Temperature", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("Traversability", typeof(float)),
                    ("Exposure", typeof(float)),
                    ("ResourcePotential", typeof(float)),
                    ("HazardPotential", typeof(float)),
                    ("EncounterPotential", typeof(float)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldRegion>(
                [
                    ("GridX", typeof(int)),
                    ("GridY", typeof(int)),
                    ("WorldPosition", typeof(Vector2)),
                    ("Height", typeof(float)),
                    ("River", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("Traversability", typeof(float)),
                    ("Exposure", typeof(float)),
                    ("ResourcePotential", typeof(float)),
                    ("HazardPotential", typeof(float)),
                    ("EncounterPotential", typeof(float)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind)),
                    ("RegionKind", typeof(TerrainWorldRegionKind))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldPointOfInterest>(
                [
                    ("Id", typeof(int)),
                    ("Kind", typeof(TerrainPointOfInterestKind)),
                    ("WorldPosition", typeof(Vector2)),
                    ("GridX", typeof(int)),
                    ("GridY", typeof(int)),
                    ("Score", typeof(float)),
                    ("Height", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("Traversability", typeof(float)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind)),
                    ("SettlementTier", typeof(TerrainSettlementTier)),
                    ("DebugName", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldRoute>(
                [
                    ("FromPointId", typeof(int)),
                    ("ToPointId", typeof(int)),
                    ("Kind", typeof(TerrainRouteKind)),
                    ("Cost", typeof(float)),
                    ("AverageScenicPotential", typeof(float)),
                    ("AverageTraversability", typeof(float)),
                    ("Waypoints", typeof(Vector2[]))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldSettingsResource>(
                [
                    ("Seed", typeof(int)),
                    ("ChunkSize", typeof(float)),
                    ("BaseResolution", typeof(int))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainShapeSettingsResource>(
                [
                    ("HeightScale", typeof(float)),
                    ("SeaLevel", typeof(float)),
                    ("ContinentScale", typeof(float)),
                    ("MountainScale", typeof(float)),
                    ("MountainWeight", typeof(float)),
                    ("ValleyWeight", typeof(float)),
                    ("DetailWeight", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainGameplaySettingsResource>(
                [
                    ("VistaFrequency", typeof(float)),
                    ("RiverStrength", typeof(float)),
                    ("RiverCarveDepth", typeof(float)),
                    ("TerraceStrength", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainStreamingSettingsResource>(
                [
                    ("StreamRadiusChunks", typeof(int)),
                    ("CollisionRadiusChunks", typeof(int)),
                    ("MaxLod", typeof(int)),
                    ("MaxCompletedTilesPerFrame", typeof(int)),
                    ("MaxQueuedTileJobs", typeof(int)),
                    ("MaxCachedTileData", typeof(int)),
                    ("GenerateCollision", typeof(bool)),
                    ("UseNativeSamplerWhenAvailable", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainRenderingSettingsResource>(
                [
                    ("SkirtDepth", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainNaturalLandmarkRuleResource>(
                [
                    ("Threshold", typeof(float)),
                    ("BaseScale", typeof(float)),
                    ("ScoreScale", typeof(float)),
                    ("BaseColor", typeof(Color))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainScenicLandmarkRuleSet>(
                [
                    ("Waterfall", typeof(TerrainNaturalLandmarkRuleResource)),
                    ("DuneCrest", typeof(TerrainNaturalLandmarkRuleResource)),
                    ("DesertMonolith", typeof(TerrainNaturalLandmarkRuleResource)),
                    ("CanyonNeedle", typeof(TerrainNaturalLandmarkRuleResource)),
                    ("IceSpire", typeof(TerrainNaturalLandmarkRuleResource)),
                    ("NaturalArch", typeof(TerrainNaturalLandmarkRuleResource)),
                    ("GeothermalSpring", typeof(TerrainNaturalLandmarkRuleResource)),
                    ("GlacialRidge", typeof(TerrainNaturalLandmarkRuleResource))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldPlan>(
                [
                    ("Center", typeof(Vector2)),
                    ("WorldSize", typeof(float)),
                    ("GridResolution", typeof(int)),
                    ("Regions", typeof(TerrainWorldRegion[])),
                    ("PointsOfInterest", typeof(TerrainWorldPointOfInterest[])),
                    ("Routes", typeof(TerrainWorldRoute[])),
                    ("QualityReport", typeof(TerrainQualityReport)),
                    ("PlanningReport", typeof(TerrainWorldPlanningReport)),
                    ("ExperienceReport", typeof(TerrainExperienceReport))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldPlanSnapshot>(
                [
                    ("Center", typeof(Vector2)),
                    ("WorldSize", typeof(float)),
                    ("GridResolution", typeof(int)),
                    ("Regions", typeof(TerrainWorldRegion[])),
                    ("PointsOfInterest", typeof(TerrainWorldPointOfInterest[])),
                    ("Routes", typeof(TerrainWorldRoute[])),
                    ("QualityReport", typeof(TerrainQualityReport)),
                    ("PlanningReport", typeof(TerrainWorldPlanningReport)),
                    ("ExperienceReport", typeof(TerrainExperienceReport))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldPlanningThresholds>(
                [
                    ("MinPointsOfInterest", typeof(int)),
                    ("MinPointOfInterestKinds", typeof(int)),
                    ("MinRoutes", typeof(int)),
                    ("MinRouteKinds", typeof(int)),
                    ("MinConnectedPointRatio", typeof(float)),
                    ("MinConnectedSettlementRatio", typeof(float)),
                    ("MinSettlementRoutes", typeof(int)),
                    ("MinPointOfInterestWorldCoverage", typeof(float)),
                    ("MinRouteWorldCoverage", typeof(float)),
                    ("MinAverageRouteTraversability", typeof(float)),
                    ("MinAverageRouteScenicPotential", typeof(float)),
                    ("MinVillages", typeof(int)),
                    ("MinTowns", typeof(int)),
                    ("MinOasisHubs", typeof(int))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldPlanningReport>(
                [
                    ("PointOfInterestCount", typeof(int)),
                    ("DistinctPointOfInterestKinds", typeof(int)),
                    ("RouteCount", typeof(int)),
                    ("DistinctRouteKinds", typeof(int)),
                    ("ConnectedPointRatio", typeof(float)),
                    ("ConnectedSettlementRatio", typeof(float)),
                    ("SettlementRouteCount", typeof(int)),
                    ("PointOfInterestWorldCoverage", typeof(float)),
                    ("RouteWorldCoverage", typeof(float)),
                    ("AveragePointScore", typeof(float)),
                    ("AverageRouteCost", typeof(float)),
                    ("AverageRouteScenicPotential", typeof(float)),
                    ("AverageRouteTraversability", typeof(float)),
                    ("SettlementCandidateCount", typeof(int)),
                    ("VistaCount", typeof(int)),
                    ("RiverCrossingCount", typeof(int)),
                    ("MountainPassCount", typeof(int)),
                    ("CoastalLandingCount", typeof(int)),
                    ("ResourceGroveCount", typeof(int)),
                    ("AncientSiteCount", typeof(int)),
                    ("CanyonOverlookCount", typeof(int)),
                    ("OasisCount", typeof(int)),
                    ("VillageCount", typeof(int)),
                    ("TownCount", typeof(int)),
                    ("OasisHubCount", typeof(int)),
                    ("PrimaryTrailCount", typeof(int)),
                    ("RiverRoadCount", typeof(int)),
                    ("RidgePassCount", typeof(int)),
                    ("CoastalPathCount", typeof(int)),
                    ("ScenicTrailCount", typeof(int))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldPlanningGateResult>(
                [
                    ("Passed", typeof(bool)),
                    ("Report", typeof(TerrainWorldPlanningReport)),
                    ("Summary", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainQualityThresholds>(
                [
                    ("MinLandRatio", typeof(float)),
                    ("MaxLandRatio", typeof(float)),
                    ("MinRiverRatio", typeof(float)),
                    ("MinScenicRatio", typeof(float)),
                    ("MinTraversableLandRatio", typeof(float)),
                    ("MinDistinctLandscapeKinds", typeof(int)),
                    ("MinDistinctBiomeKinds", typeof(int)),
                    ("MinPlainsGrasslandRatio", typeof(float)),
                    ("MinDesertOasisRatio", typeof(float)),
                    ("MinIslandCoastRatio", typeof(float)),
                    ("MinHillMountainRatio", typeof(float)),
                    ("MinSnowRatio", typeof(float)),
                    ("MinLakeRatio", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainQualityReport>(
                [
                    ("SampleCount", typeof(int)),
                    ("WorldSize", typeof(float)),
                    ("MinHeight", typeof(float)),
                    ("MaxHeight", typeof(float)),
                    ("AverageHeight", typeof(float)),
                    ("LandRatio", typeof(float)),
                    ("OceanRatio", typeof(float)),
                    ("CoastRatio", typeof(float)),
                    ("RiverRatio", typeof(float)),
                    ("ScenicRatio", typeof(float)),
                    ("TraversableLandRatio", typeof(float)),
                    ("DistinctLandscapeKinds", typeof(int)),
                    ("DistinctBiomeKinds", typeof(int)),
                    ("OceanCount", typeof(int)),
                    ("CoastCount", typeof(int)),
                    ("LowlandCount", typeof(int)),
                    ("WetlandCount", typeof(int)),
                    ("ForestBasinCount", typeof(int)),
                    ("RiverValleyCount", typeof(int)),
                    ("CanyonCount", typeof(int)),
                    ("HighlandsCount", typeof(int)),
                    ("MountainMassifCount", typeof(int)),
                    ("SnowfieldCount", typeof(int)),
                    ("VistaPlateauCount", typeof(int)),
                    ("LakeCount", typeof(int)),
                    ("BiomeOceanCount", typeof(int)),
                    ("BiomeCoastCount", typeof(int)),
                    ("IslandCount", typeof(int)),
                    ("PlainsCount", typeof(int)),
                    ("GrasslandCount", typeof(int)),
                    ("DesertCount", typeof(int)),
                    ("OasisCount", typeof(int)),
                    ("ForestCount", typeof(int)),
                    ("BiomeWetlandCount", typeof(int)),
                    ("HillsCount", typeof(int)),
                    ("MountainsCount", typeof(int)),
                    ("BiomeSnowfieldCount", typeof(int)),
                    ("BiomeLakeCount", typeof(int)),
                    ("PlainsGrasslandRatio", typeof(float)),
                    ("DesertOasisRatio", typeof(float)),
                    ("IslandCoastRatio", typeof(float)),
                    ("HillMountainRatio", typeof(float)),
                    ("SnowRatio", typeof(float)),
                    ("LakeRatio", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainQualityGateResult>(
                [
                    ("Passed", typeof(bool)),
                    ("Report", typeof(TerrainQualityReport)),
                    ("Summary", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainExperienceThresholds>(
                [
                    ("MinEncounterRichRegionRatio", typeof(float)),
                    ("MinResourceRichRegionRatio", typeof(float)),
                    ("MinHazardRichRegionRatio", typeof(float)),
                    ("MinAverageEncounterPotential", typeof(float)),
                    ("MinAverageResourcePotential", typeof(float)),
                    ("MinRouteRhythmScore", typeof(float)),
                    ("MinPointOfInterestValue", typeof(float)),
                    ("MinRiskRewardBalance", typeof(float)),
                    ("MinScenicAnchorRatio", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainExperienceReport>(
                [
                    ("RegionCount", typeof(int)),
                    ("EncounterRichRegionRatio", typeof(float)),
                    ("ResourceRichRegionRatio", typeof(float)),
                    ("HazardRichRegionRatio", typeof(float)),
                    ("AverageExposure", typeof(float)),
                    ("AverageResourcePotential", typeof(float)),
                    ("AverageHazardPotential", typeof(float)),
                    ("AverageEncounterPotential", typeof(float)),
                    ("RouteRhythmScore", typeof(float)),
                    ("PointOfInterestValue", typeof(float)),
                    ("RiskRewardBalance", typeof(float)),
                    ("ScenicAnchorRatio", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainExperienceGateResult>(
                [
                    ("Passed", typeof(bool)),
                    ("Report", typeof(TerrainExperienceReport)),
                    ("Summary", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWaterState>(
                [
                    ("WorldPosition", typeof(Vector2)),
                    ("Kind", typeof(TerrainWaterKind)),
                    ("SurfaceHeight", typeof(float)),
                    ("Depth", typeof(float)),
                    ("Strength", typeof(float)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind)),
                    ("HasWater", typeof(bool)),
                    ("IsOceanic", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainGameplayTags>(
                [
                    ("WorldPosition", typeof(Vector2)),
                    ("Flags", typeof(TerrainGameplayTag)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind)),
                    ("WaterKind", typeof(TerrainWaterKind)),
                    ("Traversability", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("ResourcePotential", typeof(float)),
                    ("HazardPotential", typeof(float)),
                    ("EncounterPotential", typeof(float)),
                    ("IsTraversable", typeof(bool)),
                    ("IsScenic", typeof(bool)),
                    ("IsResourceRich", typeof(bool)),
                    ("IsHazardous", typeof(bool)),
                    ("IsEncounterRich", typeof(bool)),
                    ("HasWaterAccess", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainTraversalCost>(
                [
                    ("WorldPosition", typeof(Vector2)),
                    ("IsBlocked", typeof(bool)),
                    ("Cost", typeof(float)),
                    ("Traversability", typeof(float)),
                    ("Slope", typeof(float)),
                    ("HazardPotential", typeof(float)),
                    ("WaterKind", typeof(TerrainWaterKind)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind)),
                    ("IsPreferred", typeof(bool)),
                    ("IsDifficult", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldStreamingSnapshot>(
                [
                    ("Profile", typeof(TerrainGenerationProfile)),
                    ("HasFocus", typeof(bool)),
                    ("FocusPosition", typeof(Vector3)),
                    ("FocusCoord", typeof(TerrainTileCoord)),
                    ("StreamRadiusChunks", typeof(int)),
                    ("DesiredChunkCount", typeof(int)),
                    ("DesiredChunks", typeof(TerrainTileCoord[])),
                    ("LoadedChunkCount", typeof(int)),
                    ("LoadedChunks", typeof(TerrainTileCoord[])),
                    ("QueuedTileJobCount", typeof(int)),
                    ("QueuedTileJobs", typeof(TerrainTileCoord[])),
                    ("RetiredTileJobCount", typeof(int)),
                    ("TileCacheCount", typeof(int)),
                    ("TileCacheLimit", typeof(int)),
                    ("MaxQueuedTileJobs", typeof(int)),
                    ("MaxCompletedTilesPerFrame", typeof(int)),
                    ("HasWorldPlan", typeof(bool)),
                    ("IsWorldPlanGenerationPending", typeof(bool)),
                    ("StreamTerrainBeforeOpenWorldPlanReady", typeof(bool)),
                    ("TileCacheWithinLimit", typeof(bool)),
                    ("TileJobQueueWithinLimit", typeof(bool)),
                    ("CanStreamTerrain", typeof(bool)),
                    ("FocusTileLoaded", typeof(bool)),
                    ("DesiredChunksLoaded", typeof(bool)),
                    ("FocusAreaReady", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldPointOfInterestAnchorDescriptor>(
                [
                    ("Name", typeof(string)),
                    ("GroupName", typeof(string)),
                    ("GameplayTagGroup", typeof(string)),
                    ("Id", typeof(int)),
                    ("Kind", typeof(TerrainPointOfInterestKind)),
                    ("WorldPosition2D", typeof(Vector2)),
                    ("Score", typeof(float)),
                    ("Height", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("Traversability", typeof(float)),
                    ("SettlementTier", typeof(TerrainSettlementTier)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind)),
                    ("VisualKind", typeof(TerrainPointOfInterestVisualKind)),
                    ("GameplayTag", typeof(string)),
                    ("InteractionRadius", typeof(float)),
                    ("EncounterBudget", typeof(int))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldRouteAnchorDescriptor>(
                [
                    ("Name", typeof(string)),
                    ("GroupName", typeof(string)),
                    ("FromPointId", typeof(int)),
                    ("ToPointId", typeof(int)),
                    ("Kind", typeof(TerrainRouteKind)),
                    ("Cost", typeof(float)),
                    ("AverageScenicPotential", typeof(float)),
                    ("AverageTraversability", typeof(float)),
                    ("WorldMidpoint2D", typeof(Vector2)),
                    ("WaypointCount", typeof(int)),
                    ("Waypoints", typeof(Vector2[]))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainRouteCorridorSample>(
                [
                    ("HasInfluence", typeof(bool)),
                    ("Kind", typeof(TerrainRouteKind)),
                    ("Influence", typeof(float)),
                    ("CoreStrength", typeof(float)),
                    ("Distance", typeof(float)),
                    ("TargetHeight", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("Traversability", typeof(float)),
                    ("Direction", typeof(Vector2))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainRouteCorridorSegment>(
                [
                    ("From", typeof(Vector2)),
                    ("To", typeof(Vector2)),
                    ("Delta", typeof(Vector2)),
                    ("Direction", typeof(Vector2)),
                    ("LengthSquared", typeof(float)),
                    ("FromHeight", typeof(float)),
                    ("ToHeight", typeof(float)),
                    ("Kind", typeof(TerrainRouteKind)),
                    ("CoreWidth", typeof(float)),
                    ("ShoulderWidth", typeof(float)),
                    ("CoreInnerWidth", typeof(float)),
                    ("ShoulderWidthSquared", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("Traversability", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainRouteCorridorIndex>(
                [
                    ("CacheKey", typeof(int)),
                    ("HasSegments", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainPointOfInterestIndex>(
                [
                    ("CacheKey", typeof(int)),
                    ("HasPoints", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWaterSurfaceData>(
                [
                    ("Vertices", typeof(Vector3[])),
                    ("Normals", typeof(Vector3[])),
                    ("Uvs", typeof(Vector2[])),
                    ("Colors", typeof(Color[])),
                    ("Indices", typeof(int[])),
                    ("LakeCellCount", typeof(int)),
                    ("RiverCellCount", typeof(int)),
                    ("OasisCellCount", typeof(int)),
                    ("MinHeight", typeof(float)),
                    ("MaxHeight", typeof(float)),
                    ("HasSurface", typeof(bool)),
                    ("CellCount", typeof(int))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainTileData>(
                [
                    ("Coord", typeof(TerrainTileCoord)),
                    ("Lod", typeof(int)),
                    ("Resolution", typeof(int)),
                    ("ChunkSize", typeof(float)),
                    ("Origin", typeof(Vector2)),
                    ("Vertices", typeof(Vector3[])),
                    ("Normals", typeof(Vector3[])),
                    ("Uvs", typeof(Vector2[])),
                    ("Colors", typeof(Color[])),
                    ("Indices", typeof(int[])),
                    ("WaterSurface", typeof(TerrainWaterSurfaceData)),
                    ("CollisionFaces", typeof(Vector3[])),
                    ("ScatterInstances", typeof(TerrainScatterInstance[])),
                    ("Landmarks", typeof(TerrainLandmarkData[])),
                    ("MinHeight", typeof(float)),
                    ("MaxHeight", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainScatterInstance>(
                [
                    ("Kind", typeof(TerrainScatterKind)),
                    ("LocalPosition", typeof(Vector3)),
                    ("RotationY", typeof(float)),
                    ("UniformScale", typeof(float)),
                    ("Color", typeof(Color)),
                    ("LandmarkKind", typeof(TerrainLandmarkKind))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainLandmarkData>(
                [
                    ("Kind", typeof(TerrainLandmarkKind)),
                    ("LocalPosition", typeof(Vector3)),
                    ("Score", typeof(float)),
                    ("DebugName", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainPointOfInterestArchetype>(
                [
                    ("Kind", typeof(TerrainPointOfInterestKind)),
                    ("VisualKind", typeof(TerrainPointOfInterestVisualKind)),
                    ("GameplayTag", typeof(string)),
                    ("DisplayName", typeof(string)),
                    ("VisualScale", typeof(float)),
                    ("VerticalOffset", typeof(float)),
                    ("InteractionRadius", typeof(float)),
                    ("EncounterBudget", typeof(int)),
                    ("Color", typeof(Color))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainPointOfInterestArchetypeValidationReport>(
                [
                    ("Passed", typeof(bool)),
                    ("DefinedArchetypeCount", typeof(int)),
                    ("ExpectedArchetypeCount", typeof(int)),
                    ("MissingArchetypeCount", typeof(int)),
                    ("PlanPointCount", typeof(int)),
                    ("PlanPointsWithArchetypes", typeof(int)),
                    ("Summary", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldPlanArtifactResult>(
                [
                    ("Plan", typeof(TerrainWorldPlan)),
                    ("PlanningGate", typeof(TerrainWorldPlanningGateResult)),
                    ("QualityGate", typeof(TerrainQualityGateResult)),
                    ("ExperienceGate", typeof(TerrainExperienceGateResult)),
                    ("MapPath", typeof(string)),
                    ("ReportPath", typeof(string)),
                    ("MapSaveError", typeof(Error)),
                    ("ReportSaveError", typeof(Error)),
                    ("Passed", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainGenerationProfile),
                [
                    new("StableHash", false, typeof(string), []),
                    new("ResolutionForLod", false, typeof(int), [typeof(int)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainSettings),
                [
                    new("Snapshot", false, typeof(TerrainGenerationProfile), [])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainScenicLandmarkRuleSet),
                [
                    new("StableHash", false, typeof(string), [])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticFields(
                typeof(TerrainApiVersion),
                [
                    ("Major", typeof(int)),
                    ("Minor", typeof(int)),
                    ("Patch", typeof(int)),
                    ("Contract", typeof(string)),
                    ("Version", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticFields(
                typeof(TerrainDeterminismContract),
                [
                    ("Contract", typeof(string)),
                    ("ExactFloatEpsilon", typeof(float)),
                    ("ExactPositionEpsilon", typeof(float)),
                    ("HeightEpsilon", typeof(float)),
                    ("FieldEpsilon", typeof(float)),
                    ("PositionEpsilon", typeof(float)),
                    ("NativeHeightMaxEpsilon", typeof(float)),
                    ("NativeHeightAverageEpsilon", typeof(float)),
                    ("NativeFieldMaxEpsilon", typeof(float)),
                    ("NativeFieldAverageEpsilon", typeof(float)),
                    ("NativeTileHeightEpsilon", typeof(float)),
                    ("NativeTileColorEpsilon", typeof(float)),
                    ("TileParityHeightEpsilon", typeof(float)),
                    ("TileParityColorEpsilon", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticFields(
                typeof(TerrainPerformanceContract),
                [
                    ("Contract", typeof(string)),
                    ("TileBenchmarkHardwareBaseline", typeof(string)),
                    ("MaxManagedMillisecondsPerTile", typeof(double)),
                    ("MaxNativeMillisecondsPerTile", typeof(double)),
                    ("MaxManagedP50Milliseconds", typeof(double)),
                    ("MaxManagedP95Milliseconds", typeof(double)),
                    ("MaxManagedP99Milliseconds", typeof(double)),
                    ("MaxNativeP50Milliseconds", typeof(double)),
                    ("MaxNativeP95Milliseconds", typeof(double)),
                    ("MaxNativeP99Milliseconds", typeof(double)),
                    ("MaxAllocatedKilobytesPerTile", typeof(double)),
                    ("MinNativeSpeedup", typeof(double)),
                    ("MinParityTileCount", typeof(int)),
                    ("MinBenchmarkBiomeKinds", typeof(int)),
                    ("MinBenchmarkLandscapeKinds", typeof(int)),
                    ("MinBenchmarkPointOfInterestTiles", typeof(int)),
                    ("MinBenchmarkRouteTiles", typeof(int)),
                    ("MinBenchmarkGameplayRichTiles", typeof(int))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticFields(
                typeof(TerrainWorldPlanSerializer),
                [
                    ("Contract", typeof(string)),
                    ("GeneratorVersion", typeof(string))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticProperties(
                typeof(NativeTerrainBridge),
                [
                    ("IsAvailable", typeof(bool)),
                    ("SupportsFieldGridSampler", typeof(bool)),
                    ("SupportsDerivedFieldGridSampler", typeof(bool)),
                    ("SupportsHeightGridSampler", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticProperties(
                typeof(TerrainPointOfInterestArchetypeCatalog),
                [
                    ("All", typeof(ReadOnlySpan<TerrainPointOfInterestArchetype>))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticProperties(
                typeof(TerrainWorldPlanningThresholds),
                [
                    ("OpenWorldDefault", typeof(TerrainWorldPlanningThresholds))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticProperties(
                typeof(TerrainQualityThresholds),
                [
                    ("OpenWorldDefault", typeof(TerrainQualityThresholds))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicStaticProperties(
                typeof(TerrainExperienceThresholds),
                [
                    ("OpenWorldDefault", typeof(TerrainExperienceThresholds))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(ITerrainQueryService),
                [
                    new("SampleField", false, typeof(TerrainWorldField), [typeof(Vector2)]),
                    new("SampleSurface", false, typeof(TerrainSample), [typeof(Vector2), typeof(float)]),
                    new("SurfacePositionAt", false, typeof(Vector3), [typeof(Vector2), typeof(float)]),
                    new("SampleWaterState", false, typeof(TerrainWaterState), [typeof(Vector2)]),
                    new("SampleGameplayTags", false, typeof(TerrainGameplayTags), [typeof(Vector2)]),
                    new("SampleTraversalCost", false, typeof(TerrainTraversalCost), [typeof(Vector2), typeof(float)]),
                    new("IsTraversable", false, typeof(bool), [typeof(Vector2), typeof(float)]),
                    new("IsAboveWater", false, typeof(bool), [typeof(Vector2), typeof(float)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(ITerrainPlanProvider),
                [
                    new("GetWorldPlanSnapshot", false, typeof(TerrainWorldPlanSnapshot), []),
                    new("TryGetWorldPlanSnapshot", false, typeof(bool), [typeof(TerrainWorldPlanSnapshot).MakeByRefType()]),
                    new("GetPointsOfInterest", false, typeof(TerrainWorldPointOfInterest[]), []),
                    new("GetRoutes", false, typeof(TerrainWorldRoute[]), []),
                    new("TryFindNearestPointOfInterest", false, typeof(bool), [typeof(Vector2), typeof(float), typeof(TerrainPointOfInterestKind?), typeof(TerrainWorldPointOfInterest).MakeByRefType()]),
                    new("QueryPointsOfInterest", false, typeof(TerrainWorldPointOfInterest[]), [typeof(Rect2), typeof(TerrainPointOfInterestKind?)]),
                    new("QueryRoutesNear", false, typeof(TerrainWorldRoute[]), [typeof(Vector2), typeof(float)]),
                    new("SampleRouteCorridor", false, typeof(TerrainRouteCorridorSample), [typeof(Vector2)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(ITerrainStreamingDiagnostics),
                [
                    new("GetStreamingSnapshot", false, typeof(TerrainWorldStreamingSnapshot), [])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainWorld),
                [
                    new("SetFocus", false, typeof(void), [typeof(Node3D)]),
                    new("SetWorldPlan", false, typeof(void), [typeof(TerrainWorldPlan)]),
                    new("Regenerate", false, typeof(void), []),
                    new("GenerateOpenWorldPlan", false, typeof(TerrainWorldPlan), [typeof(bool)]),
                    new("SampleField", false, typeof(TerrainWorldField), [typeof(Vector2)]),
                    new("SampleSurface", false, typeof(TerrainSample), [typeof(Vector2), typeof(float)]),
                    new("SurfacePositionAt", false, typeof(Vector3), [typeof(Vector2), typeof(float)]),
                    new("TryGetWorldPlan", false, typeof(bool), [typeof(TerrainWorldPlan).MakeByRefType()]),
                    new("GetWorldPlanSnapshot", false, typeof(TerrainWorldPlanSnapshot), []),
                    new("TryGetWorldPlanSnapshot", false, typeof(bool), [typeof(TerrainWorldPlanSnapshot).MakeByRefType()]),
                    new("GetPointsOfInterest", false, typeof(TerrainWorldPointOfInterest[]), []),
                    new("GetRoutes", false, typeof(TerrainWorldRoute[]), []),
                    new("GetStreamingSnapshot", false, typeof(TerrainWorldStreamingSnapshot), []),
                    new("TryFindNearestPointOfInterest", false, typeof(bool), [typeof(Vector2), typeof(float), typeof(TerrainPointOfInterestKind?), typeof(TerrainWorldPointOfInterest).MakeByRefType()]),
                    new("QueryPointsOfInterest", false, typeof(TerrainWorldPointOfInterest[]), [typeof(Rect2), typeof(TerrainPointOfInterestKind?)]),
                    new("QueryRoutesNear", false, typeof(TerrainWorldRoute[]), [typeof(Vector2), typeof(float)]),
                    new("SampleRouteCorridor", false, typeof(TerrainRouteCorridorSample), [typeof(Vector2)]),
                    new("SampleWaterState", false, typeof(TerrainWaterState), [typeof(Vector2)]),
                    new("SampleGameplayTags", false, typeof(TerrainGameplayTags), [typeof(Vector2)]),
                    new("SampleTraversalCost", false, typeof(TerrainTraversalCost), [typeof(Vector2), typeof(float)]),
                    new("IsTraversable", false, typeof(bool), [typeof(Vector2), typeof(float)]),
                    new("IsAboveWater", false, typeof(bool), [typeof(Vector2), typeof(float)]),
                    new("CreateRuntimeOpenWorldPlan", true, typeof(TerrainWorldPlan), [typeof(TerrainGenerationProfile), typeof(float), typeof(CancellationToken)]),
                    new("CreateRuntimeOpenWorldPlan", true, typeof(TerrainWorldPlan), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(CancellationToken)]),
                    new("CreateRuntimeOpenWorldPlanAsync", true, typeof(Task<TerrainWorldPlan>), [typeof(TerrainGenerationProfile), typeof(float), typeof(CancellationToken)]),
                    new("CreateRuntimeOpenWorldPlanAsync", true, typeof(Task<TerrainWorldPlan>), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(CancellationToken)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainWorldPlanSerializer),
                [
                    new("ToJson", true, typeof(string), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile)]),
                    new("TryFromJson", true, typeof(bool), [typeof(string), typeof(TerrainWorldPlan).MakeByRefType(), typeof(string).MakeByRefType()]),
                    new("TryFromJson", true, typeof(bool), [typeof(string), typeof(TerrainGenerationProfile), typeof(TerrainWorldPlan).MakeByRefType(), typeof(string).MakeByRefType()]),
                    new("SaveJson", true, typeof(Error), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile), typeof(string)]),
                    new("TryLoadJson", true, typeof(bool), [typeof(string), typeof(TerrainWorldPlan).MakeByRefType(), typeof(string).MakeByRefType()]),
                    new("TryLoadJson", true, typeof(bool), [typeof(string), typeof(TerrainGenerationProfile), typeof(TerrainWorldPlan).MakeByRefType(), typeof(string).MakeByRefType()])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainTileBuilder),
                [
                    new("Build", true, typeof(TerrainTileData), [typeof(TerrainTileCoord), typeof(int), typeof(TerrainGenerationProfile), typeof(bool), typeof(CancellationToken)]),
                    new("Build", true, typeof(TerrainTileData), [typeof(TerrainTileCoord), typeof(int), typeof(TerrainGenerationProfile), typeof(bool), typeof(TerrainRouteCorridorIndex), typeof(CancellationToken)]),
                    new("Build", true, typeof(TerrainTileData), [typeof(TerrainTileCoord), typeof(int), typeof(TerrainGenerationProfile), typeof(bool), typeof(TerrainRouteCorridorIndex), typeof(TerrainPointOfInterestIndex), typeof(CancellationToken)]),
                    new("ShouldUseNativeSamplerForTileGeneration", true, typeof(bool), [typeof(TerrainGenerationProfile), typeof(int)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainMeshBuilder),
                [
                    new("CreateMesh", true, typeof(ArrayMesh), [typeof(TerrainTileData)]),
                    new("CreateWaterMesh", true, typeof(ArrayMesh), [typeof(TerrainTileData)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainRouteCorridorIndex),
                [
                    new("FromPlan", true, typeof(TerrainRouteCorridorIndex), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile)]),
                    new("GetSegments", false, typeof(TerrainRouteCorridorSegment[]), [typeof(TerrainTileCoord)]),
                    new("Sample", false, typeof(TerrainRouteCorridorSample), [typeof(Vector2), typeof(TerrainTileCoord)]),
                    new("Sample", false, typeof(TerrainRouteCorridorSample), [typeof(Vector2), typeof(TerrainRouteCorridorSegment[])])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainPointOfInterestIndex),
                [
                    new("FromPlan", true, typeof(TerrainPointOfInterestIndex), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile)]),
                    new("GetPoints", false, typeof(TerrainWorldPointOfInterest[]), [typeof(TerrainTileCoord)]),
                    new("FootprintRadiusFor", true, typeof(float), [typeof(TerrainWorldPointOfInterest), typeof(TerrainGenerationProfile)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(NativeTerrainBridge),
                [
                    new("TrySampleHeightGrid", true, typeof(bool), [typeof(TerrainTileCoord), typeof(int), typeof(TerrainGenerationProfile), typeof(float[]).MakeByRefType()]),
                    new("TrySampleFieldGrid", true, typeof(bool), [typeof(TerrainTileCoord), typeof(int), typeof(TerrainGenerationProfile), typeof(float[]).MakeByRefType()]),
                    new("TrySampleFieldGrid", true, typeof(bool), [typeof(TerrainTileCoord), typeof(int), typeof(TerrainGenerationProfile), typeof(float[]), typeof(int)]),
                    new("TrySampleFieldGrid", true, typeof(bool), [typeof(TerrainTileCoord), typeof(int), typeof(TerrainGenerationProfile), typeof(float[]), typeof(int), typeof(bool).MakeByRefType()]),
                    new("EnsureInitialized", true, typeof(void), [])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainPointOfInterestArchetypeCatalog),
                [
                    new("Get", true, typeof(TerrainPointOfInterestArchetype), [typeof(TerrainPointOfInterestKind)]),
                    new("TryGet", true, typeof(bool), [typeof(TerrainPointOfInterestKind), typeof(TerrainPointOfInterestArchetype).MakeByRefType()]),
                    new("VisualKindFor", true, typeof(TerrainPointOfInterestVisualKind), [typeof(TerrainWorldPointOfInterest)]),
                    new("ValidatePlanReadiness", true, typeof(TerrainPointOfInterestArchetypeValidationReport), [typeof(TerrainWorldPlan)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainMapExporter),
                [
                    new("SampleWorld", true, typeof(TerrainMapSample), [typeof(Vector2), typeof(TerrainGenerationProfile)]),
                    new("CreateBiomeMap", true, typeof(Image), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(int)]),
                    new("CreateMap", true, typeof(Image), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(int), typeof(TerrainMapLayer)]),
                    new("CreateRaster", true, typeof(TerrainMapRaster), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(int), typeof(TerrainMapLayer)]),
                    new("CreateTraversalCostGrid", true, typeof(TerrainTraversalCostGrid), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(int), typeof(float)]),
                    new("CreateImage", true, typeof(Image), [typeof(TerrainMapRaster)]),
                    new("SaveBiomeMap", true, typeof(Error), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(int), typeof(string)]),
                    new("SaveMap", true, typeof(Error), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(int), typeof(TerrainMapLayer), typeof(string)]),
                    new("SaveRasterPng", true, typeof(Error), [typeof(TerrainMapRaster), typeof(string)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainWorldFieldSampler),
                [
                    new("Sample", true, typeof(TerrainWorldField), [typeof(Vector2), typeof(TerrainGenerationProfile)]),
                    new("Sample", true, typeof(TerrainWorldField), [typeof(Vector2), typeof(TerrainGenerationProfile), typeof(float)]),
                    new("SampleKnownHeight", true, typeof(TerrainWorldField), [typeof(Vector2), typeof(TerrainGenerationProfile), typeof(float)]),
                    new("SampleNativeFieldGrid", true, typeof(TerrainWorldField), [typeof(Vector2), typeof(TerrainGenerationProfile), typeof(float[]), typeof(int)]),
                    new("SampleNativeFieldGrid", true, typeof(TerrainWorldField), [typeof(Vector2), typeof(TerrainGenerationProfile), typeof(float[]), typeof(int), typeof(bool)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainSampler),
                [
                    new("Sample", true, typeof(TerrainSample), [typeof(Vector2), typeof(TerrainGenerationProfile)]),
                    new("SampleWithSlope", true, typeof(TerrainSample), [typeof(Vector2), typeof(TerrainGenerationProfile), typeof(float)]),
                    new("NormalAt", true, typeof(Vector3), [typeof(Vector2), typeof(TerrainGenerationProfile), typeof(float)]),
                    new("ColorForSurface", true, typeof(Color), [typeof(Vector2), typeof(TerrainGenerationProfile), typeof(float), typeof(float)]),
                    new("ColorForSurface", true, typeof(Color), [typeof(TerrainWorldField), typeof(TerrainGenerationProfile), typeof(float)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainWorldPlanner),
                [
                    new("CreateOpenWorldPlan", true, typeof(TerrainWorldPlan), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(CancellationToken)]),
                    new("AnalyzePlanning", true, typeof(TerrainWorldPlanningReport), [typeof(TerrainWorldPlan)]),
                    new("ValidateOpenWorldPlanning", true, typeof(TerrainWorldPlanningGateResult), [typeof(TerrainWorldPlan)]),
                    new("ValidateOpenWorldPlanning", true, typeof(TerrainWorldPlanningGateResult), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(CancellationToken)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainQualityAnalyzer),
                [
                    new("ValidateOpenWorldDefault", true, typeof(TerrainQualityGateResult), [typeof(TerrainQualityReport)]),
                    new("ValidateOpenWorldDefault", true, typeof(TerrainQualityGateResult), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(int), typeof(CancellationToken)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainExperienceAnalyzer),
                [
                    new("ValidateOpenWorldDefault", true, typeof(TerrainExperienceGateResult), [typeof(TerrainWorldPlan), typeof(CancellationToken)]),
                    new("ValidateOpenWorldDefault", true, typeof(TerrainExperienceGateResult), [typeof(TerrainExperienceReport)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainWorldPlanExporter),
                [
                    new("SaveOpenWorldArtifacts", true, typeof(TerrainWorldPlanArtifactResult), [typeof(TerrainGenerationProfile), typeof(Vector2), typeof(float), typeof(int), typeof(string), typeof(TerrainMapLayer)]),
                    new("SaveOpenWorldArtifacts", true, typeof(TerrainWorldPlanArtifactResult), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile), typeof(int), typeof(string), typeof(TerrainMapLayer)]),
                    new("SavePlanMap", true, typeof(Error), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile), typeof(int), typeof(TerrainMapLayer), typeof(string)]),
                    new("CreatePlanMap", true, typeof(Image), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile), typeof(int), typeof(TerrainMapLayer)]),
                    new("CreatePlanRaster", true, typeof(TerrainMapRaster), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile), typeof(int), typeof(TerrainMapLayer)]),
                    new("CreateTextReport", true, typeof(string), [typeof(TerrainWorldPlan), typeof(TerrainWorldPlanningGateResult), typeof(TerrainQualityGateResult), typeof(TerrainExperienceGateResult), typeof(string)]),
                    new("CreateTextReport", true, typeof(string), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile), typeof(TerrainWorldPlanningGateResult), typeof(TerrainQualityGateResult), typeof(TerrainExperienceGateResult), typeof(string)]),
                    new("SaveTextReport", true, typeof(Error), [typeof(TerrainWorldPlan), typeof(TerrainWorldPlanningGateResult), typeof(TerrainQualityGateResult), typeof(TerrainExperienceGateResult), typeof(string), typeof(string)]),
                    new("SaveTextReport", true, typeof(Error), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile), typeof(TerrainWorldPlanningGateResult), typeof(TerrainQualityGateResult), typeof(TerrainExperienceGateResult), typeof(string), typeof(string)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainWorldPlanOverlay),
                [
                    new("ApplyPlan", false, typeof(void), [typeof(TerrainWorldPlan), typeof(TerrainGenerationProfile)]),
                    new("ClearPlan", false, typeof(void), [])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason);

        return new TerrainPublicApiShapeSmokeReport(
            passed,
            checkedTypeCount,
            checkedMemberCount,
            passed
                ? "public terrain exported type set, data carrier shapes, and facade method signatures match the stable runtime contract"
                : failureReason ?? "terrain public API shape contract failed");
    }
    catch (Exception ex)
    {
        return new TerrainPublicApiShapeSmokeReport(
            false,
            0,
            0,
            $"terrain public API shape smoke threw {ex.GetType().Name}: {ex.Message}");
    }
}

static bool CheckPublicShape<T>(
    (string Name, Type Type)[] expected,
    ref int checkedTypeCount,
    ref int checkedMemberCount,
    out string? failureReason)
{
    Type type = typeof(T);
    PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
    if (properties.Length != expected.Length)
    {
        failureReason = $"{type.Name} public property count changed ({properties.Length}/{expected.Length})";
        return false;
    }

    for (int i = 0; i < expected.Length; i++)
    {
        PropertyInfo property = properties[i];
        if (!string.Equals(property.Name, expected[i].Name, StringComparison.Ordinal) ||
            property.PropertyType != expected[i].Type)
        {
            failureReason =
                $"{type.Name} property drift at index {i}: actual {property.Name}:{property.PropertyType.Name}, " +
                $"expected {expected[i].Name}:{expected[i].Type.Name}";
            return false;
        }
    }

    checkedTypeCount++;
    checkedMemberCount += expected.Length;
    failureReason = null;
    return true;
}

static bool CheckExportedTerrainTypes(out string? failureReason)
{
    string[] expected =
    [
        "Dao.Terrain.Generation.NativeTerrainBridge",
        "Dao.Terrain.Generation.ProceduralNoise",
        "Dao.Terrain.Generation.TerrainBiomeKind",
        "Dao.Terrain.Generation.TerrainExperienceAnalyzer",
        "Dao.Terrain.Generation.TerrainExperienceGateResult",
        "Dao.Terrain.Generation.TerrainExperienceReport",
        "Dao.Terrain.Generation.TerrainExperienceThresholds",
        "Dao.Terrain.Generation.TerrainGameplayTag",
        "Dao.Terrain.Generation.TerrainGameplayTags",
        "Dao.Terrain.Generation.TerrainLandmarkData",
        "Dao.Terrain.Generation.TerrainLandmarkKind",
        "Dao.Terrain.Generation.TerrainLandscapeKind",
        "Dao.Terrain.Generation.TerrainMapExporter",
        "Dao.Terrain.Generation.TerrainMapLayer",
        "Dao.Terrain.Generation.TerrainMapRaster",
        "Dao.Terrain.Generation.TerrainMapSample",
        "Dao.Terrain.Generation.TerrainPointOfInterestIndex",
        "Dao.Terrain.Generation.TerrainPointOfInterestKind",
        "Dao.Terrain.Generation.TerrainQualityAnalyzer",
        "Dao.Terrain.Generation.TerrainQualityGateResult",
        "Dao.Terrain.Generation.TerrainQualityReport",
        "Dao.Terrain.Generation.TerrainQualityThresholds",
        "Dao.Terrain.Generation.TerrainRouteCorridorIndex",
        "Dao.Terrain.Generation.TerrainRouteCorridorSample",
        "Dao.Terrain.Generation.TerrainRouteCorridorSegment",
        "Dao.Terrain.Generation.TerrainRouteKind",
        "Dao.Terrain.Generation.TerrainSample",
        "Dao.Terrain.Generation.TerrainSampler",
        "Dao.Terrain.Generation.TerrainScatterInstance",
        "Dao.Terrain.Generation.TerrainScatterKind",
        "Dao.Terrain.Generation.TerrainSemanticClassifier",
        "Dao.Terrain.Generation.TerrainSettlementTier",
        "Dao.Terrain.Generation.TerrainTileBuilder",
        "Dao.Terrain.Generation.TerrainTileCoord",
        "Dao.Terrain.Generation.TerrainTileData",
        "Dao.Terrain.Generation.TerrainTraversalCost",
        "Dao.Terrain.Generation.TerrainTraversalCostGrid",
        "Dao.Terrain.Generation.TerrainWaterKind",
        "Dao.Terrain.Generation.TerrainWaterState",
        "Dao.Terrain.Generation.TerrainWaterSurfaceData",
        "Dao.Terrain.Generation.TerrainWorldField",
        "Dao.Terrain.Generation.TerrainWorldFieldSampler",
        "Dao.Terrain.Generation.TerrainWorldPlan",
        "Dao.Terrain.Generation.TerrainWorldPlanArtifactResult",
        "Dao.Terrain.Generation.TerrainWorldPlanExporter",
        "Dao.Terrain.Generation.TerrainWorldPlanner",
        "Dao.Terrain.Generation.TerrainWorldPlanningGateResult",
        "Dao.Terrain.Generation.TerrainWorldPlanningReport",
        "Dao.Terrain.Generation.TerrainWorldPlanningThresholds",
        "Dao.Terrain.Generation.TerrainWorldPlanSerializer",
        "Dao.Terrain.Generation.TerrainWorldPlanSnapshot",
        "Dao.Terrain.Generation.TerrainWorldPointOfInterest",
        "Dao.Terrain.Generation.TerrainWorldRegion",
        "Dao.Terrain.Generation.TerrainWorldRegionKind",
        "Dao.Terrain.Generation.TerrainWorldRoute",
        "Dao.Terrain.ITerrainPlanProvider",
        "Dao.Terrain.ITerrainQueryService",
        "Dao.Terrain.ITerrainStreamingDiagnostics",
        "Dao.Terrain.TerrainGameplaySettingsResource",
        "Dao.Terrain.TerrainNaturalLandmarkRuleResource",
        "Dao.Terrain.Rendering.TerrainMaterialFactory",
        "Dao.Terrain.Rendering.TerrainMeshBuilder",
        "Dao.Terrain.TerrainRenderingSettingsResource",
        "Dao.Terrain.TerrainScenicLandmarkRuleSet",
        "Dao.Terrain.TerrainShapeSettingsResource",
        "Dao.Terrain.Runtime.TerrainPointOfInterestArchetype",
        "Dao.Terrain.Runtime.TerrainPointOfInterestArchetypeCatalog",
        "Dao.Terrain.Runtime.TerrainPointOfInterestArchetypeValidationReport",
        "Dao.Terrain.Runtime.TerrainPointOfInterestVisualKind",
        "Dao.Terrain.Runtime.TerrainWorldAnchorBuilder",
        "Dao.Terrain.Runtime.TerrainWorldAnchorContract",
        "Dao.Terrain.Runtime.TerrainWorldPlanOverlay",
        "Dao.Terrain.Runtime.TerrainWorldPointOfInterestAnchor",
        "Dao.Terrain.Runtime.TerrainWorldPointOfInterestAnchorDescriptor",
        "Dao.Terrain.Runtime.TerrainWorldRouteAnchor",
        "Dao.Terrain.Runtime.TerrainWorldRouteAnchorDescriptor",
        "Dao.Terrain.Streaming.TerrainChunk",
        "Dao.Terrain.Streaming.TerrainWorld",
        "Dao.Terrain.Streaming.TerrainWorldStreamingSnapshot",
        "Dao.Terrain.TerrainApiVersion",
        "Dao.Terrain.TerrainDeterminismContract",
        "Dao.Terrain.TerrainGenerationProfile",
        "Dao.Terrain.TerrainPerformanceContract",
        "Dao.Terrain.TerrainProfileHash",
        "Dao.Terrain.TerrainSettings",
        "Dao.Terrain.TerrainStreamingSettingsResource",
        "Dao.Terrain.TerrainWorldSettingsResource"
    ];

    string[] actual = typeof(TerrainWorld).Assembly.GetExportedTypes()
        .Where(static type =>
            type.DeclaringType is null &&
            type.Namespace is not null &&
            type.Namespace.StartsWith("Dao.Terrain", StringComparison.Ordinal))
        .Select(static type => type.FullName ?? type.Name)
        .OrderBy(static name => name, StringComparer.Ordinal)
        .ToArray();
    string[] expectedSorted = expected
        .OrderBy(static name => name, StringComparer.Ordinal)
        .ToArray();

    if (actual.Length != expectedSorted.Length)
    {
        string[] missing = expectedSorted.Except(actual, StringComparer.Ordinal).ToArray();
        string[] unexpected = actual.Except(expectedSorted, StringComparer.Ordinal).ToArray();
        failureReason =
            $"terrain exported public type set changed (actual {actual.Length}, expected {expectedSorted.Length}); " +
            $"missing [{string.Join(", ", missing)}], unexpected [{string.Join(", ", unexpected)}]";
        return false;
    }

    for (int i = 0; i < expectedSorted.Length; i++)
    {
        if (!string.Equals(actual[i], expectedSorted[i], StringComparison.Ordinal))
        {
            string[] missing = expectedSorted.Except(actual, StringComparer.Ordinal).ToArray();
            string[] unexpected = actual.Except(expectedSorted, StringComparer.Ordinal).ToArray();
            failureReason =
                $"terrain exported public type drifted at index {i}: actual {actual[i]}, expected {expectedSorted[i]}; " +
                $"missing [{string.Join(", ", missing)}], unexpected [{string.Join(", ", unexpected)}]";
            return false;
        }
    }

    failureReason = null;
    return true;
}

static bool CheckPublicMethods(
    Type type,
    PublicMethodContract[] expected,
    ref int checkedTypeCount,
    ref int checkedMemberCount,
    out string? failureReason)
{
    foreach (PublicMethodContract contract in expected)
    {
        if (!TryFindPublicMethod(type, contract, out MethodInfo? method))
        {
            failureReason = $"{type.Name}.{contract.Name} method signature drifted or disappeared";
            return false;
        }

        if (method!.ReturnType != contract.ReturnType)
        {
            failureReason =
                $"{type.Name}.{contract.Name} return type drifted: actual {method.ReturnType.Name}, expected {contract.ReturnType.Name}";
            return false;
        }

        checkedMemberCount++;
    }

    checkedTypeCount++;
    failureReason = null;
    return true;
}

static bool TryFindPublicMethod(Type type, PublicMethodContract contract, out MethodInfo? method)
{
    BindingFlags flags = BindingFlags.Public | (contract.IsStatic ? BindingFlags.Static : BindingFlags.Instance);
    foreach (MethodInfo candidate in type.GetMethods(flags))
    {
        if (!string.Equals(candidate.Name, contract.Name, StringComparison.Ordinal))
        {
            continue;
        }

        ParameterInfo[] parameters = candidate.GetParameters();
        if (parameters.Length != contract.ParameterTypes.Length)
        {
            continue;
        }

        bool parametersMatch = true;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType != contract.ParameterTypes[i])
            {
                parametersMatch = false;
                break;
            }
        }

        if (parametersMatch)
        {
            method = candidate;
            return true;
        }
    }

    method = null;
    return false;
}

static bool CheckPublicStaticFields(
    Type type,
    (string Name, Type Type)[] expected,
    ref int checkedTypeCount,
    ref int checkedMemberCount,
    out string? failureReason)
{
    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
    if (fields.Length != expected.Length)
    {
        failureReason = $"{type.Name} public static field count changed ({fields.Length}/{expected.Length})";
        return false;
    }

    for (int i = 0; i < expected.Length; i++)
    {
        FieldInfo field = fields[i];
        if (!string.Equals(field.Name, expected[i].Name, StringComparison.Ordinal) ||
            field.FieldType != expected[i].Type)
        {
            failureReason =
                $"{type.Name} static field drift at index {i}: actual {field.Name}:{field.FieldType.Name}, " +
                $"expected {expected[i].Name}:{expected[i].Type.Name}";
            return false;
        }
    }

    checkedTypeCount++;
    checkedMemberCount += expected.Length;
    failureReason = null;
    return true;
}

static bool CheckPublicStaticProperties(
    Type type,
    (string Name, Type Type)[] expected,
    ref int checkedTypeCount,
    ref int checkedMemberCount,
    out string? failureReason)
{
    PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
    if (properties.Length != expected.Length)
    {
        failureReason = $"{type.Name} public static property count changed ({properties.Length}/{expected.Length})";
        return false;
    }

    for (int i = 0; i < expected.Length; i++)
    {
        PropertyInfo property = properties[i];
        if (!string.Equals(property.Name, expected[i].Name, StringComparison.Ordinal) ||
            property.PropertyType != expected[i].Type)
        {
            failureReason =
                $"{type.Name} static property drift at index {i}: actual {property.Name}:{property.PropertyType.Name}, " +
                $"expected {expected[i].Name}:{expected[i].Type.Name}";
            return false;
        }
    }

    checkedTypeCount++;
    checkedMemberCount += expected.Length;
    failureReason = null;
    return true;
}

static void PrintPublicApiShapeSmoke(TerrainPublicApiShapeSmokeReport report)
{
    Console.WriteLine(
        $"Terrain public API shape smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"types {report.CheckedTypeCount}, members {report.CheckedMemberCount} ({report.Reason})");
}

static TerrainProfileHashSmokeReport ValidateTerrainProfileHashContract(TerrainGenerationProfile profile)
{
    const string ExpectedDemoProfileHash = "1935cb338e79a294c89306bf6cbb6ad2046420521f42e190d1dc2318b2192dc2";
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

static bool HashContainsOnlyHex(string hash)
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

static string ProfileHashFailureReason(
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

static void PrintProfileHashSmoke(TerrainProfileHashSmokeReport report)
{
    Console.WriteLine(
        $"Terrain profile hash smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"hash {report.Hash}, format {(report.FormatPassed ? "pass" : "fail")}, " +
        $"expected {(report.ExpectedHashPassed ? "pass" : "fail")}, " +
        $"fields {report.SensitiveFieldCount}/{report.ExpectedFieldCount} ({report.Reason})");
}

static TerrainRuntimeApiSmokeReport ValidateTerrainWorldRuntimeApiFacade(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    try
    {
        TerrainWorld noPlanWorld = CreateTerrainWorldFacadeProbe(profile, worldPlan: null);
        SetPrivateField(
            noPlanWorld,
            "_desiredCoords",
            new HashSet<TerrainTileCoord> { new(2, -1), new(-1, 0) });
        SetPrivateField(
            noPlanWorld,
            "_chunks",
            new Dictionary<TerrainTileCoord, TerrainChunk>
            {
                [new(5, 2)] = null!,
                [new(-3, 4)] = null!
            });
        SetPrivateField(
            noPlanWorld,
            "_jobs",
            CreatePendingTileJobKeyDictionary([new TerrainTileCoord(8, -2), new TerrainTileCoord(-4, -3)]));
        Vector2 query = plan.PointsOfInterest.Length > 0
            ? plan.PointsOfInterest[0].WorldPosition
            : new Vector2(profile.ChunkSize * 0.75f, profile.ChunkSize * -0.5f);

        bool noPlanTryGetPassed = !noPlanWorld.TryGetWorldPlan(out TerrainWorldPlan? noPlan) && noPlan is null;
        bool noPlanSnapshotPassed =
            !noPlanWorld.TryGetWorldPlanSnapshot(out TerrainWorldPlanSnapshot? noPlanSnapshot) &&
            noPlanSnapshot is null &&
            noPlanWorld.GetWorldPlanSnapshot().PointsOfInterest.Length == 0 &&
            noPlanWorld.GetWorldPlanSnapshot().Routes.Length == 0 &&
            noPlanWorld.GetWorldPlanSnapshot().Regions.Length == 0;
        bool emptyPlanCollectionsPassed =
            noPlanWorld.GetPointsOfInterest().Length == 0 &&
            noPlanWorld.GetRoutes().Length == 0 &&
            !noPlanWorld.TryFindNearestPointOfInterest(query, profile.ChunkSize, kind: null, out _) &&
            noPlanWorld.QueryPointsOfInterest(new Rect2(query - new Vector2(1.0f, 1.0f), new Vector2(2.0f, 2.0f))).Length == 0 &&
            noPlanWorld.QueryRoutesNear(query, profile.ChunkSize).Length == 0 &&
            !noPlanWorld.SampleRouteCorridor(query).HasInfluence;

        TerrainWorldField expectedField = TerrainWorldFieldSampler.Sample(query, profile);
        TerrainWorldField facadeField = noPlanWorld.SampleField(query);
        bool sampleFieldMatchesSampler = TerrainFieldsMatch(expectedField, facadeField);

        TerrainSample expectedSurface = TerrainSampler.SampleWithSlope(query, profile, spacing: 4.0f);
        TerrainSample facadeSurface = noPlanWorld.SampleSurface(query, spacing: 4.0f);
        bool sampleSurfaceMatchesSampler = TerrainSamplesMatch(expectedSurface, facadeSurface);

        const float heightOffset = 2.75f;
        Vector3 surfacePosition = noPlanWorld.SurfacePositionAt(query, heightOffset);
        bool surfacePositionAxesPassed =
            ExactFloatEquals(surfacePosition.X, query.X) &&
            ExactFloatEquals(surfacePosition.Y, expectedField.Height + heightOffset) &&
            ExactFloatEquals(surfacePosition.Z, query.Y);

        bool traversabilityQueryPassed =
            noPlanWorld.IsTraversable(query, 0.45f) == (expectedField.Traversability >= 0.45f);
        bool aboveWaterQueryPassed =
            noPlanWorld.IsAboveWater(query) == (expectedField.Height >= profile.SeaLevel);
        TerrainWaterState expectedWaterState = TerrainSemanticClassifier.ClassifyWater(expectedField, profile);
        TerrainWaterState facadeWaterState = noPlanWorld.SampleWaterState(query);
        bool waterStateQueryPassed = TerrainWaterStatesMatch(expectedWaterState, facadeWaterState);
        TerrainGameplayTags expectedGameplayTags = TerrainSemanticClassifier.ClassifyGameplayTags(expectedField, profile);
        TerrainGameplayTags facadeGameplayTags = noPlanWorld.SampleGameplayTags(query);
        bool gameplayTagsQueryPassed = TerrainGameplayTagsMatch(expectedGameplayTags, facadeGameplayTags);
        TerrainTraversalCost expectedTraversalCost =
            TerrainSemanticClassifier.ClassifyTraversalCost(expectedField, expectedSurface, profile);
        TerrainTraversalCost facadeTraversalCost = noPlanWorld.SampleTraversalCost(query, spacing: 4.0f);
        bool traversalCostQueryPassed = TerrainTraversalCostsMatch(expectedTraversalCost, facadeTraversalCost);
        bool streamingSnapshotPassed =
            StreamingSnapshotMatchesFacadeContract(
                noPlanWorld,
                profile,
                hasWorldPlan: false,
                expectedDesiredCoords: [new TerrainTileCoord(-1, 0), new TerrainTileCoord(2, -1)],
                expectedLoadedCoords: [new TerrainTileCoord(-3, 4), new TerrainTileCoord(5, 2)],
                expectedQueuedCoords: [new TerrainTileCoord(-4, -3), new TerrainTileCoord(8, -2)]);
        bool apiVersionPassed =
            TerrainApiVersion.Major == 1 &&
            TerrainApiVersion.Minor == 2 &&
            TerrainApiVersion.Patch == 0 &&
            string.Equals(TerrainApiVersion.Contract, "terrain-api-v1", StringComparison.Ordinal) &&
            string.Equals(TerrainApiVersion.Version, "1.2.0", StringComparison.Ordinal);
        bool determinismContractPassed =
            string.Equals(TerrainDeterminismContract.Contract, "terrain-determinism-v1", StringComparison.Ordinal) &&
            ExactFloatEquals(TerrainDeterminismContract.HeightEpsilon, 0.05f) &&
            ExactFloatEquals(TerrainDeterminismContract.FieldEpsilon, 0.001f) &&
            ExactFloatEquals(TerrainDeterminismContract.PositionEpsilon, 0.10f) &&
            ExactFloatEquals(TerrainDeterminismContract.NativeHeightMaxEpsilon, 1.5f) &&
            ExactFloatEquals(TerrainDeterminismContract.NativeFieldMaxEpsilon, 0.015f) &&
            ExactFloatEquals(TerrainDeterminismContract.NativeTileColorEpsilon, 0.03f);
        bool performanceContractPassed =
            string.Equals(TerrainPerformanceContract.Contract, "terrain-performance-v1", StringComparison.Ordinal) &&
            string.Equals(TerrainPerformanceContract.TileBenchmarkHardwareBaseline, "dev-linux-x64-provisional", StringComparison.Ordinal) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxManagedMillisecondsPerTile, 24.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxNativeMillisecondsPerTile, 8.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxManagedP50Milliseconds, 24.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxManagedP95Milliseconds, 48.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxManagedP99Milliseconds, 72.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxNativeP50Milliseconds, 8.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxNativeP95Milliseconds, 16.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxNativeP99Milliseconds, 24.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MaxAllocatedKilobytesPerTile, 2048.0) &&
            ExactDoubleEquals(TerrainPerformanceContract.MinNativeSpeedup, 1.00) &&
            TerrainPerformanceContract.MinParityTileCount == 8 &&
            TerrainPerformanceContract.MinBenchmarkBiomeKinds == 7 &&
            TerrainPerformanceContract.MinBenchmarkLandscapeKinds == 6 &&
            TerrainPerformanceContract.MinBenchmarkPointOfInterestTiles == 8 &&
            TerrainPerformanceContract.MinBenchmarkRouteTiles == 8 &&
            TerrainPerformanceContract.MinBenchmarkGameplayRichTiles == 12;
        bool integrationInterfacesPassed = TerrainWorldImplementsIntegrationContracts();
        bool signalContractsPassed = TerrainWorldSignalContractMatches();

        TerrainWorld planWorld = CreateTerrainWorldFacadeProbe(profile, plan);
        streamingSnapshotPassed =
            streamingSnapshotPassed &&
            StreamingSnapshotMatchesFacadeContract(
                planWorld,
                profile,
                hasWorldPlan: true,
                expectedDesiredCoords: [],
                expectedLoadedCoords: [],
                expectedQueuedCoords: []);
        bool planTryGetPassed =
            planWorld.TryGetWorldPlan(out TerrainWorldPlan? returnedPlan) &&
            returnedPlan is not null &&
            !ReferenceEquals(returnedPlan, plan) &&
            TerrainPlansMatchForJson(plan, returnedPlan) &&
            RuntimeWorldPlanFacadeIsolated(planWorld, returnedPlan, plan);
        bool planSnapshotTryGetPassed = planWorld.TryGetWorldPlanSnapshot(out TerrainWorldPlanSnapshot? planSnapshot) &&
            planSnapshot is not null &&
            planSnapshot.PointsOfInterest.Length == plan.PointsOfInterest.Length &&
            planSnapshot.Routes.Length == plan.Routes.Length &&
            planSnapshot.Regions.Length == plan.Regions.Length;

        TerrainWorldPointOfInterest[] points = planWorld.GetPointsOfInterest();
        bool pointSnapshotIsolated = points.Length == plan.PointsOfInterest.Length;
        if (pointSnapshotIsolated && points.Length > 0)
        {
            points[0] = default;
            TerrainWorldPointOfInterest[] secondRead = planWorld.GetPointsOfInterest();
            pointSnapshotIsolated =
                secondRead.Length == plan.PointsOfInterest.Length &&
                secondRead[0].Id == plan.PointsOfInterest[0].Id &&
                secondRead[0].Kind == plan.PointsOfInterest[0].Kind;
        }

        TerrainWorldRoute[] routes = planWorld.GetRoutes();
        bool routeSnapshotIsolated = routes.Length == plan.Routes.Length;
        if (routeSnapshotIsolated && routes.Length > 0)
        {
            bool waypointSnapshotIsolated = routes[0].Waypoints.Length == plan.Routes[0].Waypoints.Length;
            if (waypointSnapshotIsolated && routes[0].Waypoints.Length > 0)
            {
                Vector2 originalWaypoint = plan.Routes[0].Waypoints[0];
                routes[0].Waypoints[0] = originalWaypoint + new Vector2(9999.0f, -9999.0f);
                TerrainWorldRoute[] secondRead = planWorld.GetRoutes();
                waypointSnapshotIsolated =
                    secondRead.Length == plan.Routes.Length &&
                    secondRead[0].Waypoints.Length == plan.Routes[0].Waypoints.Length &&
                    ExactPositionEquals(secondRead[0].Waypoints[0], originalWaypoint);
            }

            routes[0] = default;
            TerrainWorldRoute[] routeSecondRead = planWorld.GetRoutes();
            routeSnapshotIsolated =
                routeSecondRead.Length == plan.Routes.Length &&
                routeSecondRead[0].FromPointId == plan.Routes[0].FromPointId &&
                routeSecondRead[0].ToPointId == plan.Routes[0].ToPointId &&
                waypointSnapshotIsolated;
        }

        bool worldPlanSnapshotIsolated = planSnapshotTryGetPassed && planSnapshot is not null;
        if (worldPlanSnapshotIsolated && planSnapshot is not null)
        {
            if (planSnapshot.Regions.Length > 0)
            {
                TerrainWorldRegion originalRegion = plan.Regions[0];
                planSnapshot.Regions[0] = originalRegion with { Height = originalRegion.Height + 12345.0f };
                TerrainWorldPlanSnapshot secondSnapshot = planWorld.GetWorldPlanSnapshot();
                worldPlanSnapshotIsolated =
                    secondSnapshot.Regions.Length == plan.Regions.Length &&
                    secondSnapshot.Regions[0].GridX == originalRegion.GridX &&
                    secondSnapshot.Regions[0].GridY == originalRegion.GridY &&
                    ExactFloatEquals(secondSnapshot.Regions[0].Height, originalRegion.Height);
            }

            if (worldPlanSnapshotIsolated && planSnapshot.PointsOfInterest.Length > 0)
            {
                TerrainWorldPointOfInterest originalPoint = plan.PointsOfInterest[0];
                planSnapshot.PointsOfInterest[0] = originalPoint with { Id = originalPoint.Id + 1000000 };
                TerrainWorldPlanSnapshot secondSnapshot = planWorld.GetWorldPlanSnapshot();
                worldPlanSnapshotIsolated =
                    secondSnapshot.PointsOfInterest.Length == plan.PointsOfInterest.Length &&
                    secondSnapshot.PointsOfInterest[0].Id == originalPoint.Id &&
                    secondSnapshot.PointsOfInterest[0].Kind == originalPoint.Kind;
            }

            if (worldPlanSnapshotIsolated && planSnapshot.Routes.Length > 0)
            {
                TerrainWorldRoute originalRoute = plan.Routes[0];
                bool waypointSnapshotIsolated = planSnapshot.Routes[0].Waypoints.Length == originalRoute.Waypoints.Length;
                if (waypointSnapshotIsolated && planSnapshot.Routes[0].Waypoints.Length > 0)
                {
                    Vector2 originalWaypoint = originalRoute.Waypoints[0];
                    planSnapshot.Routes[0].Waypoints[0] = originalWaypoint + new Vector2(-7777.0f, 7777.0f);
                    TerrainWorldPlanSnapshot secondSnapshot = planWorld.GetWorldPlanSnapshot();
                    waypointSnapshotIsolated =
                        secondSnapshot.Routes.Length == plan.Routes.Length &&
                        secondSnapshot.Routes[0].Waypoints.Length == originalRoute.Waypoints.Length &&
                        ExactPositionEquals(secondSnapshot.Routes[0].Waypoints[0], originalWaypoint);
                }

                planSnapshot.Routes[0] = originalRoute with { FromPointId = originalRoute.FromPointId + 1000000 };
                TerrainWorldPlanSnapshot routeSecondSnapshot = planWorld.GetWorldPlanSnapshot();
                worldPlanSnapshotIsolated =
                    routeSecondSnapshot.Routes.Length == plan.Routes.Length &&
                    routeSecondSnapshot.Routes[0].FromPointId == originalRoute.FromPointId &&
                    routeSecondSnapshot.Routes[0].ToPointId == originalRoute.ToPointId &&
                    waypointSnapshotIsolated;
            }
        }

        bool pointQueryPassed = plan.PointsOfInterest.Length == 0;
        if (plan.PointsOfInterest.Length > 0)
        {
            TerrainWorldPointOfInterest expectedPoint = plan.PointsOfInterest[0];
            Rect2 pointBounds = new(
                expectedPoint.WorldPosition - new Vector2(1.0f, 1.0f),
                new Vector2(2.0f, 2.0f));
            bool nearestPassed =
                planWorld.TryFindNearestPointOfInterest(
                    expectedPoint.WorldPosition,
                    radius: 0.01f,
                    expectedPoint.Kind,
                    out TerrainWorldPointOfInterest nearestPoint) &&
                nearestPoint.Id == expectedPoint.Id &&
                !planWorld.TryFindNearestPointOfInterest(
                    expectedPoint.WorldPosition + new Vector2(profile.ChunkSize * 8.0f, profile.ChunkSize * 8.0f),
                    radius: 0.01f,
                    kind: null,
                    out _);
            TerrainWorldPointOfInterest[] pointQuery = planWorld.QueryPointsOfInterest(pointBounds, expectedPoint.Kind);
            bool boundsPassed = ContainsPointOfInterest(pointQuery, expectedPoint);
            if (boundsPassed)
            {
                pointQuery[0] = default;
                TerrainWorldPointOfInterest[] secondPointQuery = planWorld.QueryPointsOfInterest(pointBounds, expectedPoint.Kind);
                boundsPassed = ContainsPointOfInterest(secondPointQuery, expectedPoint);
            }

            pointQueryPassed = nearestPassed && boundsPassed;
        }

        bool routeQueryPassed = plan.Routes.Length == 0;
        bool routeCorridorQueryPassed = plan.Routes.Length == 0;
        if (plan.Routes.Length > 0)
        {
            TerrainWorldRoute expectedRoute = plan.Routes[0];
            Vector2 routeQueryPoint = expectedRoute.Waypoints.Length > 0
                ? expectedRoute.Waypoints[0]
                : query;
            TerrainWorldRoute[] routeQuery = planWorld.QueryRoutesNear(routeQueryPoint, radius: 1.0f);
            bool routeFound = ContainsRoute(routeQuery, expectedRoute);
            bool waypointIsolationPassed = routeFound;
            if (waypointIsolationPassed && routeQuery.Length > 0 && routeQuery[0].Waypoints.Length > 0)
            {
                Vector2 originalWaypoint = routeQuery[0].Waypoints[0];
                routeQuery[0].Waypoints[0] = originalWaypoint + new Vector2(3333.0f, -3333.0f);
                TerrainWorldRoute[] secondRouteQuery = planWorld.QueryRoutesNear(routeQueryPoint, radius: 1.0f);
                waypointIsolationPassed = ContainsRoute(secondRouteQuery, expectedRoute);
            }

            routeQueryPassed = routeFound && waypointIsolationPassed;

            TerrainRouteCorridorIndex expectedCorridors = TerrainRouteCorridorIndex.FromPlan(plan, profile);
            foreach (TerrainWorldRoute route in plan.Routes)
            {
                if (route.Waypoints.Length < 2)
                {
                    continue;
                }

                Vector2 corridorQueryPoint = route.Waypoints[0].Lerp(route.Waypoints[1], 0.5f);
                TerrainRouteCorridorSample expectedCorridor =
                    expectedCorridors.Sample(corridorQueryPoint, WorldToCoord(corridorQueryPoint, profile));
                TerrainRouteCorridorSample facadeCorridor = planWorld.SampleRouteCorridor(corridorQueryPoint);
                routeCorridorQueryPassed =
                    expectedCorridor.HasInfluence &&
                    TerrainRouteCorridorSamplesMatch(expectedCorridor, facadeCorridor);
                break;
            }
        }

        bool passed =
            noPlanTryGetPassed &&
            noPlanSnapshotPassed &&
            emptyPlanCollectionsPassed &&
            sampleFieldMatchesSampler &&
            sampleSurfaceMatchesSampler &&
            surfacePositionAxesPassed &&
            traversabilityQueryPassed &&
            aboveWaterQueryPassed &&
            waterStateQueryPassed &&
            gameplayTagsQueryPassed &&
            traversalCostQueryPassed &&
            streamingSnapshotPassed &&
            apiVersionPassed &&
            determinismContractPassed &&
            performanceContractPassed &&
            integrationInterfacesPassed &&
            signalContractsPassed &&
            planTryGetPassed &&
            planSnapshotTryGetPassed &&
            points.Length == plan.PointsOfInterest.Length &&
            routes.Length == plan.Routes.Length &&
            pointQueryPassed &&
            routeQueryPassed &&
            routeCorridorQueryPassed &&
            pointSnapshotIsolated &&
            routeSnapshotIsolated &&
            worldPlanSnapshotIsolated;

        string reason = passed
            ? "TerrainWorld runtime facade exposes stable pure queries, semantic tags, and isolated plan snapshots"
            : RuntimeApiFailureReason(
                noPlanTryGetPassed,
                noPlanSnapshotPassed,
                emptyPlanCollectionsPassed,
                sampleFieldMatchesSampler,
                sampleSurfaceMatchesSampler,
                surfacePositionAxesPassed,
                traversabilityQueryPassed,
                aboveWaterQueryPassed,
                waterStateQueryPassed,
                gameplayTagsQueryPassed,
                traversalCostQueryPassed,
                streamingSnapshotPassed,
                apiVersionPassed,
                determinismContractPassed,
                performanceContractPassed,
                integrationInterfacesPassed,
                signalContractsPassed,
                planTryGetPassed,
                planSnapshotTryGetPassed,
                points.Length,
                plan.PointsOfInterest.Length,
                routes.Length,
                plan.Routes.Length,
                pointQueryPassed,
                routeQueryPassed,
                routeCorridorQueryPassed,
                pointSnapshotIsolated,
                routeSnapshotIsolated,
                worldPlanSnapshotIsolated);

        return new TerrainRuntimeApiSmokeReport(
            passed,
            sampleFieldMatchesSampler,
            sampleSurfaceMatchesSampler,
            surfacePositionAxesPassed,
            noPlanTryGetPassed,
            noPlanSnapshotPassed,
            emptyPlanCollectionsPassed,
            planTryGetPassed,
            planSnapshotTryGetPassed,
            points.Length,
            routes.Length,
            traversabilityQueryPassed,
            aboveWaterQueryPassed,
            waterStateQueryPassed,
            gameplayTagsQueryPassed,
            traversalCostQueryPassed,
            streamingSnapshotPassed,
            apiVersionPassed,
            determinismContractPassed,
            performanceContractPassed,
            integrationInterfacesPassed,
            signalContractsPassed,
            pointQueryPassed,
            routeQueryPassed,
            routeCorridorQueryPassed,
            pointSnapshotIsolated,
            routeSnapshotIsolated,
            worldPlanSnapshotIsolated,
            reason);
    }
    catch (Exception ex)
    {
        return new TerrainRuntimeApiSmokeReport(
            Passed: false,
            SampleFieldMatchesSampler: false,
            SampleSurfaceMatchesSampler: false,
            SurfacePositionAxesPassed: false,
            NoPlanTryGetPassed: false,
            NoPlanSnapshotPassed: false,
            EmptyPlanCollectionsPassed: false,
            PlanTryGetPassed: false,
            PlanSnapshotTryGetPassed: false,
            PointOfInterestCount: 0,
            RouteCount: 0,
            TraversabilityQueryPassed: false,
            AboveWaterQueryPassed: false,
            WaterStateQueryPassed: false,
            GameplayTagsQueryPassed: false,
            TraversalCostQueryPassed: false,
            StreamingSnapshotPassed: false,
            ApiVersionPassed: false,
            DeterminismContractPassed: false,
            PerformanceContractPassed: false,
            IntegrationInterfacesPassed: false,
            SignalContractsPassed: false,
            PointQueryPassed: false,
            RouteQueryPassed: false,
            RouteCorridorQueryPassed: false,
            PointSnapshotIsolated: false,
            RouteSnapshotIsolated: false,
            WorldPlanSnapshotIsolated: false,
            Reason: $"TerrainWorld runtime facade threw {ex.GetType().Name}: {ex.Message}");
    }
}

static TerrainWorld CreateTerrainWorldFacadeProbe(
    TerrainGenerationProfile profile,
    TerrainWorldPlan? worldPlan)
{
    var world = (TerrainWorld)RuntimeHelpers.GetUninitializedObject(typeof(TerrainWorld));
    SetPrivateField(world, "_profile", profile);
    SetPrivateField(world, "_hasProfileSnapshot", true);
    SetPrivateField(world, "_worldPlan", worldPlan);
    SetPrivateField(
        world,
        "_routeCorridors",
        worldPlan is null ? TerrainRouteCorridorIndex.Empty : TerrainRouteCorridorIndex.FromPlan(worldPlan, profile));
    SetPrivateField(
        world,
        "_pointOfInterestIndex",
        worldPlan is null ? TerrainPointOfInterestIndex.Empty : TerrainPointOfInterestIndex.FromPlan(worldPlan, profile));
    world.StreamTerrainBeforeOpenWorldPlanReady = true;
    return world;
}

static void SetPrivateField<T>(object instance, string fieldName, T value)
{
    FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
    if (field is null)
    {
        throw new MissingFieldException(instance.GetType().FullName, fieldName);
    }

    field.SetValue(instance, value);
}

static void InvokePrivateMethod(object instance, string methodName)
{
    MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
    if (method is null)
    {
        throw new MissingMethodException(instance.GetType().FullName, methodName);
    }

    method.Invoke(instance, null);
}

static object CreatePendingTileJobKeyDictionary(TerrainTileCoord[] coords)
{
    Type worldType = typeof(TerrainWorld);
    Type pendingJobType = worldType.GetNestedType("PendingTileJob", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(worldType.FullName, "PendingTileJob");
    Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(TerrainTileCoord), pendingJobType);
    var dictionary = (System.Collections.IDictionary)(Activator.CreateInstance(dictionaryType)
        ?? throw new InvalidOperationException("Failed to create pending tile job dictionary."));

    foreach (TerrainTileCoord coord in coords)
    {
        dictionary.Add(coord, null);
    }

    return dictionary;
}

static object CreatePendingTileJobStateDictionary(
    TerrainTileCoord[] coords,
    TerrainGenerationProfile profile,
    int terrainFeatureKey)
{
    Type worldType = typeof(TerrainWorld);
    Type pendingJobType = worldType.GetNestedType("PendingTileJob", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(worldType.FullName, "PendingTileJob");
    Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(TerrainTileCoord), pendingJobType);
    var dictionary = (System.Collections.IDictionary)(Activator.CreateInstance(dictionaryType)
        ?? throw new InvalidOperationException("Failed to create pending tile job dictionary."));

    foreach (TerrainTileCoord coord in coords)
    {
        dictionary.Add(coord, CreatePendingTileJob(pendingJobType, coord, profile, terrainFeatureKey));
    }

    return dictionary;
}

static object CreatePendingTileJob(
    Type pendingJobType,
    TerrainTileCoord coord,
    TerrainGenerationProfile profile,
    int terrainFeatureKey)
{
    var completion = new TaskCompletionSource<TerrainTileData>();
    return Activator.CreateInstance(
            pendingJobType,
            coord,
            0,
            false,
            profile,
            terrainFeatureKey,
            new CancellationTokenSource(),
            completion.Task)
        ?? throw new InvalidOperationException("Failed to create pending tile job.");
}

static object CreatePendingTileJobList()
{
    Type pendingJobType = typeof(TerrainWorld).GetNestedType("PendingTileJob", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(TerrainWorld).FullName, "PendingTileJob");
    Type listType = typeof(List<>).MakeGenericType(pendingJobType);
    return Activator.CreateInstance(listType)
        ?? throw new InvalidOperationException("Failed to create retired pending tile job list.");
}

static object CreateTileCacheDictionary(
    TerrainTileCoord coord,
    TerrainGenerationProfile profile,
    int terrainFeatureKey)
{
    Type cacheKeyType = typeof(TerrainWorld).GetNestedType("TerrainTileCacheKey", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(TerrainWorld).FullName, "TerrainTileCacheKey");
    object key = Activator.CreateInstance(cacheKeyType, coord, 0, false, profile, terrainFeatureKey)
        ?? throw new InvalidOperationException("Failed to create tile cache key.");
    Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(cacheKeyType, typeof(TerrainTileData));
    var dictionary = (System.Collections.IDictionary)(Activator.CreateInstance(dictionaryType)
        ?? throw new InvalidOperationException("Failed to create tile cache dictionary."));
    dictionary.Add(key, null);
    return dictionary;
}

static object CreateTileCacheNodeDictionary()
{
    Type cacheKeyType = typeof(TerrainWorld).GetNestedType("TerrainTileCacheKey", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(TerrainWorld).FullName, "TerrainTileCacheKey");
    Type nodeType = typeof(LinkedListNode<>).MakeGenericType(cacheKeyType);
    Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(cacheKeyType, nodeType);
    return Activator.CreateInstance(dictionaryType)
        ?? throw new InvalidOperationException("Failed to create tile cache node dictionary.");
}

static object CreateTileCacheLinkedList()
{
    Type cacheKeyType = typeof(TerrainWorld).GetNestedType("TerrainTileCacheKey", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(TerrainWorld).FullName, "TerrainTileCacheKey");
    Type listType = typeof(LinkedList<>).MakeGenericType(cacheKeyType);
    return Activator.CreateInstance(listType)
        ?? throw new InvalidOperationException("Failed to create tile cache LRU list.");
}

static int GetPrivateCollectionCount(object instance, string fieldName)
{
    FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
    if (field is null)
    {
        throw new MissingFieldException(instance.GetType().FullName, fieldName);
    }

    object value = field.GetValue(instance)
        ?? throw new InvalidOperationException($"Private field {fieldName} was null.");
    PropertyInfo? count = value.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
    if (count?.GetValue(value) is int result)
    {
        return result;
    }

    throw new InvalidOperationException($"Private field {fieldName} does not expose a Count property.");
}

static bool RuntimeWorldPlanFacadeIsolated(
    TerrainWorld world,
    TerrainWorldPlan returnedPlan,
    TerrainWorldPlan expectedPlan)
{
    if (returnedPlan.Regions.Length > 0)
    {
        TerrainWorldRegion originalRegion = expectedPlan.Regions[0];
        returnedPlan.Regions[0] = originalRegion with { Height = originalRegion.Height + 9999.0f };
        if (!world.TryGetWorldPlan(out TerrainWorldPlan? secondPlan) ||
            secondPlan is null ||
            secondPlan.Regions.Length != expectedPlan.Regions.Length ||
            !RegionsMatchForJson(originalRegion, secondPlan.Regions[0]))
        {
            return false;
        }
    }

    if (returnedPlan.PointsOfInterest.Length > 0)
    {
        TerrainWorldPointOfInterest originalPoint = expectedPlan.PointsOfInterest[0];
        returnedPlan.PointsOfInterest[0] = originalPoint with { Id = originalPoint.Id + 9999 };
        if (!world.TryGetWorldPlan(out TerrainWorldPlan? secondPlan) ||
            secondPlan is null ||
            secondPlan.PointsOfInterest.Length != expectedPlan.PointsOfInterest.Length ||
            !PointsMatchForJson(originalPoint, secondPlan.PointsOfInterest[0]))
        {
            return false;
        }
    }

    if (returnedPlan.Routes.Length > 0)
    {
        TerrainWorldRoute originalRoute = expectedPlan.Routes[0];
        returnedPlan.Routes[0] = returnedPlan.Routes[0] with { FromPointId = originalRoute.FromPointId + 9999 };
        if (!world.TryGetWorldPlan(out TerrainWorldPlan? secondPlan) ||
            secondPlan is null ||
            secondPlan.Routes.Length != expectedPlan.Routes.Length ||
            !RoutesMatchForJson(originalRoute, secondPlan.Routes[0]))
        {
            return false;
        }

        if (originalRoute.Waypoints.Length > 0 && returnedPlan.Routes[0].Waypoints.Length > 0)
        {
            returnedPlan.Routes[0].Waypoints[0] = originalRoute.Waypoints[0] + new Vector2(9999.0f, -9999.0f);
            if (!world.TryGetWorldPlan(out TerrainWorldPlan? waypointPlan) ||
                waypointPlan is null ||
                waypointPlan.Routes.Length != expectedPlan.Routes.Length ||
                !RoutesMatchForJson(originalRoute, waypointPlan.Routes[0]))
            {
                return false;
            }
        }
    }

    return true;
}

static bool StreamingSnapshotMatchesFacadeContract(
    TerrainWorld world,
    TerrainGenerationProfile profile,
    bool hasWorldPlan,
    TerrainTileCoord[] expectedDesiredCoords,
    TerrainTileCoord[] expectedLoadedCoords,
    TerrainTileCoord[] expectedQueuedCoords)
{
    TerrainWorldStreamingSnapshot snapshot = world.GetStreamingSnapshot();
    if (!StreamingSnapshotValuesMatch(
            snapshot,
            profile,
            hasWorldPlan,
            expectedDesiredCoords,
            expectedLoadedCoords,
            expectedQueuedCoords))
    {
        return false;
    }

    if (snapshot.DesiredChunks.Length > 0)
    {
        snapshot.DesiredChunks[0] = new TerrainTileCoord(999, 999);
    }

    if (snapshot.LoadedChunks.Length > 0)
    {
        snapshot.LoadedChunks[0] = new TerrainTileCoord(888, 888);
    }

    if (snapshot.QueuedTileJobs.Length > 0)
    {
        snapshot.QueuedTileJobs[0] = new TerrainTileCoord(777, 777);
    }

    TerrainWorldStreamingSnapshot secondSnapshot = world.GetStreamingSnapshot();
    return StreamingSnapshotValuesMatch(
        secondSnapshot,
        profile,
        hasWorldPlan,
        expectedDesiredCoords,
        expectedLoadedCoords,
        expectedQueuedCoords);
}

static bool StreamingSnapshotValuesMatch(
    TerrainWorldStreamingSnapshot snapshot,
    TerrainGenerationProfile profile,
    bool hasWorldPlan,
    TerrainTileCoord[] expectedDesiredCoords,
    TerrainTileCoord[] expectedLoadedCoords,
    TerrainTileCoord[] expectedQueuedCoords)
{
    return snapshot.Profile.Equals(profile) &&
        !snapshot.HasFocus &&
        snapshot.FocusPosition == Vector3.Zero &&
        snapshot.FocusCoord == default &&
        snapshot.StreamRadiusChunks == profile.StreamRadiusChunks &&
        snapshot.DesiredChunkCount == snapshot.DesiredChunks.Length &&
        snapshot.LoadedChunkCount == snapshot.LoadedChunks.Length &&
        snapshot.QueuedTileJobCount == snapshot.QueuedTileJobs.Length &&
        snapshot.RetiredTileJobCount == 0 &&
        snapshot.TileCacheLimit == Mathf.Max(0, profile.MaxCachedTileData) &&
        snapshot.MaxQueuedTileJobs == profile.MaxQueuedTileJobs &&
        snapshot.MaxCompletedTilesPerFrame == profile.MaxCompletedTilesPerFrame &&
        snapshot.HasWorldPlan == hasWorldPlan &&
        !snapshot.IsWorldPlanGenerationPending &&
        snapshot.StreamTerrainBeforeOpenWorldPlanReady &&
        snapshot.TileCacheWithinLimit &&
        snapshot.TileJobQueueWithinLimit &&
        !snapshot.CanStreamTerrain &&
        !snapshot.FocusTileLoaded &&
        !snapshot.DesiredChunksLoaded &&
        !snapshot.FocusAreaReady &&
        StreamingReadinessContractMatches(profile) &&
        TileCoordsMatch(snapshot.DesiredChunks, expectedDesiredCoords) &&
        TileCoordsMatch(snapshot.LoadedChunks, expectedLoadedCoords) &&
        TileCoordsMatch(snapshot.QueuedTileJobs, expectedQueuedCoords);
}

static bool StreamingReadinessContractMatches(TerrainGenerationProfile profile)
{
    TerrainTileCoord focusCoord = new(4, -2);
    TerrainTileCoord neighborCoord = new(5, -2);
    Vector3 focusPosition = new(
        focusCoord.X * profile.ChunkSize + profile.ChunkSize * 0.5f,
        0.0f,
        focusCoord.Z * profile.ChunkSize + profile.ChunkSize * 0.5f);
    TerrainTileCoord[] desired = [focusCoord, neighborCoord];
    TerrainTileCoord[] loaded = [focusCoord, neighborCoord];

    TerrainWorldStreamingSnapshot waitingForPlan = CreateStreamingReadinessSnapshot(
        profile,
        hasFocus: true,
        focusPosition,
        focusCoord,
        desired,
        loaded,
        queuedJobs: [],
        retiredJobCount: 0,
        tileCacheCount: 0,
        hasWorldPlan: false,
        isWorldPlanGenerationPending: true,
        streamTerrainBeforeOpenWorldPlanReady: false);
    if (waitingForPlan.CanStreamTerrain || waitingForPlan.FocusAreaReady)
    {
        return false;
    }

    TerrainWorldStreamingSnapshot queued = CreateStreamingReadinessSnapshot(
        profile,
        hasFocus: true,
        focusPosition,
        focusCoord,
        desired,
        loaded,
        queuedJobs: [neighborCoord],
        retiredJobCount: 0,
        tileCacheCount: 1,
        hasWorldPlan: true,
        isWorldPlanGenerationPending: false,
        streamTerrainBeforeOpenWorldPlanReady: false);
    if (!queued.CanStreamTerrain ||
        !queued.FocusTileLoaded ||
        !queued.DesiredChunksLoaded ||
        queued.FocusAreaReady)
    {
        return false;
    }

    TerrainWorldStreamingSnapshot ready = CreateStreamingReadinessSnapshot(
        profile,
        hasFocus: true,
        focusPosition,
        focusCoord,
        desired,
        loaded,
        queuedJobs: [],
        retiredJobCount: 0,
        tileCacheCount: 1,
        hasWorldPlan: true,
        isWorldPlanGenerationPending: false,
        streamTerrainBeforeOpenWorldPlanReady: false);
    if (!ready.CanStreamTerrain ||
        !ready.FocusTileLoaded ||
        !ready.DesiredChunksLoaded ||
        !ready.FocusAreaReady)
    {
        return false;
    }

    TerrainWorldStreamingSnapshot missingFocusTile = CreateStreamingReadinessSnapshot(
        profile,
        hasFocus: true,
        focusPosition,
        focusCoord,
        desired,
        loaded: [neighborCoord],
        queuedJobs: [],
        retiredJobCount: 0,
        tileCacheCount: 1,
        hasWorldPlan: true,
        isWorldPlanGenerationPending: false,
        streamTerrainBeforeOpenWorldPlanReady: false);
    if (missingFocusTile.FocusTileLoaded ||
        missingFocusTile.DesiredChunksLoaded ||
        missingFocusTile.FocusAreaReady)
    {
        return false;
    }

    TerrainWorldStreamingSnapshot overBudget = CreateStreamingReadinessSnapshot(
        profile,
        hasFocus: true,
        focusPosition,
        focusCoord,
        desired,
        loaded,
        queuedJobs: CreateTileCoordArray(Mathf.Max(0, profile.MaxQueuedTileJobs) + 1, focusCoord),
        retiredJobCount: 0,
        tileCacheCount: Mathf.Max(0, profile.MaxCachedTileData) + 1,
        hasWorldPlan: true,
        isWorldPlanGenerationPending: false,
        streamTerrainBeforeOpenWorldPlanReady: false);
    return !overBudget.TileJobQueueWithinLimit &&
        !overBudget.CanStreamTerrain &&
        !overBudget.FocusAreaReady;
}

static TerrainTileCoord[] CreateTileCoordArray(int count, TerrainTileCoord start)
{
    var coords = new TerrainTileCoord[count];
    for (int i = 0; i < coords.Length; i++)
    {
        coords[i] = new TerrainTileCoord(start.X + i, start.Z);
    }

    return coords;
}

static TerrainWorldStreamingSnapshot CreateStreamingReadinessSnapshot(
    TerrainGenerationProfile profile,
    bool hasFocus,
    Vector3 focusPosition,
    TerrainTileCoord focusCoord,
    TerrainTileCoord[] desired,
    TerrainTileCoord[] loaded,
    TerrainTileCoord[] queuedJobs,
    int retiredJobCount,
    int tileCacheCount,
    bool hasWorldPlan,
    bool isWorldPlanGenerationPending,
    bool streamTerrainBeforeOpenWorldPlanReady)
{
    return new TerrainWorldStreamingSnapshot(
        profile,
        hasFocus,
        focusPosition,
        focusCoord,
        profile.StreamRadiusChunks,
        desired.Length,
        desired,
        loaded.Length,
        loaded,
        queuedJobs.Length,
        queuedJobs,
        retiredJobCount,
        tileCacheCount,
        Mathf.Max(0, profile.MaxCachedTileData),
        profile.MaxQueuedTileJobs,
        profile.MaxCompletedTilesPerFrame,
        hasWorldPlan,
        isWorldPlanGenerationPending,
        streamTerrainBeforeOpenWorldPlanReady);
}

static bool TileCoordsMatch(TerrainTileCoord[] actual, TerrainTileCoord[] expected)
{
    if (actual.Length != expected.Length)
    {
        return false;
    }

    for (int i = 0; i < actual.Length; i++)
    {
        if (actual[i] != expected[i])
        {
            return false;
        }
    }

    return true;
}

static bool TerrainFieldsMatch(TerrainWorldField expected, TerrainWorldField actual)
{
    return ExactPositionEquals(expected.WorldPosition, actual.WorldPosition) &&
        ExactFloatEquals(expected.Height, actual.Height) &&
        ExactFloatEquals(expected.River, actual.River) &&
        ExactFloatEquals(expected.Lake, actual.Lake) &&
        ExactFloatEquals(expected.Traversability, actual.Traversability) &&
        ExactFloatEquals(expected.ScenicPotential, actual.ScenicPotential) &&
        expected.BiomeKind == actual.BiomeKind &&
        expected.LandscapeKind == actual.LandscapeKind;
}

static bool TerrainSamplesMatch(TerrainSample expected, TerrainSample actual)
{
    return ExactFloatEquals(expected.Height, actual.Height) &&
        ExactFloatEquals(expected.Slope, actual.Slope) &&
        ExactFloatEquals(expected.Traversability, actual.Traversability) &&
        expected.BiomeKind == actual.BiomeKind &&
        expected.LandscapeKind == actual.LandscapeKind &&
        ColorDistance(expected.Color, actual.Color) <= TerrainDeterminismContract.ExactFloatEpsilon;
}

static bool TerrainWaterStatesMatch(TerrainWaterState expected, TerrainWaterState actual)
{
    return ExactPositionEquals(expected.WorldPosition, actual.WorldPosition) &&
        expected.Kind == actual.Kind &&
        ExactFloatEquals(expected.SurfaceHeight, actual.SurfaceHeight) &&
        ExactFloatEquals(expected.Depth, actual.Depth) &&
        ExactFloatEquals(expected.Strength, actual.Strength) &&
        expected.BiomeKind == actual.BiomeKind &&
        expected.LandscapeKind == actual.LandscapeKind;
}

static bool TerrainGameplayTagsMatch(TerrainGameplayTags expected, TerrainGameplayTags actual)
{
    return ExactPositionEquals(expected.WorldPosition, actual.WorldPosition) &&
        expected.Flags == actual.Flags &&
        expected.BiomeKind == actual.BiomeKind &&
        expected.LandscapeKind == actual.LandscapeKind &&
        expected.WaterKind == actual.WaterKind &&
        ExactFloatEquals(expected.Traversability, actual.Traversability) &&
        ExactFloatEquals(expected.ScenicPotential, actual.ScenicPotential) &&
        ExactFloatEquals(expected.ResourcePotential, actual.ResourcePotential) &&
        ExactFloatEquals(expected.HazardPotential, actual.HazardPotential) &&
        ExactFloatEquals(expected.EncounterPotential, actual.EncounterPotential);
}

static bool TerrainTraversalCostsMatch(TerrainTraversalCost expected, TerrainTraversalCost actual)
{
    bool costMatches = float.IsPositiveInfinity(expected.Cost)
        ? float.IsPositiveInfinity(actual.Cost)
        : ExactFloatEquals(expected.Cost, actual.Cost);
    return ExactPositionEquals(expected.WorldPosition, actual.WorldPosition) &&
        expected.IsBlocked == actual.IsBlocked &&
        costMatches &&
        ExactFloatEquals(expected.Traversability, actual.Traversability) &&
        ExactFloatEquals(expected.Slope, actual.Slope) &&
        ExactFloatEquals(expected.HazardPotential, actual.HazardPotential) &&
        expected.WaterKind == actual.WaterKind &&
        expected.BiomeKind == actual.BiomeKind &&
        expected.LandscapeKind == actual.LandscapeKind;
}

static bool TerrainRouteCorridorSamplesMatch(
    TerrainRouteCorridorSample expected,
    TerrainRouteCorridorSample actual)
{
    return expected.HasInfluence == actual.HasInfluence &&
        expected.Kind == actual.Kind &&
        ExactFloatEquals(expected.Influence, actual.Influence) &&
        ExactFloatEquals(expected.CoreStrength, actual.CoreStrength) &&
        ExactFloatEquals(expected.Distance, actual.Distance) &&
        ExactFloatEquals(expected.TargetHeight, actual.TargetHeight) &&
        ExactFloatEquals(expected.ScenicPotential, actual.ScenicPotential) &&
        ExactFloatEquals(expected.Traversability, actual.Traversability) &&
        ExactFloatEquals(expected.Direction.X, actual.Direction.X) &&
        ExactFloatEquals(expected.Direction.Y, actual.Direction.Y);
}

static bool ContainsPointOfInterest(
    TerrainWorldPointOfInterest[] points,
    TerrainWorldPointOfInterest expected)
{
    foreach (TerrainWorldPointOfInterest point in points)
    {
        if (point.Id == expected.Id &&
            point.Kind == expected.Kind &&
            ExactPositionEquals(point.WorldPosition, expected.WorldPosition))
        {
            return true;
        }
    }

    return false;
}

static bool ContainsRoute(
    TerrainWorldRoute[] routes,
    TerrainWorldRoute expected)
{
    foreach (TerrainWorldRoute route in routes)
    {
        if (route.FromPointId != expected.FromPointId ||
            route.ToPointId != expected.ToPointId ||
            route.Kind != expected.Kind ||
            route.Waypoints.Length != expected.Waypoints.Length)
        {
            continue;
        }

        bool waypointsMatch = true;
        for (int i = 0; i < route.Waypoints.Length; i++)
        {
            if (!ExactPositionEquals(route.Waypoints[i], expected.Waypoints[i]))
            {
                waypointsMatch = false;
                break;
            }
        }

        if (waypointsMatch)
        {
            return true;
        }
    }

    return false;
}

static string RuntimeApiFailureReason(
    bool noPlanTryGetPassed,
    bool noPlanSnapshotPassed,
    bool emptyPlanCollectionsPassed,
    bool sampleFieldMatchesSampler,
    bool sampleSurfaceMatchesSampler,
    bool surfacePositionAxesPassed,
    bool traversabilityQueryPassed,
    bool aboveWaterQueryPassed,
    bool waterStateQueryPassed,
    bool gameplayTagsQueryPassed,
    bool traversalCostQueryPassed,
    bool streamingSnapshotPassed,
    bool apiVersionPassed,
    bool determinismContractPassed,
    bool performanceContractPassed,
    bool integrationInterfacesPassed,
    bool signalContractsPassed,
    bool planTryGetPassed,
    bool planSnapshotTryGetPassed,
    int pointCount,
    int expectedPointCount,
    int routeCount,
    int expectedRouteCount,
    bool pointQueryPassed,
    bool routeQueryPassed,
    bool routeCorridorQueryPassed,
    bool pointSnapshotIsolated,
    bool routeSnapshotIsolated,
    bool worldPlanSnapshotIsolated)
{
    if (!noPlanTryGetPassed)
    {
        return "TryGetWorldPlan did not return false for an unset runtime plan";
    }

    if (!noPlanSnapshotPassed)
    {
        return "TerrainWorld plan snapshot facade did not return an empty no-plan state";
    }

    if (!emptyPlanCollectionsPassed)
    {
        return "POI/routes facade collections were not empty before a plan was assigned";
    }

    if (!sampleFieldMatchesSampler)
    {
        return "SampleField did not match TerrainWorldFieldSampler.Sample";
    }

    if (!sampleSurfaceMatchesSampler)
    {
        return "SampleSurface did not match TerrainSampler.SampleWithSlope";
    }

    if (!surfacePositionAxesPassed)
    {
        return "SurfacePositionAt did not map world X/Y into Godot X/Z with sampled height on Y";
    }

    if (!traversabilityQueryPassed)
    {
        return "IsTraversable did not match sampled terrain traversability";
    }

    if (!aboveWaterQueryPassed)
    {
        return "IsAboveWater did not match sampled height versus sea level";
    }

    if (!waterStateQueryPassed)
    {
        return "SampleWaterState did not match terrain semantic water classification";
    }

    if (!gameplayTagsQueryPassed)
    {
        return "SampleGameplayTags did not match terrain semantic tag classification";
    }

    if (!traversalCostQueryPassed)
    {
        return "SampleTraversalCost did not match terrain semantic traversal classification";
    }

    if (!streamingSnapshotPassed)
    {
        return "GetStreamingSnapshot did not expose stable isolated streaming diagnostics";
    }

    if (!apiVersionPassed)
    {
        return "TerrainApiVersion constants did not match terrain-api-v1 version 1.2.0";
    }

    if (!determinismContractPassed)
    {
        return "TerrainDeterminismContract constants did not match terrain-determinism-v1";
    }

    if (!performanceContractPassed)
    {
        return "TerrainPerformanceContract constants did not match terrain-performance-v1";
    }

    if (!integrationInterfacesPassed)
    {
        return "TerrainWorld did not implement the stable ITerrainQueryService/ITerrainPlanProvider/ITerrainStreamingDiagnostics contracts";
    }

    if (!signalContractsPassed)
    {
        return "TerrainWorld runtime signal contract drifted from PlanReady/PlanCleared/ChunkLoaded/ChunkUnloaded/StreamingSnapshotChanged";
    }

    if (!planTryGetPassed)
    {
        return "TryGetWorldPlan did not return the assigned runtime plan";
    }

    if (!planSnapshotTryGetPassed)
    {
        return "TryGetWorldPlanSnapshot did not return a ready snapshot matching the assigned plan";
    }

    if (pointCount != expectedPointCount || routeCount != expectedRouteCount)
    {
        return $"plan facade counts did not match plan data (POIs {pointCount}/{expectedPointCount}, routes {routeCount}/{expectedRouteCount})";
    }

    if (!pointQueryPassed)
    {
        return "POI semantic query facade did not find or isolate expected planned POIs";
    }

    if (!routeQueryPassed)
    {
        return "route semantic query facade did not find nearby routes or isolate waypoint arrays";
    }

    if (!routeCorridorQueryPassed)
    {
        return "route corridor semantic facade did not match the planned corridor index";
    }

    if (!pointSnapshotIsolated)
    {
        return "GetPointsOfInterest exposed mutable plan array state";
    }

    if (!routeSnapshotIsolated)
    {
        return "GetRoutes exposed mutable route or waypoint array state";
    }

    if (!worldPlanSnapshotIsolated)
    {
        return "TerrainWorld plan snapshot facade exposed mutable plan array state";
    }

    return "TerrainWorld runtime facade failed";
}

static bool TerrainWorldImplementsIntegrationContracts()
{
    Type worldType = typeof(TerrainWorld);
    return typeof(ITerrainQueryService).IsAssignableFrom(worldType) &&
        typeof(ITerrainPlanProvider).IsAssignableFrom(worldType) &&
        typeof(ITerrainStreamingDiagnostics).IsAssignableFrom(worldType);
}

static bool TerrainWorldSignalContractMatches()
{
    Type worldType = typeof(TerrainWorld);
    return HasSignalDelegate(worldType, "PlanReadyEventHandler", Type.EmptyTypes) &&
        HasSignalDelegate(worldType, "PlanClearedEventHandler", Type.EmptyTypes) &&
        HasSignalDelegate(worldType, "ChunkLoadedEventHandler", [typeof(int), typeof(int), typeof(int), typeof(bool)]) &&
        HasSignalDelegate(worldType, "ChunkUnloadedEventHandler", [typeof(int), typeof(int), typeof(int), typeof(bool)]) &&
        HasSignalDelegate(worldType, "StreamingSnapshotChangedEventHandler", Type.EmptyTypes);
}

static bool HasSignalDelegate(Type declaringType, string nestedTypeName, Type[] parameterTypes)
{
    Type? nested = declaringType.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
    if (nested is null ||
        !typeof(MulticastDelegate).IsAssignableFrom(nested))
    {
        return false;
    }

    bool hasSignalAttribute = nested.GetCustomAttributesData()
        .Any(attribute => string.Equals(attribute.AttributeType.Name, "SignalAttribute", StringComparison.Ordinal));
    if (!hasSignalAttribute)
    {
        return false;
    }

    MethodInfo? invoke = nested.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
    if (invoke is null ||
        invoke.ReturnType != typeof(void))
    {
        return false;
    }

    ParameterInfo[] parameters = invoke.GetParameters();
    if (parameters.Length != parameterTypes.Length)
    {
        return false;
    }

    for (int i = 0; i < parameters.Length; i++)
    {
        if (parameters[i].ParameterType != parameterTypes[i])
        {
            return false;
        }
    }

    return true;
}

static void PrintRuntimeApiSmoke(TerrainRuntimeApiSmokeReport report)
{
    Console.WriteLine(
        $"Runtime TerrainWorld API smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"sample field/surface {(report.SampleFieldMatchesSampler ? "pass" : "fail")}/{(report.SampleSurfaceMatchesSampler ? "pass" : "fail")}, " +
        $"surface axes {(report.SurfacePositionAxesPassed ? "pass" : "fail")}, " +
        $"api {TerrainApiVersion.Contract}/{TerrainApiVersion.Version}/{(report.ApiVersionPassed ? "pass" : "fail")}, " +
        $"determinism {TerrainDeterminismContract.Contract}/{(report.DeterminismContractPassed ? "pass" : "fail")}, " +
        $"performance {TerrainPerformanceContract.Contract}/{TerrainPerformanceContract.TileBenchmarkHardwareBaseline}/{(report.PerformanceContractPassed ? "pass" : "fail")}, " +
        $"integration iface/signals {(report.IntegrationInterfacesPassed ? "pass" : "fail")}/{(report.SignalContractsPassed ? "pass" : "fail")}, " +
        $"plan empty/ready {(report.NoPlanTryGetPassed && report.NoPlanSnapshotPassed && report.EmptyPlanCollectionsPassed ? "pass" : "fail")}/{(report.PlanTryGetPassed && report.PlanSnapshotTryGetPassed ? "pass" : "fail")}, " +
        $"POIs/routes {report.PointOfInterestCount}/{report.RouteCount}, " +
        $"traversable/water {(report.TraversabilityQueryPassed ? "pass" : "fail")}/{(report.AboveWaterQueryPassed ? "pass" : "fail")}, " +
        $"semantic POI/route/corridor/water/tags/traversal {(report.PointQueryPassed ? "pass" : "fail")}/{(report.RouteQueryPassed ? "pass" : "fail")}/{(report.RouteCorridorQueryPassed ? "pass" : "fail")}/{(report.WaterStateQueryPassed ? "pass" : "fail")}/{(report.GameplayTagsQueryPassed ? "pass" : "fail")}/{(report.TraversalCostQueryPassed ? "pass" : "fail")}, " +
        $"streaming {(report.StreamingSnapshotPassed ? "pass" : "fail")}, " +
        $"snapshots POI/routes/plan {(report.PointSnapshotIsolated ? "pass" : "fail")}/{(report.RouteSnapshotIsolated ? "pass" : "fail")}/{(report.WorldPlanSnapshotIsolated ? "pass" : "fail")} " +
        $"({report.Reason})");
}

static TerrainAnchorContractSmokeReport ValidateTerrainAnchorContract(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    try
    {
        _ = profile;
        bool poiContractNamesPassed =
            string.Equals(TerrainWorldAnchorContract.PointOfInterestGroup, "terrain_poi", StringComparison.Ordinal) &&
            RequiredKeysMatch(
                TerrainWorldAnchorContract.GetPointOfInterestRequiredMetaKeys(),
                [
                    "terrain_poi_id",
                    "terrain_poi_kind",
                    "terrain_poi_visual",
                    "terrain_poi_gameplay_tag",
                    "terrain_poi_score",
                    "terrain_poi_scenic",
                    "terrain_poi_traversability",
                    "terrain_poi_settlement_tier",
                    "terrain_poi_landscape",
                    "terrain_poi_interaction_radius",
                    "terrain_poi_encounter_budget"
                ]);
        bool routeContractNamesPassed =
            string.Equals(TerrainWorldAnchorContract.RouteGroup, "terrain_route", StringComparison.Ordinal) &&
            RequiredKeysMatch(
                TerrainWorldAnchorContract.GetRouteRequiredMetaKeys(),
                [
                    "terrain_route_kind",
                    "terrain_route_from",
                    "terrain_route_to",
                    "terrain_route_cost",
                    "terrain_route_scenic",
                    "terrain_route_traversability"
                ]);
        bool anchorNodeConstantsPassed = AnchorNodeConstantsMatchContract();

        TerrainWorldPointOfInterestAnchorDescriptor[] points =
            TerrainWorldAnchorContract.CreatePointOfInterestDescriptors(plan);
        TerrainWorldRouteAnchorDescriptor[] routes = TerrainWorldAnchorContract.CreateRouteDescriptors(plan);

        bool pointCountPassed = points.Length == plan.PointsOfInterest.Length;
        bool routeCountPassed = routes.Length == plan.Routes.Length;
        bool poiGroupMetaPassed = pointCountPassed && PointDescriptorsHaveRequiredContract(points, plan);
        bool routeGroupMetaPassed = routeCountPassed && RouteDescriptorsHaveRequiredContract(routes, plan);
        bool descriptorRebuildPassed = ValidateAnchorDescriptorRebuild(plan, points, routes);
        bool routeWaypointSnapshotPassed = RouteDescriptorWaypointsAreIsolated(routes, plan);
        bool builderPlanSnapshotPassed = AnchorBuilderPlanSnapshotIsolated(plan);
        bool overlayPlanSnapshotPassed = PlanOverlaySnapshotIsolated(plan);
        bool metaKeySnapshotPassed = AnchorMetaKeySnapshotsAreIsolated();

        bool passed =
            poiContractNamesPassed &&
            routeContractNamesPassed &&
            anchorNodeConstantsPassed &&
            pointCountPassed &&
            routeCountPassed &&
            poiGroupMetaPassed &&
            routeGroupMetaPassed &&
            routeWaypointSnapshotPassed &&
            descriptorRebuildPassed &&
            builderPlanSnapshotPassed &&
            overlayPlanSnapshotPassed &&
            metaKeySnapshotPassed;
        string reason = passed
            ? "anchor descriptors expose stable gameplay anchor group/meta contracts without requiring debug overlay nodes"
            : AnchorContractFailureReason(
                poiContractNamesPassed,
                routeContractNamesPassed,
                anchorNodeConstantsPassed,
                pointCountPassed,
                routeCountPassed,
                poiGroupMetaPassed,
                routeGroupMetaPassed,
                routeWaypointSnapshotPassed,
                descriptorRebuildPassed,
                builderPlanSnapshotPassed,
                overlayPlanSnapshotPassed,
                metaKeySnapshotPassed,
                points.Length,
                plan.PointsOfInterest.Length,
                routes.Length,
                plan.Routes.Length);

        return new TerrainAnchorContractSmokeReport(
            passed,
            pointCountPassed,
            routeCountPassed,
            poiGroupMetaPassed,
            routeGroupMetaPassed,
            poiContractNamesPassed,
            routeContractNamesPassed,
            routeWaypointSnapshotPassed,
            descriptorRebuildPassed,
            builderPlanSnapshotPassed,
            overlayPlanSnapshotPassed,
            metaKeySnapshotPassed,
            anchorNodeConstantsPassed,
            points.Length,
            routes.Length,
            reason);
    }
    catch (Exception ex)
    {
        return new TerrainAnchorContractSmokeReport(
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            0,
            0,
            $"anchor contract smoke threw {ex.GetType().Name}: {ex.Message}");
    }
}

static bool RequiredKeysMatch(string[] actual, string[] expected)
{
    if (actual.Length != expected.Length)
    {
        return false;
    }

    for (int i = 0; i < expected.Length; i++)
    {
        if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal))
        {
            return false;
        }
    }

    return true;
}

static bool AnchorMetaKeySnapshotsAreIsolated()
{
    string[] pointKeys = TerrainWorldAnchorContract.GetPointOfInterestRequiredMetaKeys();
    string[] routeKeys = TerrainWorldAnchorContract.GetRouteRequiredMetaKeys();

    if (pointKeys.Length == 0 || routeKeys.Length == 0)
    {
        return false;
    }

    string originalPointKey = pointKeys[0];
    string originalRouteKey = routeKeys[0];
    pointKeys[0] = "__mutated_poi_meta_key__";
    routeKeys[0] = "__mutated_route_meta_key__";

    string[] secondPointRead = TerrainWorldAnchorContract.GetPointOfInterestRequiredMetaKeys();
    string[] secondRouteRead = TerrainWorldAnchorContract.GetRouteRequiredMetaKeys();
    return
        secondPointRead.Length == pointKeys.Length &&
        secondRouteRead.Length == routeKeys.Length &&
        string.Equals(secondPointRead[0], originalPointKey, StringComparison.Ordinal) &&
        string.Equals(secondRouteRead[0], originalRouteKey, StringComparison.Ordinal);
}

static bool AnchorNodeConstantsMatchContract()
{
    return
        string.Equals(TerrainWorldPointOfInterestAnchor.GroupName, TerrainWorldAnchorContract.PointOfInterestGroup, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyId, TerrainWorldAnchorContract.PointOfInterestMetaKeyId, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyKind, TerrainWorldAnchorContract.PointOfInterestMetaKeyKind, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyVisual, TerrainWorldAnchorContract.PointOfInterestMetaKeyVisual, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyGameplayTag, TerrainWorldAnchorContract.PointOfInterestMetaKeyGameplayTag, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyScore, TerrainWorldAnchorContract.PointOfInterestMetaKeyScore, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyScenic, TerrainWorldAnchorContract.PointOfInterestMetaKeyScenic, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyTraversability, TerrainWorldAnchorContract.PointOfInterestMetaKeyTraversability, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeySettlementTier, TerrainWorldAnchorContract.PointOfInterestMetaKeySettlementTier, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyLandscape, TerrainWorldAnchorContract.PointOfInterestMetaKeyLandscape, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyInteractionRadius, TerrainWorldAnchorContract.PointOfInterestMetaKeyInteractionRadius, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldPointOfInterestAnchor.MetaKeyEncounterBudget, TerrainWorldAnchorContract.PointOfInterestMetaKeyEncounterBudget, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldRouteAnchor.GroupName, TerrainWorldAnchorContract.RouteGroup, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldRouteAnchor.MetaKeyKind, TerrainWorldAnchorContract.RouteMetaKeyKind, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldRouteAnchor.MetaKeyFrom, TerrainWorldAnchorContract.RouteMetaKeyFrom, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldRouteAnchor.MetaKeyTo, TerrainWorldAnchorContract.RouteMetaKeyTo, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldRouteAnchor.MetaKeyCost, TerrainWorldAnchorContract.RouteMetaKeyCost, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldRouteAnchor.MetaKeyScenic, TerrainWorldAnchorContract.RouteMetaKeyScenic, StringComparison.Ordinal) &&
        string.Equals(TerrainWorldRouteAnchor.MetaKeyTraversability, TerrainWorldAnchorContract.RouteMetaKeyTraversability, StringComparison.Ordinal);
}

static bool PointDescriptorsHaveRequiredContract(
    TerrainWorldPointOfInterestAnchorDescriptor[] points,
    TerrainWorldPlan plan)
{
    if (points.Length == 0)
    {
        return false;
    }

    for (int i = 0; i < points.Length; i++)
    {
        TerrainWorldPointOfInterestAnchorDescriptor point = points[i];
        TerrainWorldPointOfInterest source = plan.PointsOfInterest[i];
        TerrainPointOfInterestArchetype archetype = TerrainPointOfInterestArchetypeCatalog.Get(source.Kind);
        if (!string.Equals(point.GroupName, TerrainWorldAnchorContract.PointOfInterestGroup, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(point.GameplayTagGroup) ||
            !string.Equals(point.GameplayTagGroup, point.GameplayTag, StringComparison.Ordinal) ||
            point.Id != source.Id ||
            point.Kind != source.Kind ||
            !ExactPositionEquals(point.WorldPosition2D, source.WorldPosition) ||
            !string.Equals(point.Name, $"POI_{source.Id:00}_{source.Kind}", StringComparison.Ordinal) ||
            !string.Equals(point.GameplayTag, archetype.GameplayTag, StringComparison.Ordinal) ||
            !ExactFloatEquals(point.InteractionRadius, archetype.InteractionRadius) ||
            point.EncounterBudget != archetype.EncounterBudget ||
            point.VisualKind != TerrainPointOfInterestArchetypeCatalog.VisualKindFor(source))
        {
            return false;
        }
    }

    return true;
}

static bool RouteDescriptorsHaveRequiredContract(
    TerrainWorldRouteAnchorDescriptor[] routes,
    TerrainWorldPlan plan)
{
    if (routes.Length == 0)
    {
        return false;
    }

    for (int i = 0; i < routes.Length; i++)
    {
        TerrainWorldRouteAnchorDescriptor route = routes[i];
        TerrainWorldRoute source = plan.Routes[i];
        if (!string.Equals(route.GroupName, TerrainWorldAnchorContract.RouteGroup, StringComparison.Ordinal) ||
            route.FromPointId != source.FromPointId ||
            route.ToPointId != source.ToPointId ||
            route.Kind != source.Kind ||
            !ExactFloatEquals(route.Cost, source.Cost) ||
            !ExactFloatEquals(route.AverageScenicPotential, source.AverageScenicPotential) ||
            !ExactFloatEquals(route.AverageTraversability, source.AverageTraversability) ||
            !string.Equals(route.Name, $"Route_{source.FromPointId:00}_{source.ToPointId:00}_{source.Kind}", StringComparison.Ordinal) ||
            route.WaypointCount != source.Waypoints.Length)
        {
            return false;
        }

        Vector2 expectedMidpoint = source.Waypoints.Length == 0
            ? Vector2.Zero
            : source.Waypoints[source.Waypoints.Length / 2];
        if (!ExactPositionEquals(route.WorldMidpoint2D, expectedMidpoint))
        {
            return false;
        }
    }

    return true;
}

static bool RouteDescriptorWaypointsAreIsolated(
    TerrainWorldRouteAnchorDescriptor[] routes,
    TerrainWorldPlan plan)
{
    if (routes.Length == 0 || plan.Routes.Length == 0)
    {
        return false;
    }

    TerrainWorldRouteAnchorDescriptor routeDescriptor = routes[0];
    TerrainWorldRoute route = plan.Routes[0];
    if (routeDescriptor.WaypointCount != route.Waypoints.Length)
    {
        return false;
    }

    if (routeDescriptor.WaypointCount == 0)
    {
        return true;
    }

    Vector2 originalWaypoint = route.Waypoints[0];
    Vector2[] descriptorSnapshot = routeDescriptor.Waypoints;
    descriptorSnapshot[0] = originalWaypoint + new Vector2(321.0f, -321.0f);
    bool descriptorSnapshotIsolated =
        ExactPositionEquals(route.Waypoints[0], originalWaypoint) &&
        ExactPositionEquals(routeDescriptor.GetWaypoint(0), originalWaypoint);

    Vector2[] constructorInput = route.Waypoints.Length == 0
        ? []
        : (Vector2[])route.Waypoints.Clone();
    TerrainWorldRouteAnchorDescriptor constructedDescriptor = new(
        Name: routeDescriptor.Name,
        GroupName: routeDescriptor.GroupName,
        FromPointId: routeDescriptor.FromPointId,
        ToPointId: routeDescriptor.ToPointId,
        Kind: routeDescriptor.Kind,
        Cost: routeDescriptor.Cost,
        AverageScenicPotential: routeDescriptor.AverageScenicPotential,
        AverageTraversability: routeDescriptor.AverageTraversability,
        WorldMidpoint2D: routeDescriptor.WorldMidpoint2D,
        Waypoints: constructorInput);
    constructorInput[0] = originalWaypoint + new Vector2(-654.0f, 654.0f);
    bool descriptorConstructorInputIsolated = ExactPositionEquals(constructedDescriptor.GetWaypoint(0), originalWaypoint);

    Vector2[] anchorSnapshot = TerrainWorldRouteAnchor.CreateWaypointSnapshot(routeDescriptor);
    anchorSnapshot[0] = originalWaypoint + new Vector2(777.0f, -777.0f);
    bool routeAnchorSnapshotIsolated =
        anchorSnapshot.Length == route.Waypoints.Length &&
        ExactPositionEquals(TerrainWorldRouteAnchor.CreateWaypointSnapshot(routeDescriptor)[0], originalWaypoint);

    return descriptorSnapshotIsolated &&
        descriptorConstructorInputIsolated &&
        routeAnchorSnapshotIsolated;
}

static bool ValidateAnchorDescriptorRebuild(
    TerrainWorldPlan plan,
    TerrainWorldPointOfInterestAnchorDescriptor[] points,
    TerrainWorldRouteAnchorDescriptor[] routes)
{
    TerrainWorldPointOfInterestAnchorDescriptor[] secondPoints =
        TerrainWorldAnchorContract.CreatePointOfInterestDescriptors(plan);
    TerrainWorldRouteAnchorDescriptor[] secondRoutes = TerrainWorldAnchorContract.CreateRouteDescriptors(plan);
    if (secondPoints.Length != points.Length || secondRoutes.Length != routes.Length)
    {
        return false;
    }

    for (int i = 0; i < points.Length; i++)
    {
        if (!points[i].Equals(secondPoints[i]))
        {
            return false;
        }
    }

    for (int i = 0; i < routes.Length; i++)
    {
        if (!RoutesMatch(routes[i], secondRoutes[i]))
        {
            return false;
        }
    }

    return true;
}

static bool AnchorBuilderPlanSnapshotIsolated(TerrainWorldPlan plan)
{
    TerrainWorldPlan assignedPlan = TerrainWorldPlan.CopyOf(plan);
    var builder = (TerrainWorldAnchorBuilder)RuntimeHelpers.GetUninitializedObject(typeof(TerrainWorldAnchorBuilder));
    SetPrivateField(builder, "_plan", TerrainWorldPlan.CopyOf(assignedPlan));
    return PlanSnapshotIsolated(plan, assignedPlan, builder.Plan, () => builder.Plan);
}

static bool PlanOverlaySnapshotIsolated(TerrainWorldPlan plan)
{
    TerrainWorldPlan assignedPlan = TerrainWorldPlan.CopyOf(plan);
    var overlay = (TerrainWorldPlanOverlay)RuntimeHelpers.GetUninitializedObject(typeof(TerrainWorldPlanOverlay));
    SetPrivateField(overlay, "_plan", TerrainWorldPlan.CopyOf(assignedPlan));
    return PlanSnapshotIsolated(plan, assignedPlan, overlay.Plan, () => overlay.Plan);
}

static bool PlanSnapshotIsolated(
    TerrainWorldPlan plan,
    TerrainWorldPlan assignedPlan,
    TerrainWorldPlan? snapshot,
    Func<TerrainWorldPlan?> readSnapshot)
{
    if (snapshot is null ||
        ReferenceEquals(snapshot, assignedPlan) ||
        snapshot.PointsOfInterest.Length != plan.PointsOfInterest.Length ||
        snapshot.Routes.Length != plan.Routes.Length ||
        snapshot.Regions.Length != plan.Regions.Length)
    {
        return false;
    }

    TerrainWorldRegion? originalRegion = plan.Regions.Length == 0 ? null : plan.Regions[0];
    TerrainWorldPointOfInterest? originalPoint = plan.PointsOfInterest.Length == 0 ? null : plan.PointsOfInterest[0];
    TerrainWorldRoute? originalRoute = plan.Routes.Length == 0 ? null : plan.Routes[0];
    Vector2? originalWaypoint = originalRoute?.Waypoints.Length > 0 ? originalRoute.Value.Waypoints[0] : null;

    if (snapshot.Regions.Length > 0)
    {
        snapshot.Regions[0] = snapshot.Regions[0] with { Height = snapshot.Regions[0].Height + 54321.0f };
    }

    if (snapshot.PointsOfInterest.Length > 0)
    {
        snapshot.PointsOfInterest[0] = snapshot.PointsOfInterest[0] with { Id = snapshot.PointsOfInterest[0].Id + 100000 };
    }

    if (snapshot.Routes.Length > 0)
    {
        if (snapshot.Routes[0].Waypoints.Length > 0)
        {
            snapshot.Routes[0].Waypoints[0] += new Vector2(1111.0f, -1111.0f);
        }

        snapshot.Routes[0] = snapshot.Routes[0] with { FromPointId = snapshot.Routes[0].FromPointId + 100000 };
    }

    if (assignedPlan.Regions.Length > 0)
    {
        assignedPlan.Regions[0] = assignedPlan.Regions[0] with { Height = assignedPlan.Regions[0].Height - 43210.0f };
    }

    if (assignedPlan.PointsOfInterest.Length > 0)
    {
        assignedPlan.PointsOfInterest[0] = assignedPlan.PointsOfInterest[0] with { Id = assignedPlan.PointsOfInterest[0].Id - 100000 };
    }

    if (assignedPlan.Routes.Length > 0)
    {
        if (assignedPlan.Routes[0].Waypoints.Length > 0)
        {
            assignedPlan.Routes[0].Waypoints[0] += new Vector2(-2222.0f, 2222.0f);
        }

        assignedPlan.Routes[0] = assignedPlan.Routes[0] with { ToPointId = assignedPlan.Routes[0].ToPointId - 100000 };
    }

    TerrainWorldPlan? secondSnapshot = readSnapshot();
    if (secondSnapshot is null ||
        secondSnapshot.PointsOfInterest.Length != plan.PointsOfInterest.Length ||
        secondSnapshot.Routes.Length != plan.Routes.Length ||
        secondSnapshot.Regions.Length != plan.Regions.Length)
    {
        return false;
    }

    bool regionIsolated = originalRegion is null ||
        (secondSnapshot.Regions.Length > 0 &&
            secondSnapshot.Regions[0].GridX == originalRegion.Value.GridX &&
            secondSnapshot.Regions[0].GridY == originalRegion.Value.GridY &&
            ExactFloatEquals(secondSnapshot.Regions[0].Height, originalRegion.Value.Height));
    bool pointIsolated = originalPoint is null ||
        (secondSnapshot.PointsOfInterest.Length > 0 &&
            secondSnapshot.PointsOfInterest[0].Id == originalPoint.Value.Id &&
            secondSnapshot.PointsOfInterest[0].Kind == originalPoint.Value.Kind);
    bool routeIsolated = originalRoute is null ||
        (secondSnapshot.Routes.Length > 0 &&
            secondSnapshot.Routes[0].FromPointId == originalRoute.Value.FromPointId &&
            secondSnapshot.Routes[0].ToPointId == originalRoute.Value.ToPointId &&
            secondSnapshot.Routes[0].Waypoints.Length == originalRoute.Value.Waypoints.Length);
    bool waypointIsolated = originalWaypoint is null ||
        (secondSnapshot.Routes.Length > 0 &&
            secondSnapshot.Routes[0].Waypoints.Length > 0 &&
            ExactPositionEquals(secondSnapshot.Routes[0].Waypoints[0], originalWaypoint.Value));

    return regionIsolated && pointIsolated && routeIsolated && waypointIsolated;
}

static bool RoutesMatch(TerrainWorldRouteAnchorDescriptor a, TerrainWorldRouteAnchorDescriptor b)
{
    if (a.FromPointId != b.FromPointId ||
        a.ToPointId != b.ToPointId ||
        a.Kind != b.Kind ||
        a.WaypointCount != b.WaypointCount ||
        !string.Equals(a.Name, b.Name, StringComparison.Ordinal) ||
        !string.Equals(a.GroupName, b.GroupName, StringComparison.Ordinal))
    {
        return false;
    }

    for (int i = 0; i < a.WaypointCount; i++)
    {
        if (!ExactPositionEquals(a.GetWaypoint(i), b.GetWaypoint(i)))
        {
            return false;
        }
    }

    return true;
}

static string AnchorContractFailureReason(
    bool poiContractNamesPassed,
    bool routeContractNamesPassed,
    bool anchorNodeConstantsPassed,
    bool pointCountPassed,
    bool routeCountPassed,
    bool poiGroupMetaPassed,
    bool routeGroupMetaPassed,
    bool routeWaypointSnapshotPassed,
    bool descriptorRebuildPassed,
    bool builderPlanSnapshotPassed,
    bool overlayPlanSnapshotPassed,
    bool metaKeySnapshotPassed,
    int pointCount,
    int expectedPointCount,
    int routeCount,
    int expectedRouteCount)
{
    if (!poiContractNamesPassed)
    {
        return "POI anchor group or required meta key contract names drifted";
    }

    if (!routeContractNamesPassed)
    {
        return "route anchor group or required meta key contract names drifted";
    }

    if (!anchorNodeConstantsPassed)
    {
        return "anchor node constants drifted away from TerrainWorldAnchorContract";
    }

    if (!pointCountPassed || !routeCountPassed)
    {
        return $"anchor builder count mismatch (POIs {pointCount}/{expectedPointCount}, routes {routeCount}/{expectedRouteCount})";
    }

    if (!poiGroupMetaPassed)
    {
        return "POI anchor descriptors missed required group or meta contract values";
    }

    if (!routeGroupMetaPassed)
    {
        return "route anchor descriptors missed required group or meta contract values";
    }

    if (!routeWaypointSnapshotPassed)
    {
        return "route anchor descriptors exposed plan waypoint array state";
    }

    if (!descriptorRebuildPassed)
    {
        return "anchor descriptors were not stable across repeated construction";
    }

    if (!builderPlanSnapshotPassed)
    {
        return "TerrainWorldAnchorBuilder.Plan exposed mutable plan array state";
    }

    if (!overlayPlanSnapshotPassed)
    {
        return "TerrainWorldPlanOverlay.Plan exposed mutable plan array state";
    }

    if (!metaKeySnapshotPassed)
    {
        return "TerrainWorldAnchorContract required meta key arrays exposed mutable static state";
    }

    return "terrain anchor contract failed";
}

static void PrintAnchorContractSmoke(TerrainAnchorContractSmokeReport report)
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

static TerrainRuntimeWorldSmokeReport ValidateRuntimeWorldPlanMaterialization(
    TerrainGenerationProfile profile,
    float worldSize)
{
    TerrainWorldPlan syncPlan = TerrainWorld.CreateRuntimeOpenWorldPlan(profile, worldSize);
    var asyncPlanWatch = Stopwatch.StartNew();
    TerrainWorldPlan plan = TerrainWorld.CreateRuntimeOpenWorldPlanAsync(profile, worldSize).GetAwaiter().GetResult();
    asyncPlanWatch.Stop();
    bool asyncPlanMatchesSync = RuntimePlansMatch(syncPlan, plan);
    TerrainRuntimeWorldCancellationReport cancellationReport = ValidateRuntimeWorldPlanCancellation(profile, worldSize);
    TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
    TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
    TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
    TerrainPointOfInterestArchetypeValidationReport archetypeGate = TerrainPointOfInterestArchetypeCatalog.ValidatePlanReadiness(plan);
    bool setWorldPlanInvalidationPassed = ValidateRuntimeSetWorldPlanInvalidation(profile, plan);
    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);

    var coords = new HashSet<TerrainTileCoord>();
    AddRouteScatterCandidateCoords(plan, profile, coords, maxCoords: 48);
    foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
    {
        AddPoiFootprintCoords(coords, point, profile);
    }

    var materializedPoiNames = new HashSet<string>(StringComparer.Ordinal);
    Span<int> scatterLandmarkCounts = stackalloc int[Enum.GetValues<TerrainLandmarkKind>().Length];
    int sampledTiles = 0;
    int roadMarkerCount = 0;
    int bridgeSpanCount = 0;

    foreach (TerrainTileCoord coord in coords)
    {
        TerrainTileData data = TerrainTileBuilder.Build(
            coord,
            lod: 0,
            profile,
            includeCollision: false,
            corridorIndex,
            poiIndex);
        sampledTiles++;

        foreach (TerrainLandmarkData landmark in data.Landmarks)
        {
            if (landmark.DebugName.StartsWith("POI_", StringComparison.Ordinal))
            {
                materializedPoiNames.Add(landmark.DebugName);
            }
        }

        foreach (TerrainScatterInstance scatter in data.ScatterInstances)
        {
            if (scatter.Kind != TerrainScatterKind.Landmark)
            {
                continue;
            }

            int kindIndex = Mathf.Clamp((int)scatter.LandmarkKind, 0, scatterLandmarkCounts.Length - 1);
            scatterLandmarkCounts[kindIndex]++;
            if (scatter.LandmarkKind == TerrainLandmarkKind.RoadMarker)
            {
                roadMarkerCount++;
            }
            else if (scatter.LandmarkKind == TerrainLandmarkKind.BridgeSpan)
            {
                bridgeSpanCount++;
            }
        }
    }

    int settlementInteriorScatterCount = SettlementInteriorScatterCount(scatterLandmarkCounts);
    int routeLandmarkCount = roadMarkerCount + bridgeSpanCount;
    bool passed =
        asyncPlanMatchesSync &&
        cancellationReport.Passed &&
        qualityGate.Passed &&
        planningGate.Passed &&
        experienceGate.Passed &&
        archetypeGate.Passed &&
        setWorldPlanInvalidationPassed &&
        corridorIndex.HasSegments &&
        poiIndex.HasPoints &&
        sampledTiles > 0 &&
        materializedPoiNames.Count == plan.PointsOfInterest.Length &&
        roadMarkerCount >= 8 &&
        bridgeSpanCount > 0 &&
        routeLandmarkCount >= 12 &&
        settlementInteriorScatterCount >= 24;
    string reason = passed
        ? "runtime TerrainWorld plan entry generated indexed routes/POIs that materialized on tiles"
        : RuntimeWorldFailureReason(
            asyncPlanMatchesSync,
            cancellationReport.Passed,
            qualityGate,
            planningGate,
            experienceGate,
            archetypeGate,
            setWorldPlanInvalidationPassed,
            corridorIndex.HasSegments,
            poiIndex.HasPoints,
            sampledTiles,
            materializedPoiNames.Count,
            plan.PointsOfInterest.Length,
            roadMarkerCount,
            bridgeSpanCount,
            settlementInteriorScatterCount);

    return new TerrainRuntimeWorldSmokeReport(
        passed,
        plan.PointsOfInterest.Length,
        plan.Routes.Length,
        sampledTiles,
        materializedPoiNames.Count,
        roadMarkerCount,
        bridgeSpanCount,
        settlementInteriorScatterCount,
        asyncPlanWatch.Elapsed.TotalMilliseconds,
        asyncPlanMatchesSync,
        cancellationReport.ElapsedMilliseconds,
        cancellationReport.Passed,
        corridorIndex.HasSegments,
        poiIndex.HasPoints,
        qualityGate.Passed,
        planningGate.Passed,
        experienceGate.Passed,
        archetypeGate.Passed,
        setWorldPlanInvalidationPassed,
        reason);
}

static bool ValidateRuntimeSetWorldPlanInvalidation(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan)
{
    TerrainGenerationProfile probeProfile = profile with
    {
        MaxQueuedTileJobs = 0,
        StreamRadiusChunks = 1
    };
    TerrainRouteCorridorIndex routeIndex = TerrainRouteCorridorIndex.FromPlan(plan, probeProfile);
    TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, probeProfile);
    int terrainFeatureKey = HashCode.Combine(routeIndex.CacheKey, poiIndex.CacheKey);
    TerrainTileCoord coord = new(12, -7);

    TerrainWorld world = CreateTerrainWorldFacadeProbe(probeProfile, plan);
    SetPrivateField(world, "_routeCorridors", routeIndex);
    SetPrivateField(world, "_pointOfInterestIndex", poiIndex);
    SetPrivateField(world, "_desiredCoords", new HashSet<TerrainTileCoord> { coord });

    SetPrivateField(
        world,
        "_chunks",
        new Dictionary<TerrainTileCoord, TerrainChunk>
        {
            [coord] = null!
        });
    SetPrivateField(world, "_jobs", CreatePendingTileJobStateDictionary([coord], probeProfile, terrainFeatureKey));
    SetPrivateField(world, "_retiredJobs", CreatePendingTileJobList());
    SetPrivateField(world, "_tileCache", CreateTileCacheDictionary(coord, probeProfile, terrainFeatureKey));
    SetPrivateField(world, "_tileCacheNodes", CreateTileCacheNodeDictionary());
    SetPrivateField(world, "_tileCacheLru", CreateTileCacheLinkedList());

    TerrainWorldStreamingSnapshot seededSnapshot = world.GetStreamingSnapshot();
    if (!seededSnapshot.HasWorldPlan ||
        seededSnapshot.LoadedChunkCount != 1 ||
        seededSnapshot.QueuedTileJobCount != 1 ||
        seededSnapshot.TileCacheCount != 1)
    {
        return false;
    }

    InvokePrivateMethod(world, "InvalidatePlanDependentStreamingState");
    SetPrivateField(world, "_worldPlan", null as TerrainWorldPlan);
    SetPrivateField(world, "_routeCorridors", TerrainRouteCorridorIndex.Empty);
    SetPrivateField(world, "_pointOfInterestIndex", TerrainPointOfInterestIndex.Empty);
    TerrainWorldStreamingSnapshot clearedSnapshot = world.GetStreamingSnapshot();
    return !world.TryGetWorldPlan(out _) &&
        !clearedSnapshot.HasWorldPlan &&
        clearedSnapshot.LoadedChunkCount == 0 &&
        clearedSnapshot.QueuedTileJobCount == 0 &&
        clearedSnapshot.TileCacheCount == 0 &&
        GetPrivateCollectionCount(world, "_jobs") == 0 &&
        GetPrivateCollectionCount(world, "_retiredJobs") == 1 &&
        GetPrivateCollectionCount(world, "_tileCache") == 0 &&
        GetPrivateCollectionCount(world, "_tileCacheNodes") == 0 &&
        GetPrivateCollectionCount(world, "_tileCacheLru") == 0 &&
        GetPrivateCollectionCount(world, "_chunks") == 0;
}

static TerrainRuntimeWorldCancellationReport ValidateRuntimeWorldPlanCancellation(
    TerrainGenerationProfile profile,
    float worldSize)
{
    TerrainGenerationProfile cancellationProfile = profile with
    {
        StreamRadiusChunks = Math.Max(profile.StreamRadiusChunks, 10)
    };
    float cancellationWorldSize = Mathf.Max(worldSize * 2.0f, cancellationProfile.ChunkSize * 128.0f);
    using var cancellation = new CancellationTokenSource();
    var watch = Stopwatch.StartNew();
    Task<TerrainWorldPlan> task = TerrainWorld.CreateRuntimeOpenWorldPlanAsync(
        cancellationProfile,
        cancellationWorldSize,
        cancellation.Token);
    cancellation.CancelAfter(TimeSpan.FromMilliseconds(12));

    try
    {
        _ = task.GetAwaiter().GetResult();
        watch.Stop();
        return new TerrainRuntimeWorldCancellationReport(false, watch.Elapsed.TotalMilliseconds);
    }
    catch (OperationCanceledException)
    {
        watch.Stop();
        return new TerrainRuntimeWorldCancellationReport(task.IsCanceled, watch.Elapsed.TotalMilliseconds);
    }
    catch
    {
        watch.Stop();
        return new TerrainRuntimeWorldCancellationReport(false, watch.Elapsed.TotalMilliseconds);
    }
}

static bool RuntimePlansMatch(TerrainWorldPlan expected, TerrainWorldPlan actual)
{
    if (!Mathf.IsEqualApprox(expected.WorldSize, actual.WorldSize) ||
        expected.GridResolution != actual.GridResolution ||
        expected.PointsOfInterest.Length != actual.PointsOfInterest.Length ||
        expected.Routes.Length != actual.Routes.Length)
    {
        return false;
    }

    TerrainWorldPlanningReport expectedPlanning = expected.PlanningReport;
    TerrainWorldPlanningReport actualPlanning = actual.PlanningReport;
    if (expectedPlanning.PointOfInterestCount != actualPlanning.PointOfInterestCount ||
        expectedPlanning.RouteCount != actualPlanning.RouteCount ||
        expectedPlanning.VillageCount != actualPlanning.VillageCount ||
        expectedPlanning.TownCount != actualPlanning.TownCount ||
        expectedPlanning.OasisHubCount != actualPlanning.OasisHubCount)
    {
        return false;
    }

    for (int i = 0; i < expected.PointsOfInterest.Length; i++)
    {
        TerrainWorldPointOfInterest a = expected.PointsOfInterest[i];
        TerrainWorldPointOfInterest b = actual.PointsOfInterest[i];
        if (a.Id != b.Id ||
            a.Kind != b.Kind ||
            a.SettlementTier != b.SettlementTier ||
            !ContractPositionEquals(a.WorldPosition, b.WorldPosition) ||
            !ExactFloatEquals(a.Score, b.Score))
        {
            return false;
        }
    }

    for (int i = 0; i < expected.Routes.Length; i++)
    {
        TerrainWorldRoute a = expected.Routes[i];
        TerrainWorldRoute b = actual.Routes[i];
        if (a.FromPointId != b.FromPointId ||
            a.ToPointId != b.ToPointId ||
            a.Kind != b.Kind ||
            a.Waypoints.Length != b.Waypoints.Length)
        {
            return false;
        }
    }

    return true;
}

static string RuntimeWorldFailureReason(
    bool asyncPlanMatchesSync,
    bool asyncPlanCancellationPassed,
    TerrainQualityGateResult qualityGate,
    TerrainWorldPlanningGateResult planningGate,
    TerrainExperienceGateResult experienceGate,
    TerrainPointOfInterestArchetypeValidationReport archetypeGate,
    bool setWorldPlanInvalidationPassed,
    bool hasCorridorIndex,
    bool hasPointIndex,
    int sampledTiles,
    int materializedPoiCount,
    int expectedPoiCount,
    int roadMarkerCount,
    int bridgeSpanCount,
    int settlementInteriorScatterCount)
{
    if (!asyncPlanMatchesSync)
    {
        return "async runtime open world plan did not match the synchronous runtime plan";
    }

    if (!asyncPlanCancellationPassed)
    {
        return "async runtime open world plan did not honor cancellation";
    }

    if (!qualityGate.Passed || !planningGate.Passed || !experienceGate.Passed || !archetypeGate.Passed)
    {
        return "runtime open world plan failed readiness gates";
    }

    if (!setWorldPlanInvalidationPassed)
    {
        return "TerrainWorld.SetWorldPlan did not invalidate ready-state jobs, cache, chunks, and plan indices";
    }

    if (!hasCorridorIndex || !hasPointIndex)
    {
        return "runtime open world plan did not build route corridor and POI indices";
    }

    if (sampledTiles == 0)
    {
        return "runtime open world plan produced no candidate tiles for materialization";
    }

    if (materializedPoiCount != expectedPoiCount)
    {
        return $"runtime POI materialization incomplete ({materializedPoiCount}/{expectedPoiCount})";
    }

    if (roadMarkerCount < 8 || bridgeSpanCount == 0)
    {
        return $"runtime route materialization incomplete (markers {roadMarkerCount}, bridges {bridgeSpanCount})";
    }

    if (settlementInteriorScatterCount < 24)
    {
        return $"runtime settlement interior scatter too sparse ({settlementInteriorScatterCount})";
    }

    return "runtime TerrainWorld plan materialization failed";
}

static void PrintRuntimeWorldSmoke(TerrainRuntimeWorldSmokeReport report)
{
    Console.WriteLine(
        $"Runtime TerrainWorld smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"POIs {report.MaterializedPointCount}/{report.PointOfInterestCount}, routes {report.RouteCount}, tiles {report.SampledTileCount}, " +
        $"async {report.AsyncPlanMilliseconds:0.0} ms/{(report.AsyncPlanMatchesSync ? "match" : "mismatch")}, " +
        $"cancel {report.AsyncPlanCancellationMilliseconds:0.0} ms/{(report.AsyncPlanCancellationPassed ? "pass" : "fail")}, " +
        $"indices route/POI {(report.HasCorridorIndex ? "yes" : "no")}/{(report.HasPointIndex ? "yes" : "no")}, " +
        $"markers/bridges {report.RoadMarkerCount}/{report.BridgeSpanCount}, settlement scatter {report.SettlementInteriorScatterCount}, " +
        $"gates Q/P/E/A {(report.QualityGatePassed ? "pass" : "fail")}/{(report.PlanningGatePassed ? "pass" : "fail")}/{(report.ExperienceGatePassed ? "pass" : "fail")}/{(report.ArchetypeGatePassed ? "pass" : "fail")}, " +
        $"set-plan invalidation {(report.SetWorldPlanInvalidationPassed ? "pass" : "fail")} " +
        $"({report.Reason})");
}

static int CountPositive(params int[] values)
{
    int count = 0;
    foreach (int value in values)
    {
        if (value > 0)
        {
            count++;
        }
    }

    return count;
}

static TerrainNativeSamplerSmokeReport ValidateNativeSamplerParity(TerrainGenerationProfile profile)
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

static void AccumulateFieldDelta(
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

static void PrintNativeSamplerSmoke(TerrainNativeSamplerSmokeReport report)
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

static TerrainTileBenchmarkReport BenchmarkTerrainTiles(
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

static void MeasureStableTileBuildPasses(
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

static TerrainTileBenchmarkPass BestTileBenchmarkPass(
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

static bool EvaluateTileBenchmark(
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

static bool TileBenchmarkPercentilesWithinThresholds(
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

static int RequiredBenchmarkCoverage(int threshold, int measuredTileCount, int tilesPerRequirement)
{
    if (measuredTileCount <= 0)
    {
        return threshold;
    }

    int scaled = Math.Max(1, measuredTileCount / Math.Max(1, tilesPerRequirement));
    return Math.Min(threshold, scaled);
}

static TerrainTileBenchmarkPass MeasureTileBuildPass(
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

static double TicksToMilliseconds(long ticks)
{
    return ticks * 1000.0 / Stopwatch.Frequency;
}

static double Percentile(double[] values, double percentile)
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

static void MeasureBenchmarkTileParity(
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

static TerrainTileCoord[] SelectBenchmarkTileCoords(
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

static TerrainTileBenchmarkCoverage AnalyzeBenchmarkTileCoverage(
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

static List<GameplayScatterRegionCandidate>[] CreateBenchmarkGroupBuckets<TEnum>()
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

static void AddBestBenchmarkGroupCoords(
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

static void AddSortedBenchmarkCoords(
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

static float BenchmarkRegionStressScore(TerrainWorldRegion region)
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

static bool IsBenchmarkGameplayRich(TerrainWorldField field)
{
    return field.EncounterPotential >= 0.52f ||
        field.ResourcePotential >= 0.50f ||
        field.HazardPotential >= 0.42f ||
        field.ScenicPotential >= 0.62f;
}

static bool TryAddBenchmarkCoord(
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

static TerrainTileCoord WorldToCoord(Vector2 world, TerrainGenerationProfile profile)
{
    return new TerrainTileCoord(
        Mathf.FloorToInt(world.X / profile.ChunkSize),
        Mathf.FloorToInt(world.Y / profile.ChunkSize));
}

static void PrintTileBenchmark(TerrainTileBenchmarkReport report)
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

static void PrintTileBenchmarkPass(string label, TerrainTileBenchmarkPass pass)
{
    Console.WriteLine(
        $"{label} tile build: {pass.TileCount} tiles in {pass.ElapsedMilliseconds:0.0} ms, " +
        $"{pass.TilesPerSecond:0.0} tiles/s, {pass.MillisecondsPerTile:0.00} ms/tile, " +
        $"p50/p95/p99 {pass.P50Milliseconds:0.00}/{pass.P95Milliseconds:0.00}/{pass.P99Milliseconds:0.00} ms, " +
        $"alloc {pass.AllocatedMegabytes:0.00} MB ({pass.AllocatedKilobytesPerTile:0.0} KB/tile), " +
        $"vertices {pass.TotalVertices}, scatter {pass.TotalScatter}, landmarks {pass.TotalLandmarks}");
}

static TerrainValidationCliContractSmokeReport ValidateValidationCliContract()
{
    bool tierSelectionPassed =
        ParseValidationTier(["--validation-tier", "pr"], out string prError).Name == "pr" &&
        string.IsNullOrEmpty(prError) &&
        ParseValidationTier(["--validation-tier", "nightly"], out string nightlyError).Name == "nightly" &&
        string.IsNullOrEmpty(nightlyError) &&
        ParseValidationTier(["--validation-tier", "release"], out string releaseError).Name == "release" &&
        string.IsNullOrEmpty(releaseError);
    bool fixedTierConfigurationPassed =
        TierMatches(
            TerrainValidationTierSpec.Pr,
            "pr",
            seedCount: 1,
            smokeAllSeeds: false,
            nativeSmoke: false,
            benchmarkTiles: false,
            benchmarkTileCount: 48) &&
        TierMatches(
            TerrainValidationTierSpec.Nightly,
            "nightly",
            seedCount: 10,
            smokeAllSeeds: true,
            nativeSmoke: false,
            benchmarkTiles: false,
            benchmarkTileCount: 48) &&
        TierMatches(
            TerrainValidationTierSpec.Release,
            "release",
            seedCount: 25,
            smokeAllSeeds: true,
            nativeSmoke: true,
            benchmarkTiles: true,
            benchmarkTileCount: 48);

    TerrainValidationTierSpec custom = ParseValidationTier([], out string customError);
    bool customFallbackPassed = custom.IsCustom && string.IsNullOrEmpty(customError);

    TerrainValidationTierSpec skipRejected = ParseValidationTier(
        ["--validation-tier", "pr", "--skip-runtime-api-smoke"],
        out string skipError);
    bool skipOverrideRejected =
        skipRejected.IsCustom &&
        skipError.Contains("--skip-*", StringComparison.Ordinal);

    TerrainValidationTierSpec seedRejected = ParseValidationTier(
        ["--validation-tier", "nightly", "--seed", "1234"],
        out string seedError);
    bool seedOverrideRejected =
        seedRejected.IsCustom &&
        seedError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

    TerrainValidationTierSpec worldRejected = ParseValidationTier(
        ["--validation-tier", "release", "--world-size", "4096"],
        out string worldError);
    bool worldOverrideRejected =
        worldRejected.IsCustom &&
        worldError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

    TerrainValidationTierSpec nativeRejected = ParseValidationTier(
        ["--validation-tier", "pr", "--native-smoke"],
        out string nativeError);
    bool nativeOverrideRejected =
        nativeRejected.IsCustom &&
        nativeError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

    TerrainValidationTierSpec smokeAllSeedsRejected = ParseValidationTier(
        ["--validation-tier", "pr", "--smoke-all-seeds"],
        out string smokeAllSeedsError);
    bool smokeAllSeedsOverrideRejected =
        smokeAllSeedsRejected.IsCustom &&
        smokeAllSeedsError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

    TerrainValidationTierSpec benchmarkRejected = ParseValidationTier(
        ["--validation-tier", "release", "--benchmark-tiles"],
        out string benchmarkError);
    bool benchmarkOverrideRejected =
        benchmarkRejected.IsCustom &&
        benchmarkError.Contains("seed/world/smoke/native/benchmark", StringComparison.Ordinal);

    TerrainValidationTierSpec unknownRejected = ParseValidationTier(
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

static bool TierMatches(
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

static void PrintValidationCliContractSmoke(TerrainValidationCliContractSmokeReport report)
{
    Console.WriteLine(
        $"Validation CLI contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"tiers/fixed/custom {report.TierSelectionPassed}/{report.FixedTierConfigurationPassed}/{report.CustomFallbackPassed}, " +
        $"reject skip/seed/world/native/smoke-all/benchmark/unknown {report.SkipOverrideRejected}/{report.SeedOverrideRejected}/" +
        $"{report.WorldOverrideRejected}/{report.NativeOverrideRejected}/{report.SmokeAllSeedsOverrideRejected}/" +
        $"{report.BenchmarkOverrideRejected}/{report.UnknownTierRejected} " +
        $"({report.Reason})");
}

static TerrainThresholdContractSmokeReport ValidateTerrainDefaultThresholdContracts()
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

static string ThresholdContractFailureReason(
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

static void PrintThresholdContractSmoke(TerrainThresholdContractSmokeReport report)
{
    Console.WriteLine(
        $"Terrain threshold contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"planning/quality/experience/benchmark {report.PlanningThresholdsPassed}/{report.QualityThresholdsPassed}/{report.ExperienceThresholdsPassed}/{report.BenchmarkThresholdsPassed} " +
        $"({report.Reason})");
}

static TerrainDefaultStateContractSmokeReport ValidateTerrainDefaultStateContracts()
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

static string DefaultStateContractFailureReason(
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

static void PrintDefaultStateContractSmoke(TerrainDefaultStateContractSmokeReport report)
{
    Console.WriteLine(
        $"Terrain default state contract smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"corridor-none/index-empty/poi-empty/water-empty/plan-empty " +
        $"{report.CorridorNonePassed}/{report.CorridorIndexEmptyPassed}/{report.PointOfInterestIndexEmptyPassed}/" +
        $"{report.WaterSurfaceEmptyPassed}/{report.PlanSnapshotEmptyPassed} " +
        $"({report.Reason})");
}

static void PrintAggregate(
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

static TerrainGenerationProfile CreateDemoProfile()
{
    return new TerrainGenerationProfile(
        Seed: 613_061,
        ChunkSize: 192.0f,
        BaseResolution: 64,
        StreamRadiusChunks: 6,
        CollisionRadiusChunks: 2,
        MaxLod: 3,
        HeightScale: 820.0f,
        SeaLevel: -22.0f,
        ContinentScale: 5600.0f,
        MountainScale: 1720.0f,
        MountainWeight: 0.82f,
        ValleyWeight: 0.56f,
        DetailWeight: 0.16f,
        VistaFrequency: 0.62f,
        RiverStrength: 0.72f,
        RiverCarveDepth: 130.0f,
        TerraceStrength: 66.0f,
        SkirtDepth: 42.0f,
        MaxCompletedTilesPerFrame: 3,
        MaxQueuedTileJobs: 28,
        MaxCachedTileData: 96,
        GenerateCollision: true,
        UseNativeSamplerWhenAvailable: true);
}

static TerrainValidationTierSpec ParseValidationTier(string[] args, out string error)
{
    error = string.Empty;
    string? tier = GetArg(args, "--validation-tier");
    if (string.IsNullOrWhiteSpace(tier))
    {
        return TerrainValidationTierSpec.Custom;
    }

    if (HasAnyFlag(
        args,
        "--skip-corridor-smoke",
        "--skip-route-scatter-smoke",
        "--skip-poi-tile-smoke",
        "--skip-gameplay-scatter-smoke",
        "--skip-biome-scatter-smoke",
        "--skip-scenic-landmark-smoke",
        "--skip-artifact-smoke",
        "--skip-plan-json-smoke",
        "--skip-enum-contract-smoke",
        "--skip-runtime-api-smoke",
        "--skip-anchor-smoke",
        "--skip-runtime-world-smoke"))
    {
        error = "--validation-tier cannot be combined with --skip-* flags; tiers are fixed regression gates.";
        return TerrainValidationTierSpec.Custom;
    }

    if (HasAnyOption(
        args,
        "--seed",
        "--seed-count",
        "--seed-step",
        "--world-size",
        "--artifact-image-size",
        "--smoke-all-seeds",
        "--native-smoke",
        "--benchmark-tiles",
        "--benchmark-tile-count"))
    {
        error = "--validation-tier cannot be combined with seed/world/smoke/native/benchmark overrides; choose a tier or custom flags.";
        return TerrainValidationTierSpec.Custom;
    }

    return tier.ToLowerInvariant() switch
    {
        "pr" => TerrainValidationTierSpec.Pr,
        "nightly" => TerrainValidationTierSpec.Nightly,
        "release" => TerrainValidationTierSpec.Release,
        _ => FailUnknownTier(tier, out error)
    };
}

static TerrainValidationTierSpec FailUnknownTier(string tier, out string error)
{
    error = $"unknown --validation-tier '{tier}'. Valid tiers: pr, nightly, release.";
    return TerrainValidationTierSpec.Custom;
}

static void PrintValidationTier(
    TerrainValidationTierSpec tier,
    int seedCount,
    bool smokeAllSeeds,
    bool nativeSmoke,
    bool benchmarkTiles,
    int benchmarkTileCount)
{
    Console.WriteLine(
        $"Validation tier: {tier.Name} " +
        $"(seeds {seedCount}, smoke-all-seeds {smokeAllSeeds}, native-smoke {nativeSmoke}, " +
        $"benchmark-tiles {benchmarkTiles}, benchmark-tile-count {benchmarkTileCount})");
}

static int GetIntArg(string[] args, string name, int fallback)
{
    string? value = GetArg(args, name);
    return int.TryParse(value, out int parsed) ? parsed : fallback;
}

static float GetFloatArg(string[] args, string name, float fallback)
{
    string? value = GetArg(args, name);
    return float.TryParse(value, out float parsed) ? parsed : fallback;
}

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static bool HasFlag(string[] args, string name)
{
    foreach (string arg in args)
    {
        if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static bool HasAnyFlag(string[] args, params string[] names)
{
    foreach (string name in names)
    {
        if (HasFlag(args, name))
        {
            return true;
        }
    }

    return false;
}

static bool HasAnyOption(string[] args, params string[] names)
{
    foreach (string name in names)
    {
        if (HasFlag(args, name) || GetArg(args, name) is not null)
        {
            return true;
        }
    }

    return false;
}

static string DefaultArtifactOutputDirectory(int seed)
{
    return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dao_terrain_validation", $"seed_{seed}");
}

static string DefaultBatchArtifactOutputDirectory(int seed, int seedCount)
{
    return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dao_terrain_validation", $"batch_seed_{seed}_count_{seedCount}");
}

static string ArtifactOutputDirectoryForSeed(
    string baseDirectory,
    int seed,
    bool isolateBySeed)
{
    return isolateBySeed
        ? System.IO.Path.Combine(baseDirectory, $"seed_{seed}")
        : baseDirectory;
}

static string FileSystemPath(string path)
{
    if (path.Contains("://", StringComparison.Ordinal))
    {
        return ProjectSettings.GlobalizePath(path);
    }

    return System.IO.Path.GetFullPath(path);
}

static float ColorDistance(Color a, Color b)
{
    float dr = a.R - b.R;
    float dg = a.G - b.G;
    float db = a.B - b.B;
    float da = a.A - b.A;
    return MathF.Sqrt((dr * dr) + (dg * dg) + (db * db) + (da * da));
}

/// <summary>Defines fixed validation coverage tiers for CI and release gates.</summary>
internal readonly record struct TerrainValidationTierSpec(
    string Name,
    int SeedCount,
    bool SmokeAllSeeds,
    bool NativeSmoke,
    bool BenchmarkTiles,
    int BenchmarkTileCount)
{
    public bool IsCustom => string.Equals(Name, "custom", StringComparison.Ordinal);

    public static TerrainValidationTierSpec Custom { get; } = new(
        "custom",
        SeedCount: 1,
        SmokeAllSeeds: false,
        NativeSmoke: false,
        BenchmarkTiles: false,
        BenchmarkTileCount: 48);

    public static TerrainValidationTierSpec Pr { get; } = new(
        "pr",
        SeedCount: 1,
        SmokeAllSeeds: false,
        NativeSmoke: false,
        BenchmarkTiles: false,
        BenchmarkTileCount: 48);

    public static TerrainValidationTierSpec Nightly { get; } = new(
        "nightly",
        SeedCount: 10,
        SmokeAllSeeds: true,
        NativeSmoke: false,
        BenchmarkTiles: false,
        BenchmarkTileCount: 48);

    public static TerrainValidationTierSpec Release { get; } = new(
        "release",
        SeedCount: 25,
        SmokeAllSeeds: true,
        NativeSmoke: true,
        BenchmarkTiles: true,
        BenchmarkTileCount: 48);
}

/// <summary>Aggregates planning, quality, experience, and archetype gate results for a single seed.</summary>
internal readonly record struct TerrainValidationResult(
    int Seed,
    TerrainWorldPlan Plan,
    TerrainQualityGateResult QualityGate,
    TerrainWorldPlanningGateResult PlanningGate,
    TerrainExperienceGateResult ExperienceGate,
    TerrainPointOfInterestArchetypeValidationReport ArchetypeGate)
{
    public bool Passed => QualityGate.Passed && PlanningGate.Passed && ExperienceGate.Passed && ArchetypeGate.Passed;
}

/// <summary>Reports whether route corridors produce measurable height/color changes on a sampled tile.</summary>
internal readonly record struct TerrainRouteCorridorSmokeReport(
    bool Passed,
    int Seed,
    TerrainTileCoord Coord,
    int SegmentCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    int InfluencedVertexCount,
    bool SegmentSnapshotIsolated,
    string Reason);

/// <summary>Reports whether corridor-driven road markers and bridge spans materialize across many tiles.</summary>
internal readonly record struct TerrainRouteScatterSmokeReport(
    bool Passed,
    int CandidateTileCount,
    int SampledTileCount,
    int RoadMarkerCount,
    int BridgeSpanCount,
    int RouteLandmarkCount,
    string Reason);

/// <summary>Reports whether planned POIs materialize as landmarks and settlement interior scatter on generated tiles.</summary>
internal readonly record struct TerrainPoiTileSmokeReport(
    bool Passed,
    int ExpectedPointCount,
    int MaterializedPointCount,
    int TileCount,
    int DistinctLandmarkKinds,
    int DistinctScatterLandmarkKinds,
    int VillageLandmarkCount,
    int TownLandmarkCount,
    int OasisHubLandmarkCount,
    int VillageScatterCount,
    int TownScatterCount,
    int OasisHubScatterCount,
    int SettlementLandmarkCount,
    int ExpectedSettlementPointCount,
    int SettlementInteriorScatterCount,
    int VillageHouseScatterCount,
    int TownBlockScatterCount,
    int OasisCanopyScatterCount,
    int SettlementPlazaScatterCount,
    int OasisPoolScatterCount,
    int VillageWellScatterCount,
    int MarketStallScatterCount,
    int WatchTowerScatterCount,
    int OasisGardenScatterCount,
    int SettlementGatewayScatterCount,
    int SettlementServiceScatterCount,
    int FootprintInfluencedVertexCount,
    float FootprintMaxHeightDelta,
    float FootprintMaxColorDelta,
    int LayoutColorVertexCount,
    float LayoutMaxColorDelta,
    int LandmarkScatterCount,
    bool PoiIndexSnapshotIsolated,
    string Reason);

/// <summary>Reports whether POI footprints produce height and color changes on a sampled tile.</summary>
internal readonly record struct TerrainPoiFootprintSmokeReport(
    bool Passed,
    int InfluencedVertexCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    int LayoutColorVertexCount,
    float LayoutMaxColorDelta);

/// <summary>Reports whether validation tier command-line contracts reject weakening overrides.</summary>
internal readonly record struct TerrainValidationCliContractSmokeReport(
    bool Passed,
    bool TierSelectionPassed,
    bool FixedTierConfigurationPassed,
    bool CustomFallbackPassed,
    bool SkipOverrideRejected,
    bool SeedOverrideRejected,
    bool WorldOverrideRejected,
    bool NativeOverrideRejected,
    bool SmokeAllSeedsOverrideRejected,
    bool BenchmarkOverrideRejected,
    bool UnknownTierRejected,
    string Reason);

/// <summary>Reports whether gameplay scatter (understory, resource nodes, hazard outcrops) materializes across tiles.</summary>
internal readonly record struct TerrainGameplayScatterSmokeReport(
    bool Passed,
    int CandidateTileCount,
    int SampledTileCount,
    int UnderstoryCount,
    int ResourceNodeCount,
    int HazardOutcropCount,
    int TotalGameplayScatterCount,
    string Reason);

/// <summary>Reports whether biome-specific scatter (grass, desert, wetland, snow, coast, alpine) materializes across diverse tiles.</summary>
internal readonly record struct TerrainBiomeScatterSmokeReport(
    bool Passed,
    int CandidateTileCount,
    int SampledTileCount,
    int RequiredCategoryCount,
    int MaterializedCategoryCount,
    int GrassTuftCount,
    int DesertShrubCount,
    int CactusClusterCount,
    int ReedClusterCount,
    int SnowClumpCount,
    int AlpinePineCount,
    int CoastalPalmCount,
    int DriftwoodCount,
    int MangroveRootCount,
    int LakeReedCount,
    int WaterLilyCount,
    int BiomeScatterCount,
    int LakeWaterCellCount,
    int RiverWaterCellCount,
    int OasisWaterCellCount,
    int TotalWaterCellCount,
    string Reason);

/// <summary>Reports whether scenic natural landmarks (waterfalls, dunes, monoliths, etc.) materialize across diverse terrain types.</summary>
internal readonly record struct TerrainScenicLandmarkSmokeReport(
    bool Passed,
    int CandidateTileCount,
    int SampledTileCount,
    int WaterfallCount,
    int DuneCrestCount,
    int DesertMonolithCount,
    int CanyonNeedleCount,
    int IceSpireCount,
    int NaturalArchCount,
    int GeothermalSpringCount,
    int GlacialRidgeCount,
    int DistinctGeneratedKindCount,
    int ScenicLandmarkCount,
    string Reason);

/// <summary>Reports whether open-world map and text artifacts are exported and contain meaningful terrain, routes, and POI overlays.</summary>
internal readonly record struct TerrainArtifactSmokeReport(
    bool Passed,
    string OutputDirectory,
    string MapPath,
    string ReportPath,
    string TraversalCostMapPath,
    long MapBytes,
    long ReportBytes,
    long TraversalCostMapBytes,
    int ImageSize,
    int DistinctColorBuckets,
    int NonDarkSampleCount,
    int OverlayChangedPixels,
    float MaxOverlayColorDelta,
    bool MapRasterSnapshotIsolated,
    int TraversalCostColorBuckets,
    int TraversalCostBlockedSamples,
    int TraversalCostGridSize,
    int TraversalCostGridFiniteSamples,
    int TraversalCostGridBlockedSamples,
    bool TraversalCostGridSnapshotIsolated,
    bool ReportContainsRequiredSections,
    string Reason);

/// <summary>Reports whether terrain plans roundtrip through the stable JSON persistence schema and reject drift.</summary>
internal readonly record struct TerrainPlanJsonSmokeReport(
    bool Passed,
    bool MetadataPassed,
    bool SchemaShapePassed,
    bool StringLoadPassed,
    bool StringRoundtripMatches,
    bool FileLoadPassed,
    bool FileRoundtripMatches,
    bool SeedMismatchRejected,
    bool ProfileHashMismatchRejected,
    bool LegacyApiVersionAccepted,
    bool PreviousApiVersionAccepted,
    bool VersionDriftRejected,
    bool EnumNameDriftRejected,
    bool EnumValueDriftRejected,
    bool RoundtripIsolationPassed,
    bool SetWorldPlanPassed,
    int JsonBytes,
    long FileBytes,
    string Reason);

/// <summary>Reports whether public terrain enum names and numeric values match the stable external contract.</summary>
internal readonly record struct TerrainEnumContractSmokeReport(
    bool Passed,
    int CheckedTypeCount,
    int CheckedValueCount,
    string Reason);

/// <summary>Reports whether stable public terrain data carriers kept their property names and types.</summary>
internal readonly record struct TerrainPublicApiShapeSmokeReport(
    bool Passed,
    int CheckedTypeCount,
    int CheckedMemberCount,
    string Reason);

/// <summary>Reports whether generation profile hashing remains stable and field-sensitive.</summary>
internal readonly record struct TerrainProfileHashSmokeReport(
    bool Passed,
    string Hash,
    string ExpectedHash,
    bool FormatPassed,
    bool ExpectedHashPassed,
    bool FieldSensitivityPassed,
    int SensitiveFieldCount,
    int ExpectedFieldCount,
    string Reason);

/// <summary>Reports whether default planning, quality, and experience gate thresholds match the stable open-world contract.</summary>
internal readonly record struct TerrainThresholdContractSmokeReport(
    bool Passed,
    bool PlanningThresholdsPassed,
    bool QualityThresholdsPassed,
    bool ExperienceThresholdsPassed,
    bool BenchmarkThresholdsPassed,
    string Reason);

/// <summary>Reports whether public empty/default terrain sentinel states match the stable contract.</summary>
internal readonly record struct TerrainDefaultStateContractSmokeReport(
    bool Passed,
    bool CorridorNonePassed,
    bool CorridorIndexEmptyPassed,
    bool PointOfInterestIndexEmptyPassed,
    bool WaterSurfaceEmptyPassed,
    bool PlanSnapshotEmptyPassed,
    string Reason);

/// <summary>Stable method signature expected by public API shape validation.</summary>
internal readonly record struct PublicMethodContract(
    string Name,
    bool IsStatic,
    Type ReturnType,
    Type[] ParameterTypes);

/// <summary>Reports whether TerrainWorld's public runtime query facade matches the underlying samplers and exposes isolated plan snapshots.</summary>
internal readonly record struct TerrainRuntimeApiSmokeReport(
    bool Passed,
    bool SampleFieldMatchesSampler,
    bool SampleSurfaceMatchesSampler,
    bool SurfacePositionAxesPassed,
    bool NoPlanTryGetPassed,
    bool NoPlanSnapshotPassed,
    bool EmptyPlanCollectionsPassed,
    bool PlanTryGetPassed,
    bool PlanSnapshotTryGetPassed,
    int PointOfInterestCount,
    int RouteCount,
    bool TraversabilityQueryPassed,
    bool AboveWaterQueryPassed,
    bool WaterStateQueryPassed,
    bool GameplayTagsQueryPassed,
    bool TraversalCostQueryPassed,
    bool StreamingSnapshotPassed,
    bool ApiVersionPassed,
    bool DeterminismContractPassed,
    bool PerformanceContractPassed,
    bool IntegrationInterfacesPassed,
    bool SignalContractsPassed,
    bool PointQueryPassed,
    bool RouteQueryPassed,
    bool RouteCorridorQueryPassed,
    bool PointSnapshotIsolated,
    bool RouteSnapshotIsolated,
    bool WorldPlanSnapshotIsolated,
    string Reason);

/// <summary>Reports whether runtime gameplay anchors expose stable builder, group, meta, and snapshot contracts.</summary>
internal readonly record struct TerrainAnchorContractSmokeReport(
    bool Passed,
    bool PointCountPassed,
    bool RouteCountPassed,
    bool PoiGroupMetaPassed,
    bool RouteGroupMetaPassed,
    bool PoiContractNamesPassed,
    bool RouteContractNamesPassed,
    bool RouteWaypointSnapshotPassed,
    bool DescriptorRebuildPassed,
    bool BuilderPlanSnapshotPassed,
    bool OverlayPlanSnapshotPassed,
    bool MetaKeySnapshotPassed,
    bool AnchorNodeConstantsPassed,
    int PointAnchorCount,
    int RouteAnchorCount,
    string Reason);

/// <summary>Reports whether TerrainWorld's runtime plan entry creates indexed open-world content that materializes on tiles.</summary>
internal readonly record struct TerrainRuntimeWorldSmokeReport(
    bool Passed,
    int PointOfInterestCount,
    int RouteCount,
    int SampledTileCount,
    int MaterializedPointCount,
    int RoadMarkerCount,
    int BridgeSpanCount,
    int SettlementInteriorScatterCount,
    double AsyncPlanMilliseconds,
    bool AsyncPlanMatchesSync,
    double AsyncPlanCancellationMilliseconds,
    bool AsyncPlanCancellationPassed,
    bool HasCorridorIndex,
    bool HasPointIndex,
    bool QualityGatePassed,
    bool PlanningGatePassed,
    bool ExperienceGatePassed,
    bool ArchetypeGatePassed,
    bool SetWorldPlanInvalidationPassed,
    string Reason);

/// <summary>Reports whether async runtime plan generation responds to cancellation.</summary>
internal readonly record struct TerrainRuntimeWorldCancellationReport(
    bool Passed,
    double ElapsedMilliseconds);

/// <summary>Pairs a world position with a score for sorting scatter candidate regions.</summary>
internal readonly record struct GameplayScatterRegionCandidate(
    Vector2 WorldPosition,
    float Score);

/// <summary>Reports whether native sampler height grids and tile output match the managed sampler within tolerance.</summary>
internal readonly record struct TerrainNativeSamplerSmokeReport(
    bool Passed,
    int Seed,
    string ProfileHash,
    bool Available,
    TerrainTileCoord Coord,
    int Resolution,
    int ComparedSampleCount,
    bool FieldGridAvailable,
    bool FieldGridContainsDerivedData,
    int ComparedFieldValueCount,
    float MaxFieldDelta,
    float AverageFieldDelta,
    int FieldClassificationMismatchCount,
    float MaxHeightDelta,
    float AverageHeightDelta,
    int TileVertexCount,
    float TileMaxHeightDelta,
    float TileMaxColorDelta,
    string Reason);

/// <summary>Reports how representative the benchmark tile set is across gameplay, routes, POIs, biomes, and landscapes.</summary>
internal readonly record struct TerrainTileBenchmarkCoverage(
    int DistinctBiomeKinds,
    int DistinctLandscapeKinds,
    int PointOfInterestTileCount,
    int RouteTileCount,
    int GameplayRichTileCount);

/// <summary>Reports managed vs native tile generation benchmark results including timing, allocations, parity, and sample coverage.</summary>
internal readonly record struct TerrainTileBenchmarkReport(
    bool Passed,
    int Seed,
    string ProfileHash,
    string ManagedBackendMode,
    string NativeBackendMode,
    bool NativeAvailable,
    bool NativeSelectedForTileGeneration,
    int RequestedTileCount,
    int MeasuredTileCount,
    TerrainTileBenchmarkCoverage Coverage,
    TerrainTileBenchmarkPass Managed,
    TerrainTileBenchmarkPass Native,
    int ParityTileCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    double NativeSpeedup,
    int MeasurementPassCount,
    TerrainTileBenchmarkThresholds Thresholds,
    string Reason);

/// <summary>Thresholds for tile generation benchmark pass/fail criteria.</summary>
internal readonly record struct TerrainTileBenchmarkThresholds(
    double MaxManagedMillisecondsPerTile,
    double MaxNativeMillisecondsPerTile,
    double MaxManagedP50Milliseconds,
    double MaxManagedP95Milliseconds,
    double MaxManagedP99Milliseconds,
    double MaxNativeP50Milliseconds,
    double MaxNativeP95Milliseconds,
    double MaxNativeP99Milliseconds,
    double MaxAllocatedKilobytesPerTile,
    double MinNativeSpeedup,
    int MinParityTileCount,
    int MinBenchmarkBiomeKinds,
    int MinBenchmarkLandscapeKinds,
    int MinBenchmarkPointOfInterestTiles,
    int MinBenchmarkRouteTiles,
    int MinBenchmarkGameplayRichTiles,
    float MaxParityHeightDelta,
    float MaxParityColorDelta)
{
    public static TerrainTileBenchmarkThresholds Default { get; } = new(
        MaxManagedMillisecondsPerTile: TerrainPerformanceContract.MaxManagedMillisecondsPerTile,
        MaxNativeMillisecondsPerTile: TerrainPerformanceContract.MaxNativeMillisecondsPerTile,
        MaxManagedP50Milliseconds: TerrainPerformanceContract.MaxManagedP50Milliseconds,
        MaxManagedP95Milliseconds: TerrainPerformanceContract.MaxManagedP95Milliseconds,
        MaxManagedP99Milliseconds: TerrainPerformanceContract.MaxManagedP99Milliseconds,
        MaxNativeP50Milliseconds: TerrainPerformanceContract.MaxNativeP50Milliseconds,
        MaxNativeP95Milliseconds: TerrainPerformanceContract.MaxNativeP95Milliseconds,
        MaxNativeP99Milliseconds: TerrainPerformanceContract.MaxNativeP99Milliseconds,
        MaxAllocatedKilobytesPerTile: TerrainPerformanceContract.MaxAllocatedKilobytesPerTile,
        MinNativeSpeedup: TerrainPerformanceContract.MinNativeSpeedup,
        MinParityTileCount: TerrainPerformanceContract.MinParityTileCount,
        MinBenchmarkBiomeKinds: TerrainPerformanceContract.MinBenchmarkBiomeKinds,
        MinBenchmarkLandscapeKinds: TerrainPerformanceContract.MinBenchmarkLandscapeKinds,
        MinBenchmarkPointOfInterestTiles: TerrainPerformanceContract.MinBenchmarkPointOfInterestTiles,
        MinBenchmarkRouteTiles: TerrainPerformanceContract.MinBenchmarkRouteTiles,
        MinBenchmarkGameplayRichTiles: TerrainPerformanceContract.MinBenchmarkGameplayRichTiles,
        MaxParityHeightDelta: TerrainDeterminismContract.TileParityHeightEpsilon,
        MaxParityColorDelta: TerrainDeterminismContract.TileParityColorEpsilon);
}

/// <summary>Measured performance data for a single tile build pass (managed or native).</summary>
internal readonly record struct TerrainTileBenchmarkPass(
    int TileCount,
    long TotalVertices,
    long TotalIndices,
    long TotalScatter,
    long TotalLandmarks,
    double ElapsedMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    long AllocatedBytes,
    double HeightChecksum)
{
    public double TilesPerSecond => ElapsedMilliseconds <= 0.0 ? 0.0 : TileCount / (ElapsedMilliseconds / 1000.0);
    public double MillisecondsPerTile => TileCount == 0 ? 0.0 : ElapsedMilliseconds / TileCount;
    public double AllocatedMegabytes => AllocatedBytes / (1024.0 * 1024.0);
    public double AllocatedKilobytesPerTile => TileCount == 0 ? 0.0 : (AllocatedBytes / 1024.0) / TileCount;
}

/// <summary>Aggregates min/max/average statistics across multiple seed validation results.</summary>
internal sealed class TerrainValidationAggregate
{
    private int _count;
    private double _landRatioSum;
    private double _scenicRatioSum;
    private double _traversableLandRatioSum;
    private double _poiCountSum;
    private double _routeCountSum;
    private double _connectedPointRatioSum;
    private double _connectedSettlementRatioSum;
    private double _settlementRouteCountSum;
    private double _pointOfInterestWorldCoverageSum;
    private double _routeWorldCoverageSum;
    private double _routeScenicPotentialSum;
    private double _routeTraversabilitySum;
    private double _encounterPotentialSum;
    private double _routeRhythmScoreSum;
    private double _riskRewardBalanceSum;
    private double _villageCountSum;
    private double _townCountSum;
    private double _oasisHubCountSum;

    public float MinLandRatio { get; private set; } = float.PositiveInfinity;
    public float MaxLandRatio { get; private set; } = float.NegativeInfinity;
    public float MinScenicRatio { get; private set; } = float.PositiveInfinity;
    public float MaxScenicRatio { get; private set; } = float.NegativeInfinity;
    public float MinTraversableLandRatio { get; private set; } = float.PositiveInfinity;
    public float MaxTraversableLandRatio { get; private set; } = float.NegativeInfinity;
    public int MinPoiCount { get; private set; } = int.MaxValue;
    public int MaxPoiCount { get; private set; } = int.MinValue;
    public int MinRouteCount { get; private set; } = int.MaxValue;
    public int MaxRouteCount { get; private set; } = int.MinValue;
    public float MinConnectedPointRatio { get; private set; } = float.PositiveInfinity;
    public float MaxConnectedPointRatio { get; private set; } = float.NegativeInfinity;
    public float MinConnectedSettlementRatio { get; private set; } = float.PositiveInfinity;
    public float MaxConnectedSettlementRatio { get; private set; } = float.NegativeInfinity;
    public int MinSettlementRouteCount { get; private set; } = int.MaxValue;
    public int MaxSettlementRouteCount { get; private set; } = int.MinValue;
    public float MinPointOfInterestWorldCoverage { get; private set; } = float.PositiveInfinity;
    public float MaxPointOfInterestWorldCoverage { get; private set; } = float.NegativeInfinity;
    public float MinRouteWorldCoverage { get; private set; } = float.PositiveInfinity;
    public float MaxRouteWorldCoverage { get; private set; } = float.NegativeInfinity;
    public float MinRouteScenicPotential { get; private set; } = float.PositiveInfinity;
    public float MaxRouteScenicPotential { get; private set; } = float.NegativeInfinity;
    public float MinRouteTraversability { get; private set; } = float.PositiveInfinity;
    public float MaxRouteTraversability { get; private set; } = float.NegativeInfinity;
    public float MinEncounterPotential { get; private set; } = float.PositiveInfinity;
    public float MaxEncounterPotential { get; private set; } = float.NegativeInfinity;
    public float MinRouteRhythmScore { get; private set; } = float.PositiveInfinity;
    public float MaxRouteRhythmScore { get; private set; } = float.NegativeInfinity;
    public float MinRiskRewardBalance { get; private set; } = float.PositiveInfinity;
    public float MaxRiskRewardBalance { get; private set; } = float.NegativeInfinity;
    public int MinVillageCount { get; private set; } = int.MaxValue;
    public int MaxVillageCount { get; private set; } = int.MinValue;
    public int MinTownCount { get; private set; } = int.MaxValue;
    public int MaxTownCount { get; private set; } = int.MinValue;
    public int MinOasisHubCount { get; private set; } = int.MaxValue;
    public int MaxOasisHubCount { get; private set; } = int.MinValue;
    public int ExperienceFailureCount { get; private set; }
    public int ArchetypeFailureCount { get; private set; }

    public double AverageLandRatio => Average(_landRatioSum);
    public double AverageScenicRatio => Average(_scenicRatioSum);
    public double AverageTraversableLandRatio => Average(_traversableLandRatioSum);
    public double AveragePoiCount => Average(_poiCountSum);
    public double AverageRouteCount => Average(_routeCountSum);
    public double AverageConnectedPointRatio => Average(_connectedPointRatioSum);
    public double AverageConnectedSettlementRatio => Average(_connectedSettlementRatioSum);
    public double AverageSettlementRouteCount => Average(_settlementRouteCountSum);
    public double AveragePointOfInterestWorldCoverage => Average(_pointOfInterestWorldCoverageSum);
    public double AverageRouteWorldCoverage => Average(_routeWorldCoverageSum);
    public double AverageRouteScenicPotential => Average(_routeScenicPotentialSum);
    public double AverageRouteTraversability => Average(_routeTraversabilitySum);
    public double AverageEncounterPotential => Average(_encounterPotentialSum);
    public double AverageRouteRhythmScore => Average(_routeRhythmScoreSum);
    public double AverageRiskRewardBalance => Average(_riskRewardBalanceSum);
    public double AverageVillageCount => Average(_villageCountSum);
    public double AverageTownCount => Average(_townCountSum);
    public double AverageOasisHubCount => Average(_oasisHubCountSum);

    public void Add(TerrainValidationResult result)
    {
        TerrainQualityReport quality = result.QualityGate.Report;
        TerrainWorldPlanningReport planning = result.PlanningGate.Report;
        TerrainExperienceReport experience = result.ExperienceGate.Report;
        _count++;
        _landRatioSum += quality.LandRatio;
        _scenicRatioSum += quality.ScenicRatio;
        _traversableLandRatioSum += quality.TraversableLandRatio;
        _poiCountSum += planning.PointOfInterestCount;
        _routeCountSum += planning.RouteCount;
        _connectedPointRatioSum += planning.ConnectedPointRatio;
        _connectedSettlementRatioSum += planning.ConnectedSettlementRatio;
        _settlementRouteCountSum += planning.SettlementRouteCount;
        _pointOfInterestWorldCoverageSum += planning.PointOfInterestWorldCoverage;
        _routeWorldCoverageSum += planning.RouteWorldCoverage;
        _routeScenicPotentialSum += planning.AverageRouteScenicPotential;
        _routeTraversabilitySum += planning.AverageRouteTraversability;
        _encounterPotentialSum += experience.AverageEncounterPotential;
        _routeRhythmScoreSum += experience.RouteRhythmScore;
        _riskRewardBalanceSum += experience.RiskRewardBalance;
        _villageCountSum += planning.VillageCount;
        _townCountSum += planning.TownCount;
        _oasisHubCountSum += planning.OasisHubCount;

        MinLandRatio = Math.Min(MinLandRatio, quality.LandRatio);
        MaxLandRatio = Math.Max(MaxLandRatio, quality.LandRatio);
        MinScenicRatio = Math.Min(MinScenicRatio, quality.ScenicRatio);
        MaxScenicRatio = Math.Max(MaxScenicRatio, quality.ScenicRatio);
        MinTraversableLandRatio = Math.Min(MinTraversableLandRatio, quality.TraversableLandRatio);
        MaxTraversableLandRatio = Math.Max(MaxTraversableLandRatio, quality.TraversableLandRatio);
        MinPoiCount = Math.Min(MinPoiCount, planning.PointOfInterestCount);
        MaxPoiCount = Math.Max(MaxPoiCount, planning.PointOfInterestCount);
        MinRouteCount = Math.Min(MinRouteCount, planning.RouteCount);
        MaxRouteCount = Math.Max(MaxRouteCount, planning.RouteCount);
        MinConnectedPointRatio = Math.Min(MinConnectedPointRatio, planning.ConnectedPointRatio);
        MaxConnectedPointRatio = Math.Max(MaxConnectedPointRatio, planning.ConnectedPointRatio);
        MinConnectedSettlementRatio = Math.Min(MinConnectedSettlementRatio, planning.ConnectedSettlementRatio);
        MaxConnectedSettlementRatio = Math.Max(MaxConnectedSettlementRatio, planning.ConnectedSettlementRatio);
        MinSettlementRouteCount = Math.Min(MinSettlementRouteCount, planning.SettlementRouteCount);
        MaxSettlementRouteCount = Math.Max(MaxSettlementRouteCount, planning.SettlementRouteCount);
        MinPointOfInterestWorldCoverage = Math.Min(MinPointOfInterestWorldCoverage, planning.PointOfInterestWorldCoverage);
        MaxPointOfInterestWorldCoverage = Math.Max(MaxPointOfInterestWorldCoverage, planning.PointOfInterestWorldCoverage);
        MinRouteWorldCoverage = Math.Min(MinRouteWorldCoverage, planning.RouteWorldCoverage);
        MaxRouteWorldCoverage = Math.Max(MaxRouteWorldCoverage, planning.RouteWorldCoverage);
        MinRouteScenicPotential = Math.Min(MinRouteScenicPotential, planning.AverageRouteScenicPotential);
        MaxRouteScenicPotential = Math.Max(MaxRouteScenicPotential, planning.AverageRouteScenicPotential);
        MinRouteTraversability = Math.Min(MinRouteTraversability, planning.AverageRouteTraversability);
        MaxRouteTraversability = Math.Max(MaxRouteTraversability, planning.AverageRouteTraversability);
        MinEncounterPotential = Math.Min(MinEncounterPotential, experience.AverageEncounterPotential);
        MaxEncounterPotential = Math.Max(MaxEncounterPotential, experience.AverageEncounterPotential);
        MinRouteRhythmScore = Math.Min(MinRouteRhythmScore, experience.RouteRhythmScore);
        MaxRouteRhythmScore = Math.Max(MaxRouteRhythmScore, experience.RouteRhythmScore);
        MinRiskRewardBalance = Math.Min(MinRiskRewardBalance, experience.RiskRewardBalance);
        MaxRiskRewardBalance = Math.Max(MaxRiskRewardBalance, experience.RiskRewardBalance);
        MinVillageCount = Math.Min(MinVillageCount, planning.VillageCount);
        MaxVillageCount = Math.Max(MaxVillageCount, planning.VillageCount);
        MinTownCount = Math.Min(MinTownCount, planning.TownCount);
        MaxTownCount = Math.Max(MaxTownCount, planning.TownCount);
        MinOasisHubCount = Math.Min(MinOasisHubCount, planning.OasisHubCount);
        MaxOasisHubCount = Math.Max(MaxOasisHubCount, planning.OasisHubCount);

        if (!result.ExperienceGate.Passed)
        {
            ExperienceFailureCount++;
        }

        if (!result.ArchetypeGate.Passed)
        {
            ArchetypeFailureCount++;
        }
    }

    private double Average(double sum)
    {
        return _count == 0 ? 0.0 : sum / _count;
    }
}
