using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using static TerrainValidationCliHelpers;
using static TerrainValidationBenchmarkChecks;
using static TerrainValidationContractChecks;
using static TerrainValidationOutput;
using static TerrainValidationRuntimeProbeHelpers;
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

static bool TraversalCostGridMatches(
    TerrainTraversalCostGrid expected,
    TerrainTraversalCostGrid actual)
{
    if (expected.Width != actual.Width ||
        expected.Height != actual.Height ||
        !ExactPositionEquals(expected.Center, actual.Center) ||
        !ExactFloatEquals(expected.WorldSize, actual.WorldSize) ||
        expected.SampleCount != actual.SampleCount)
    {
        return false;
    }

    for (int i = 0; i < expected.Height; i++)
    {
        for (int j = 0; j < expected.Width; j++)
        {
            if (!TerrainTraversalCostsMatch(expected.GetSample(j, i), actual.GetSample(j, i)))
            {
                return false;
            }
        }
    }

    return true;
}

static bool TraversalCostGridSnapshotIsolated(TerrainTraversalCostGrid grid)
{
    if (grid.Width <= 0 || grid.Height <= 0 || grid.SampleCount < grid.Width * grid.Height)
    {
        return false;
    }

    TerrainTraversalCost firstSample = grid.GetSample(0, 0);
    TerrainTraversalCost[] sampleSnapshot = grid.ToSampleArray();
    if (sampleSnapshot.Length == 0)
    {
        return false;
    }

    sampleSnapshot[0] = default;
    bool snapshotMutationIsolated = TerrainTraversalCostsMatch(firstSample, grid.GetSample(0, 0)) &&
        !TerrainTraversalCostsMatch(sampleSnapshot[0], grid.GetSample(0, 0));

    TerrainTraversalCost[] constructorSamples = grid.ToSampleArray();
    TerrainTraversalCostGrid constructed = new(
        grid.Width,
        grid.Height,
        grid.Center,
        grid.WorldSize,
        constructorSamples);
    constructorSamples[0] = default;
    bool constructorInputIsolated = TerrainTraversalCostsMatch(firstSample, constructed.GetSample(0, 0));

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
        reportText.Contains($"Terrain Scatter Rule Set Hash: {NormalizeScatterRuleSetHash(profile.ScatterRuleSetHash)}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain Settlement Visual Rule Set Hash: {NormalizeSettlementVisualRuleSetHash(profile.SettlementVisualRuleSetHash)}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain POI Rule Set Hash: {NormalizePointOfInterestRuleSetHash(profile.PointOfInterestRuleSetHash)}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain Route Rule Set Hash: {NormalizeRouteRuleSetHash(profile.RouteRuleSetHash)}", StringComparison.Ordinal) &&
        reportText.Contains($"Terrain Scenic Landmark Rule Set Hash: {NormalizeScenicRuleSetHash(profile.ScenicLandmarkRuleSetHash)}", StringComparison.Ordinal) &&
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
        bool currentApiMinusOneVersionAccepted = AcceptsCompatibleApiVersion(json, profile, "1.2.0");
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
            currentApiMinusOneVersionAccepted &&
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
                currentApiMinusOneVersionAccepted,
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
            currentApiMinusOneVersionAccepted,
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
        JsonStringEquals(root, "scatterRuleSetHash", NormalizeScatterRuleSetHash(profile.ScatterRuleSetHash)) &&
        JsonStringEquals(root, "settlementVisualRuleSetHash", NormalizeSettlementVisualRuleSetHash(profile.SettlementVisualRuleSetHash)) &&
        JsonStringEquals(root, "pointOfInterestRuleSetHash", NormalizePointOfInterestRuleSetHash(profile.PointOfInterestRuleSetHash)) &&
        JsonStringEquals(root, "routeRuleSetHash", NormalizeRouteRuleSetHash(profile.RouteRuleSetHash)) &&
        JsonStringEquals(root, "scenicLandmarkRuleSetHash", NormalizeScenicRuleSetHash(profile.ScenicLandmarkRuleSetHash)) &&
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

static string NormalizeScatterRuleSetHash(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainScatterRuleCatalog")
        : value;
}

static string NormalizeSettlementVisualRuleSetHash(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainSettlementVisualRuleCatalog")
        : value;
}

static string NormalizePointOfInterestRuleSetHash(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainPointOfInterestRuleCatalog")
        : value;
}

static string NormalizeRouteRuleSetHash(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainRouteRuleCatalog")
        : value;
}

static string NormalizeScenicRuleSetHash(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainScenicLandmarkRuleCatalog")
        : value;
}

static string ResolveInternalDefaultRuleHash(string fullTypeName)
{
    Type assemblyType = typeof(TerrainWorld);
    Type? type = assemblyType.Assembly.GetType(fullTypeName, throwOnError: false);
    if (type is null)
    {
        throw new InvalidOperationException($"Unable to resolve terrain rule catalog type '{fullTypeName}'.");
    }

    PropertyInfo? property = type.GetProperty("DefaultHash", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (property?.GetValue(null) is string value && !string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    throw new InvalidOperationException($"Terrain rule catalog '{fullTypeName}' did not expose a usable DefaultHash.");
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

static bool ExactRectEquals(Rect2 expected, Rect2 actual)
{
    return ExactPositionEquals(expected.Position, actual.Position) &&
        ExactPositionEquals(expected.Size, actual.Size);
}

static bool ContractPositionEquals(Vector2 expected, Vector2 actual)
{
    return expected.DistanceSquaredTo(actual) <= TerrainDeterminismContract.Squared(TerrainDeterminismContract.PositionEpsilon);
}

static Rect2 ComputeExpectedGameplayTagRegionBounds(TerrainWorldPlan plan, TerrainWorldRegion region)
{
    float cellSize = plan.WorldSize / plan.GridResolution;
    Vector2 min = new(
        plan.Center.X - plan.WorldSize * 0.5f + region.GridX * cellSize,
        plan.Center.Y - plan.WorldSize * 0.5f + region.GridY * cellSize);
    return new Rect2(min, new Vector2(cellSize, cellSize));
}

static TerrainGameplayTag ComputeExpectedGameplayTagFlags(
    TerrainWorldRegion region,
    TerrainGenerationProfile profile)
{
    TerrainGameplayTag flags = TerrainGameplayTag.None;

    if (region.Traversability >= 0.45f)
    {
        flags |= TerrainGameplayTag.Traversable;
    }

    if (region.ScenicPotential >= 0.62f)
    {
        flags |= TerrainGameplayTag.Scenic;
    }

    if (region.ResourcePotential >= 0.50f)
    {
        flags |= TerrainGameplayTag.ResourceRich;
    }

    if (region.HazardPotential >= 0.42f)
    {
        flags |= TerrainGameplayTag.Hazardous;
    }

    if (region.EncounterPotential >= 0.52f)
    {
        flags |= TerrainGameplayTag.EncounterRich;
    }

    if (region.River >= 0.34f ||
        region.BiomeKind is TerrainBiomeKind.Coast or TerrainBiomeKind.Lake or TerrainBiomeKind.Oasis ||
        region.LandscapeKind is TerrainLandscapeKind.Coast or TerrainLandscapeKind.Lake or TerrainLandscapeKind.RiverValley ||
        region.RegionKind is TerrainWorldRegionKind.Coast or TerrainWorldRegionKind.Lake or TerrainWorldRegionKind.Oasis or TerrainWorldRegionKind.RiverValley)
    {
        flags |= TerrainGameplayTag.WaterAccess;
    }

    if (region.BiomeKind == TerrainBiomeKind.Coast ||
        region.LandscapeKind == TerrainLandscapeKind.Coast ||
        region.RegionKind == TerrainWorldRegionKind.Coast)
    {
        flags |= TerrainGameplayTag.Coastal;
    }

    if (region.Traversability >= 0.54f &&
        region.ResourcePotential >= 0.38f &&
        region.HazardPotential < 0.65f &&
        region.Height >= profile.SeaLevel + 8.0f)
    {
        flags |= TerrainGameplayTag.SettlementFriendly;
    }

    if (region.Height > profile.SeaLevel + profile.HeightScale * 0.55f ||
        region.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.Snowfield or TerrainLandscapeKind.VistaPlateau ||
        region.RegionKind is TerrainWorldRegionKind.Highlands or TerrainWorldRegionKind.Mountains or TerrainWorldRegionKind.Snow or TerrainWorldRegionKind.ScenicPlateau)
    {
        flags |= TerrainGameplayTag.HighElevation;
    }

    if (region.BiomeKind == TerrainBiomeKind.Snowfield ||
        region.LandscapeKind == TerrainLandscapeKind.Snowfield ||
        region.RegionKind == TerrainWorldRegionKind.Snow)
    {
        flags |= TerrainGameplayTag.Cold;
    }

    if (region.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis ||
        region.RegionKind is TerrainWorldRegionKind.Desert or TerrainWorldRegionKind.Oasis)
    {
        flags |= TerrainGameplayTag.Arid;
    }

    return flags;
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
    bool currentApiMinusOneVersionAccepted,
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

    if (!currentApiMinusOneVersionAccepted)
    {
        return "plan JSON rejected a compatible terrain-api-v1 1.2.0 plan";
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
                    ("ScatterRuleSetHash", typeof(string)),
                    ("SettlementVisualRuleSetHash", typeof(string)),
                    ("PointOfInterestRuleSetHash", typeof(string)),
                    ("RouteRuleSetHash", typeof(string)),
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
            CheckPublicShape<TerrainWorldPointOfInterestSummary>(
                [
                    ("Id", typeof(int)),
                    ("Kind", typeof(TerrainPointOfInterestKind)),
                    ("WorldPosition", typeof(Vector2)),
                    ("Distance", typeof(float)),
                    ("Score", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("Traversability", typeof(float)),
                    ("SettlementTier", typeof(TerrainSettlementTier)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainWorldRouteSummary>(
                [
                    ("FromPointId", typeof(int)),
                    ("ToPointId", typeof(int)),
                    ("Kind", typeof(TerrainRouteKind)),
                    ("Distance", typeof(float)),
                    ("Cost", typeof(float)),
                    ("AverageScenicPotential", typeof(float)),
                    ("AverageTraversability", typeof(float)),
                    ("WaypointCount", typeof(int))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainGameplayTagRegionSummary>(
                [
                    ("GridX", typeof(int)),
                    ("GridY", typeof(int)),
                    ("WorldPosition", typeof(Vector2)),
                    ("WorldBounds", typeof(Rect2)),
                    ("Flags", typeof(TerrainGameplayTag)),
                    ("BiomeKind", typeof(TerrainBiomeKind)),
                    ("LandscapeKind", typeof(TerrainLandscapeKind)),
                    ("RegionKind", typeof(TerrainWorldRegionKind)),
                    ("Traversability", typeof(float)),
                    ("ScenicPotential", typeof(float)),
                    ("ResourcePotential", typeof(float)),
                    ("HazardPotential", typeof(float)),
                    ("EncounterPotential", typeof(float))
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
            CheckPublicShape<TerrainScatterVariantRuleResource>(
                [
                    ("ProbabilityLow", typeof(float)),
                    ("ProbabilityHigh", typeof(float)),
                    ("BaseScale", typeof(float)),
                    ("ScaleJitterFactor", typeof(float)),
                    ("TintLow", typeof(Color)),
                    ("TintHigh", typeof(Color))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainScatterRuleSet>(
                [
                    ("NaturalDensityPenalty", typeof(float)),
                    ("BaseDensityPenalty", typeof(float)),
                    ("TreeMaxSlope", typeof(float)),
                    ("TreeMinMoisture", typeof(float)),
                    ("TreeMinTemperature", typeof(float)),
                    ("TreeMaxRiver", typeof(float)),
                    ("TreeMinTraversability", typeof(float)),
                    ("TreeProbability", typeof(float)),
                    ("TreeBaseScale", typeof(float)),
                    ("TreeScaleJitter", typeof(float)),
                    ("TreeTintLow", typeof(Color)),
                    ("TreeTintHigh", typeof(Color)),
                    ("RockMinSlope", typeof(float)),
                    ("RockMinHeightAboveSea", typeof(float)),
                    ("RockMinHazardPotential", typeof(float)),
                    ("RockProbability", typeof(float)),
                    ("RockBaseScale", typeof(float)),
                    ("RockScaleJitter", typeof(float)),
                    ("RockTintLow", typeof(Color)),
                    ("RockTintHigh", typeof(Color)),
                    ("UnderstoryMaxSlope", typeof(float)),
                    ("UnderstoryMinResourcePotential", typeof(float)),
                    ("UnderstoryMinMoisture", typeof(float)),
                    ("UnderstoryMinTemperature", typeof(float)),
                    ("UnderstoryProbabilityLow", typeof(float)),
                    ("UnderstoryProbabilityHigh", typeof(float)),
                    ("UnderstoryBaseScale", typeof(float)),
                    ("UnderstoryScaleJitter", typeof(float)),
                    ("UnderstoryTintLow", typeof(Color)),
                    ("UnderstoryTintHigh", typeof(Color)),
                    ("ResourceNodeMaxSlope", typeof(float)),
                    ("ResourceNodeMinResourcePotential", typeof(float)),
                    ("ResourceNodeMinTraversability", typeof(float)),
                    ("ResourceNodeMinTemperature", typeof(float)),
                    ("ResourceNodeProbabilityLow", typeof(float)),
                    ("ResourceNodeProbabilityHigh", typeof(float)),
                    ("ResourceNodeBaseScale", typeof(float)),
                    ("ResourceNodeScaleJitter", typeof(float)),
                    ("ResourceNodeTintLow", typeof(Color)),
                    ("ResourceNodeTintHigh", typeof(Color)),
                    ("HazardOutcropMaxSlope", typeof(float)),
                    ("HazardOutcropMinHazardPotential", typeof(float)),
                    ("HazardOutcropMinEncounterPotential", typeof(float)),
                    ("HazardOutcropMinTemperature", typeof(float)),
                    ("HazardOutcropProbabilityLow", typeof(float)),
                    ("HazardOutcropProbabilityHigh", typeof(float)),
                    ("HazardOutcropBaseScale", typeof(float)),
                    ("HazardOutcropScaleJitter", typeof(float)),
                    ("HazardOutcropTintLow", typeof(Color)),
                    ("HazardOutcropTintHigh", typeof(Color)),
                    ("TidalMangroveFlatMaxSlope", typeof(float)),
                    ("TidalMangroveFlatMinHeightOffset", typeof(float)),
                    ("TidalMangroveFlatMaxHeightOffset", typeof(float)),
                    ("TidalMangroveFlatMinMoisture", typeof(float)),
                    ("TidalMangroveFlatMinTemperature", typeof(float)),
                    ("TidalMangroveFlatRiverThreshold", typeof(float)),
                    ("TidalMangroveFlatShorelineHeightOffset", typeof(float)),
                    ("LakeScatterZoneMaxSlope", typeof(float)),
                    ("LakeScatterZoneMinHeightOffset", typeof(float)),
                    ("LakeScatterZoneMaxHeightFactor", typeof(float)),
                    ("LakeScatterZoneMinLake", typeof(float)),
                    ("LakeScatterZoneMinMoisture", typeof(float)),
                    ("LakeScatterZoneMinResourcePotential", typeof(float)),
                    ("TidalMangroveRoot", typeof(TerrainScatterVariantRuleResource)),
                    ("LakeWaterLily", typeof(TerrainScatterVariantRuleResource)),
                    ("LakeReed", typeof(TerrainScatterVariantRuleResource)),
                    ("GrassTuft", typeof(TerrainScatterVariantRuleResource)),
                    ("CoastalMangroveRoot", typeof(TerrainScatterVariantRuleResource)),
                    ("CoastalPalm", typeof(TerrainScatterVariantRuleResource)),
                    ("Driftwood", typeof(TerrainScatterVariantRuleResource)),
                    ("OasisReed", typeof(TerrainScatterVariantRuleResource)),
                    ("DesertCactus", typeof(TerrainScatterVariantRuleResource)),
                    ("DesertShrub", typeof(TerrainScatterVariantRuleResource)),
                    ("WetlandMangroveRoot", typeof(TerrainScatterVariantRuleResource)),
                    ("WetlandReed", typeof(TerrainScatterVariantRuleResource)),
                    ("SnowfieldAlpinePine", typeof(TerrainScatterVariantRuleResource)),
                    ("SnowClump", typeof(TerrainScatterVariantRuleResource)),
                    ("MountainAlpinePine", typeof(TerrainScatterVariantRuleResource))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainSettlementVisualRuleSet>(
                [
                    ("VillageInteriorCount", typeof(int)),
                    ("TownInteriorCount", typeof(int)),
                    ("OasisHubInteriorCount", typeof(int)),
                    ("SettlementLandmarkBaseScale", typeof(float)),
                    ("VistaLandmarkBaseScale", typeof(float)),
                    ("RiverCrossingLandmarkBaseScale", typeof(float)),
                    ("MountainPassLandmarkBaseScale", typeof(float)),
                    ("CoastalLandingLandmarkBaseScale", typeof(float)),
                    ("ResourceGroveLandmarkBaseScale", typeof(float)),
                    ("CanyonOverlookLandmarkBaseScale", typeof(float)),
                    ("OasisLandmarkBaseScale", typeof(float)),
                    ("VillageLandmarkBaseScale", typeof(float)),
                    ("TownLandmarkBaseScale", typeof(float)),
                    ("OasisHubLandmarkBaseScale", typeof(float)),
                    ("DefaultGatewayTierScale", typeof(float)),
                    ("VillageGatewayTierScale", typeof(float)),
                    ("TownGatewayTierScale", typeof(float)),
                    ("OasisHubGatewayTierScale", typeof(float)),
                    ("DefaultGatewayRouteScale", typeof(float)),
                    ("PrimaryTrailGatewayRouteScale", typeof(float)),
                    ("RiverRoadGatewayRouteScale", typeof(float)),
                    ("CoastalPathGatewayRouteScale", typeof(float)),
                    ("DefaultGatewayBaseColor", typeof(Color)),
                    ("VillageGatewayBaseColor", typeof(Color)),
                    ("TownGatewayBaseColor", typeof(Color)),
                    ("OasisHubGatewayBaseColor", typeof(Color)),
                    ("DefaultGatewayRouteTint", typeof(Color)),
                    ("RiverRoadGatewayRouteTint", typeof(Color)),
                    ("CoastalPathGatewayRouteTint", typeof(Color)),
                    ("RidgePassGatewayRouteTint", typeof(Color)),
                    ("ScenicTrailGatewayRouteTint", typeof(Color)),
                    ("VillageInteriorBaseScale", typeof(float)),
                    ("TownInteriorBaseScale", typeof(float)),
                    ("OasisHubInteriorBaseScale", typeof(float)),
                    ("VillageWellBaseScale", typeof(float)),
                    ("MarketStallBaseScale", typeof(float)),
                    ("WatchTowerBaseScale", typeof(float)),
                    ("OasisGardenBaseScale", typeof(float)),
                    ("SettlementLandmarkBaseColor", typeof(Color)),
                    ("VistaLandmarkBaseColor", typeof(Color)),
                    ("RiverCrossingLandmarkBaseColor", typeof(Color)),
                    ("MountainPassLandmarkBaseColor", typeof(Color)),
                    ("CoastalLandingLandmarkBaseColor", typeof(Color)),
                    ("ResourceGroveLandmarkBaseColor", typeof(Color)),
                    ("CanyonOverlookLandmarkBaseColor", typeof(Color)),
                    ("OasisLandmarkBaseColor", typeof(Color)),
                    ("VillageLandmarkBaseColor", typeof(Color)),
                    ("TownLandmarkBaseColor", typeof(Color)),
                    ("OasisHubLandmarkBaseColor", typeof(Color)),
                    ("DefaultInteriorVariationColor", typeof(Color)),
                    ("TownBlockVariationColor", typeof(Color)),
                    ("OasisCanopyVariationColor", typeof(Color)),
                    ("SettlementPlazaVariationColor", typeof(Color)),
                    ("OasisPoolVariationColor", typeof(Color)),
                    ("VillageWellVariationColor", typeof(Color)),
                    ("MarketStallVariationColor", typeof(Color)),
                    ("WatchTowerVariationColor", typeof(Color)),
                    ("OasisGardenVariationColor", typeof(Color))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainPointOfInterestRuleSet>(
                [
                    ("SettlementCandidateThreshold", typeof(float)),
                    ("VistaThreshold", typeof(float)),
                    ("RiverCrossingThreshold", typeof(float)),
                    ("MountainPassThreshold", typeof(float)),
                    ("CoastalLandingThreshold", typeof(float)),
                    ("ResourceGroveThreshold", typeof(float)),
                    ("AncientSiteThreshold", typeof(float)),
                    ("CanyonOverlookThreshold", typeof(float)),
                    ("OasisThreshold", typeof(float)),
                    ("SettlementStableFlatLandWeight", typeof(float)),
                    ("SettlementMoistureWeight", typeof(float)),
                    ("SettlementTemperatureWeight", typeof(float)),
                    ("SettlementRiverWeight", typeof(float)),
                    ("SettlementScenicWeight", typeof(float)),
                    ("SettlementPlainsGrassBonus", typeof(float)),
                    ("SettlementOasisBonus", typeof(float)),
                    ("VistaScenicWeight", typeof(float)),
                    ("VistaElevationWeight", typeof(float)),
                    ("VistaRarityWeight", typeof(float)),
                    ("CrossingRiverWeight", typeof(float)),
                    ("CrossingTraversabilityWeight", typeof(float)),
                    ("CrossingLandWeight", typeof(float)),
                    ("PassElevationWeight", typeof(float)),
                    ("PassTraversabilityWeight", typeof(float)),
                    ("PassScenicWeight", typeof(float)),
                    ("PassRarityWeight", typeof(float)),
                    ("CoastLandWeight", typeof(float)),
                    ("CoastTraversabilityWeight", typeof(float)),
                    ("CoastScenicWeight", typeof(float)),
                    ("CoastRarityWeight", typeof(float)),
                    ("ResourceMoistureWeight", typeof(float)),
                    ("ResourceTraversabilityWeight", typeof(float)),
                    ("ResourceLowElevationWeight", typeof(float)),
                    ("ResourceRiverWeight", typeof(float)),
                    ("ResourceRarityWeight", typeof(float)),
                    ("AncientScenicWeight", typeof(float)),
                    ("AncientElevationWeight", typeof(float)),
                    ("AncientStableFlatLandWeight", typeof(float)),
                    ("AncientRarityWeight", typeof(float)),
                    ("CanyonScenicWeight", typeof(float)),
                    ("CanyonRiverWeight", typeof(float)),
                    ("CanyonElevationWeight", typeof(float)),
                    ("CanyonRarityWeight", typeof(float)),
                    ("OasisNaturalResourceWeight", typeof(float)),
                    ("OasisNaturalTraversabilityWeight", typeof(float)),
                    ("OasisNaturalRiverWeight", typeof(float)),
                    ("OasisNaturalScenicWeight", typeof(float)),
                    ("OasisNaturalRarityWeight", typeof(float)),
                    ("OasisStrategicWaterAccessWeight", typeof(float)),
                    ("OasisStrategicResourceWeight", typeof(float)),
                    ("OasisStrategicTraversabilityWeight", typeof(float)),
                    ("OasisStrategicScenicWeight", typeof(float)),
                    ("OasisStrategicRarityWeight", typeof(float)),
                    ("CandidateRarityLift", typeof(float)),
                    ("MinPerKindLimit", typeof(int)),
                    ("PerKindLimitRatio", typeof(float)),
                    ("MinDistanceCellMultiplier", typeof(float)),
                    ("MinDistanceChunkMultiplier", typeof(float)),
                    ("RequiredKindDistanceFactor", typeof(float)),
                    ("KindSweepDistanceFactor", typeof(float)),
                    ("CoverageAnchorTargetRatio", typeof(float)),
                    ("CoverageGainWeight", typeof(float)),
                    ("DistanceNoveltyWeight", typeof(float)),
                    ("CandidateScoreWeight", typeof(float)),
                    ("ExoticBiomeBonus", typeof(float)),
                    ("TownThreshold", typeof(float)),
                    ("TownCandidateScoreWeight", typeof(float)),
                    ("TownTraversabilityWeight", typeof(float)),
                    ("TownResourceWeight", typeof(float)),
                    ("TownScenicWeight", typeof(float)),
                    ("TownBiomeWeight", typeof(float)),
                    ("PlainsBiomeScore", typeof(float)),
                    ("GrasslandBiomeScore", typeof(float)),
                    ("OasisBiomeScore", typeof(float)),
                    ("ForestBiomeScore", typeof(float)),
                    ("CoastBiomeScore", typeof(float)),
                    ("FallbackBiomeScore", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainRouteRuleSet>(
                [
                    ("SecondaryMinDistanceChunks", typeof(float)),
                    ("SecondaryIdealDistanceChunks", typeof(float)),
                    ("SecondaryMaxDistanceChunks", typeof(float)),
                    ("SecondaryMinCandidateTests", typeof(int)),
                    ("SecondaryCandidateTestMultiplier", typeof(int)),
                    ("SettlementMinDistanceChunks", typeof(float)),
                    ("SettlementIdealDistanceChunks", typeof(float)),
                    ("SettlementMaxDistanceChunks", typeof(float)),
                    ("SettlementMinCandidateTests", typeof(int)),
                    ("SettlementCandidateTestMultiplier", typeof(int)),
                    ("MinimumSettlementConnectorRoutes", typeof(int)),
                    ("SettlementEndpointWeight", typeof(float)),
                    ("SettlementScenicWeight", typeof(float)),
                    ("SettlementTraversalWeight", typeof(float)),
                    ("SettlementUnderConnectedWeight", typeof(float)),
                    ("SettlementKindVarietyWeight", typeof(float)),
                    ("SettlementTierImportanceWeight", typeof(float)),
                    ("SettlementTierVarietyWeight", typeof(float)),
                    ("SettlementDistanceWeight", typeof(float)),
                    ("SettlementBonusWeight", typeof(float)),
                    ("SecondaryEndpointWeight", typeof(float)),
                    ("SecondaryScenicWeight", typeof(float)),
                    ("SecondaryTraversalWeight", typeof(float)),
                    ("SecondaryUnderConnectedWeight", typeof(float)),
                    ("SecondaryKindVarietyWeight", typeof(float)),
                    ("SecondaryTierImportanceWeight", typeof(float)),
                    ("SecondaryTierVarietyWeight", typeof(float)),
                    ("SecondaryDistanceWeight", typeof(float)),
                    ("SecondarySettlementBonusWeight", typeof(float)),
                    ("ImpassableWaterDepthHeightScaleRatio", typeof(float)),
                    ("DiagonalBaseCost", typeof(float)),
                    ("OrthogonalBaseCost", typeof(float)),
                    ("TraversabilityPenaltyWeight", typeof(float)),
                    ("HeightDeltaPenaltyHeightScaleRatio", typeof(float)),
                    ("HeightDeltaPenaltyMax", typeof(float)),
                    ("RiverHighPenaltyThreshold", typeof(float)),
                    ("RiverHighPenalty", typeof(float)),
                    ("RiverPenaltyWeight", typeof(float)),
                    ("WaterPenaltyStart", typeof(float)),
                    ("WaterPenaltyBase", typeof(float)),
                    ("WaterPenaltyDepthScale", typeof(float)),
                    ("WaterPenaltyDepthMax", typeof(float)),
                    ("ScenicBonusWeight", typeof(float)),
                    ("MinimumScaledCost", typeof(float)),
                    ("WaterPathThreshold", typeof(float)),
                    ("CoastPathThreshold", typeof(float)),
                    ("RiverRoadPrimaryThreshold", typeof(float)),
                    ("RidgePassPrimaryThreshold", typeof(float)),
                    ("ScenicTrailThreshold", typeof(float)),
                    ("RiverRoadSecondaryThreshold", typeof(float)),
                    ("RidgePassSecondaryThreshold", typeof(float))
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
            CheckPublicShape<TerrainPlacementCandidate>(
                [
                    ("WorldPosition", typeof(Vector2)),
                    ("Height", typeof(float)),
                    ("Score", typeof(float)),
                    ("Tags", typeof(TerrainGameplayTags)),
                    ("Traversal", typeof(TerrainTraversalCost)),
                    ("Water", typeof(TerrainWaterState)),
                    ("RouteCorridor", typeof(TerrainRouteCorridorSample)),
                    ("NearRoute", typeof(bool))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainRouteGraphNode>(
                [
                    ("PointId", typeof(int)),
                    ("WorldPosition", typeof(Vector2)),
                    ("Kind", typeof(TerrainPointOfInterestKind)),
                    ("SettlementTier", typeof(TerrainSettlementTier)),
                    ("Score", typeof(float))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainRouteGraphEdge>(
                [
                    ("FromPointId", typeof(int)),
                    ("ToPointId", typeof(int)),
                    ("Kind", typeof(TerrainRouteKind)),
                    ("Cost", typeof(float)),
                    ("AverageScenicPotential", typeof(float)),
                    ("AverageTraversability", typeof(float)),
                    ("CoreWidth", typeof(float)),
                    ("ShoulderWidth", typeof(float)),
                    ("Waypoints", typeof(Vector2[]))
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicShape<TerrainRouteGraphSnapshot>(
                [
                    ("Center", typeof(Vector2)),
                    ("WorldSize", typeof(float)),
                    ("Nodes", typeof(TerrainRouteGraphNode[])),
                    ("Edges", typeof(TerrainRouteGraphEdge[]))
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
            CheckPublicMethods(
                typeof(TerrainScatterRuleSet),
                [
                    new("StableHash", false, typeof(string), [])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainSettlementVisualRuleSet),
                [
                    new("StableHash", false, typeof(string), [])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainPointOfInterestRuleSet),
                [
                    new("StableHash", false, typeof(string), [])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(TerrainRouteRuleSet),
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
                    new("QueryNearestPointsOfInterest", false, typeof(TerrainWorldPointOfInterestSummary[]), [typeof(Vector2), typeof(float), typeof(int), typeof(TerrainPointOfInterestKind?)]),
                    new("TryFindNearestPointOfInterest", false, typeof(bool), [typeof(Vector2), typeof(float), typeof(TerrainPointOfInterestKind?), typeof(TerrainWorldPointOfInterest).MakeByRefType()]),
                    new("QueryPointsOfInterest", false, typeof(TerrainWorldPointOfInterest[]), [typeof(Rect2), typeof(TerrainPointOfInterestKind?)]),
                    new("QueryGameplayTagRegions", false, typeof(TerrainGameplayTagRegionSummary[]), [typeof(Rect2), typeof(TerrainGameplayTag), typeof(TerrainGameplayTag), typeof(int)]),
                    new("QueryRoutesNear", false, typeof(TerrainWorldRoute[]), [typeof(Vector2), typeof(float)]),
                    new("QueryRouteSummariesNear", false, typeof(TerrainWorldRouteSummary[]), [typeof(Vector2), typeof(float), typeof(int)]),
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
                typeof(ITerrainPlacementService),
                [
                    new("QueryPlacementCandidates", false, typeof(TerrainPlacementCandidate[]), [typeof(Rect2), typeof(TerrainGameplayTag), typeof(TerrainGameplayTag), typeof(int), typeof(float), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(float)])
                ],
                ref checkedTypeCount,
                ref checkedMemberCount,
                out failureReason) &&
            CheckPublicMethods(
                typeof(ITerrainNavigationProvider),
                [
                    new("CreateTraversalCostGrid", false, typeof(TerrainTraversalCostGrid), [typeof(Vector2), typeof(float), typeof(int), typeof(float)]),
                    new("GetRouteGraphSnapshot", false, typeof(TerrainRouteGraphSnapshot), []),
                    new("TryGetRouteGraphSnapshot", false, typeof(bool), [typeof(TerrainRouteGraphSnapshot).MakeByRefType()])
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
                    new("QueryNearestPointsOfInterest", false, typeof(TerrainWorldPointOfInterestSummary[]), [typeof(Vector2), typeof(float), typeof(int), typeof(TerrainPointOfInterestKind?)]),
                    new("TryFindNearestPointOfInterest", false, typeof(bool), [typeof(Vector2), typeof(float), typeof(TerrainPointOfInterestKind?), typeof(TerrainWorldPointOfInterest).MakeByRefType()]),
                    new("QueryPointsOfInterest", false, typeof(TerrainWorldPointOfInterest[]), [typeof(Rect2), typeof(TerrainPointOfInterestKind?)]),
                    new("QueryGameplayTagRegions", false, typeof(TerrainGameplayTagRegionSummary[]), [typeof(Rect2), typeof(TerrainGameplayTag), typeof(TerrainGameplayTag), typeof(int)]),
                    new("QueryRoutesNear", false, typeof(TerrainWorldRoute[]), [typeof(Vector2), typeof(float)]),
                    new("QueryRouteSummariesNear", false, typeof(TerrainWorldRouteSummary[]), [typeof(Vector2), typeof(float), typeof(int)]),
                    new("SampleRouteCorridor", false, typeof(TerrainRouteCorridorSample), [typeof(Vector2)]),
                    new("SampleWaterState", false, typeof(TerrainWaterState), [typeof(Vector2)]),
                    new("SampleGameplayTags", false, typeof(TerrainGameplayTags), [typeof(Vector2)]),
                    new("SampleTraversalCost", false, typeof(TerrainTraversalCost), [typeof(Vector2), typeof(float)]),
                    new("IsTraversable", false, typeof(bool), [typeof(Vector2), typeof(float)]),
                    new("IsAboveWater", false, typeof(bool), [typeof(Vector2), typeof(float)]),
                    new("QueryPlacementCandidates", false, typeof(TerrainPlacementCandidate[]), [typeof(Rect2), typeof(TerrainGameplayTag), typeof(TerrainGameplayTag), typeof(int), typeof(float), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(float)]),
                    new("CreateTraversalCostGrid", false, typeof(TerrainTraversalCostGrid), [typeof(Vector2), typeof(float), typeof(int), typeof(float)]),
                    new("GetRouteGraphSnapshot", false, typeof(TerrainRouteGraphSnapshot), []),
                    new("TryGetRouteGraphSnapshot", false, typeof(bool), [typeof(TerrainRouteGraphSnapshot).MakeByRefType()]),
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
        "Dao.Terrain.Generation.TerrainGameplayTagRegionSummary",
        "Dao.Terrain.Generation.TerrainGameplayTags",
        "Dao.Terrain.Generation.TerrainLandmarkData",
        "Dao.Terrain.Generation.TerrainLandmarkKind",
        "Dao.Terrain.Generation.TerrainLandscapeKind",
        "Dao.Terrain.Generation.TerrainMapExporter",
        "Dao.Terrain.Generation.TerrainMapLayer",
        "Dao.Terrain.Generation.TerrainMapRaster",
        "Dao.Terrain.Generation.TerrainMapSample",
        "Dao.Terrain.Generation.TerrainPlacementCandidate",
        "Dao.Terrain.Generation.TerrainPointOfInterestIndex",
        "Dao.Terrain.Generation.TerrainPointOfInterestKind",
        "Dao.Terrain.Generation.TerrainQualityAnalyzer",
        "Dao.Terrain.Generation.TerrainQualityGateResult",
        "Dao.Terrain.Generation.TerrainQualityReport",
        "Dao.Terrain.Generation.TerrainQualityThresholds",
        "Dao.Terrain.Generation.TerrainRouteCorridorIndex",
        "Dao.Terrain.Generation.TerrainRouteCorridorSample",
        "Dao.Terrain.Generation.TerrainRouteCorridorSegment",
        "Dao.Terrain.Generation.TerrainRouteGraphEdge",
        "Dao.Terrain.Generation.TerrainRouteGraphNode",
        "Dao.Terrain.Generation.TerrainRouteGraphSnapshot",
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
        "Dao.Terrain.Generation.TerrainWorldPointOfInterestSummary",
        "Dao.Terrain.Generation.TerrainWorldRegion",
        "Dao.Terrain.Generation.TerrainWorldRegionKind",
        "Dao.Terrain.Generation.TerrainWorldRoute",
        "Dao.Terrain.Generation.TerrainWorldRouteSummary",
        "Dao.Terrain.ITerrainNavigationProvider",
        "Dao.Terrain.ITerrainPlanProvider",
        "Dao.Terrain.ITerrainPlacementService",
        "Dao.Terrain.ITerrainQueryService",
        "Dao.Terrain.ITerrainStreamingDiagnostics",
        "Dao.Terrain.TerrainGameplaySettingsResource",
        "Dao.Terrain.TerrainNaturalLandmarkRuleResource",
        "Dao.Terrain.TerrainPointOfInterestRuleSet",
        "Dao.Terrain.Rendering.TerrainMaterialFactory",
        "Dao.Terrain.Rendering.TerrainMeshBuilder",
        "Dao.Terrain.TerrainRenderingSettingsResource",
        "Dao.Terrain.TerrainRouteRuleSet",
        "Dao.Terrain.TerrainScatterRuleSet",
        "Dao.Terrain.TerrainScatterVariantRuleResource",
        "Dao.Terrain.TerrainSettlementVisualRuleSet",
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
            noPlanWorld.QueryNearestPointsOfInterest(query, profile.ChunkSize, 4).Length == 0 &&
            !noPlanWorld.TryFindNearestPointOfInterest(query, profile.ChunkSize, kind: null, out _) &&
            noPlanWorld.QueryPointsOfInterest(new Rect2(query - new Vector2(1.0f, 1.0f), new Vector2(2.0f, 2.0f))).Length == 0 &&
            noPlanWorld.QueryGameplayTagRegions(
                new Rect2(query - new Vector2(1.0f, 1.0f), new Vector2(2.0f, 2.0f)),
                TerrainGameplayTag.Traversable,
                TerrainGameplayTag.None,
                4).Length == 0 &&
            noPlanWorld.QueryRoutesNear(query, profile.ChunkSize).Length == 0 &&
            noPlanWorld.QueryRouteSummariesNear(query, profile.ChunkSize, 4).Length == 0 &&
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
        Rect2 placementBounds = new(
            query - new Vector2(profile.ChunkSize * 0.5f, profile.ChunkSize * 0.5f),
            new Vector2(profile.ChunkSize, profile.ChunkSize));
        TerrainPlacementCandidate[] noPlanPlacementCandidates = noPlanWorld.QueryPlacementCandidates(
            placementBounds,
            TerrainGameplayTag.Traversable,
            TerrainGameplayTag.None,
            maxCandidates: 8,
            sampleSpacing: 24.0f,
            minTraversability: 0.45f,
            maxTraversalCost: 3.0f,
            maxHazardPotential: 1.0f,
            requireRouteInfluence: false,
            minRouteInfluence: 0.0f);
        bool placementCandidatesQueryPassed =
            noPlanPlacementCandidates.Length > 0 &&
            PlacementCandidatesMatchFilters(
                noPlanPlacementCandidates,
                TerrainGameplayTag.Traversable,
                TerrainGameplayTag.None,
                minTraversability: 0.45f,
                maxTraversalCost: 3.0f,
                maxHazardPotential: 1.0f,
                requireRouteInfluence: false,
                minRouteInfluence: 0.0f);
        bool noPlanRoutePlacementPassed = noPlanWorld.QueryPlacementCandidates(
            placementBounds,
            TerrainGameplayTag.Traversable,
            TerrainGameplayTag.None,
            maxCandidates: 8,
            sampleSpacing: 24.0f,
            minTraversability: 0.30f,
            maxTraversalCost: 8.0f,
            maxHazardPotential: 1.0f,
            requireRouteInfluence: true,
            minRouteInfluence: 0.05f).Length == 0;
        TerrainTraversalCostGrid expectedTraversalGrid =
            TerrainMapExporter.CreateTraversalCostGrid(profile, query, profile.ChunkSize, 8, 16.0f);
        TerrainTraversalCostGrid facadeTraversalGrid = noPlanWorld.CreateTraversalCostGrid(query, profile.ChunkSize, 8, 16.0f);
        bool navigationGridPassed =
            TraversalCostGridMatches(expectedTraversalGrid, facadeTraversalGrid) &&
            TraversalCostGridSnapshotIsolated(facadeTraversalGrid);
        bool noPlanRouteGraphPassed =
            !noPlanWorld.TryGetRouteGraphSnapshot(out TerrainRouteGraphSnapshot? noPlanRouteGraph) &&
            noPlanRouteGraph is null &&
            noPlanWorld.GetRouteGraphSnapshot().Nodes.Length == 0 &&
            noPlanWorld.GetRouteGraphSnapshot().Edges.Length == 0;
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
            TerrainApiVersion.Minor == 3 &&
            TerrainApiVersion.Patch == 0 &&
            string.Equals(TerrainApiVersion.Contract, "terrain-api-v1", StringComparison.Ordinal) &&
            string.Equals(TerrainApiVersion.Version, "1.3.0", StringComparison.Ordinal);
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
        bool pointSummaryQueryPassed = plan.PointsOfInterest.Length == 0;
        bool gameplayTagRegionQueryPassed = plan.Regions.Length == 0;
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

            TerrainWorldPointOfInterestSummary[] pointSummaries = planWorld.QueryNearestPointsOfInterest(
                expectedPoint.WorldPosition,
                radius: 0.01f,
                maxResults: 1,
                expectedPoint.Kind);
            bool pointSummaryMatches =
                pointSummaries.Length == 1 &&
                ContainsPointOfInterestSummary(pointSummaries, expectedPoint, expectedDistance: 0.0f);
            if (pointSummaryMatches)
            {
                pointSummaries[0] = default;
                TerrainWorldPointOfInterestSummary[] secondPointSummaries = planWorld.QueryNearestPointsOfInterest(
                    expectedPoint.WorldPosition,
                    radius: 0.01f,
                    maxResults: 1,
                    expectedPoint.Kind);
                pointSummaryMatches =
                    secondPointSummaries.Length == 1 &&
                    ContainsPointOfInterestSummary(secondPointSummaries, expectedPoint, expectedDistance: 0.0f);
            }

            pointSummaryQueryPassed = pointSummaryMatches;
        }

        if (plan.Regions.Length > 0)
        {
            foreach (TerrainWorldRegion expectedRegion in plan.Regions)
            {
                TerrainGameplayTag expectedFlags = ComputeExpectedGameplayTagFlags(expectedRegion, profile);
                if (expectedFlags == TerrainGameplayTag.None)
                {
                    continue;
                }

                Rect2 regionBounds = new(
                    expectedRegion.WorldPosition - new Vector2(1.0f, 1.0f),
                    new Vector2(2.0f, 2.0f));
                TerrainGameplayTagRegionSummary[] regionSummaries = planWorld.QueryGameplayTagRegions(
                    regionBounds,
                    expectedFlags,
                    TerrainGameplayTag.None,
                    maxResults: 4);
                bool regionMatches =
                    regionSummaries.Length == 1 &&
                    ContainsGameplayTagRegionSummary(regionSummaries, plan, expectedRegion, expectedFlags);
                if (regionMatches)
                {
                    regionSummaries[0] = default;
                    TerrainGameplayTagRegionSummary[] secondRegionSummaries = planWorld.QueryGameplayTagRegions(
                        regionBounds,
                        expectedFlags,
                        TerrainGameplayTag.None,
                        maxResults: 4);
                    regionMatches =
                        secondRegionSummaries.Length == 1 &&
                        ContainsGameplayTagRegionSummary(secondRegionSummaries, plan, expectedRegion, expectedFlags);
                }

                gameplayTagRegionQueryPassed = regionMatches;
                if (gameplayTagRegionQueryPassed)
                {
                    break;
                }
            }
        }

        bool routeQueryPassed = plan.Routes.Length == 0;
        bool routeSummaryQueryPassed = plan.Routes.Length == 0;
        bool routeCorridorQueryPassed = plan.Routes.Length == 0;
        bool routeGraphSnapshotPassed = true;
        bool routeGraphSnapshotIsolated = true;
        bool routePlacementQueryPassed = plan.Routes.Length == 0;
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

            TerrainWorldRouteSummary[] routeSummaries = planWorld.QueryRouteSummariesNear(routeQueryPoint, radius: 1.0f, maxResults: 8);
            bool routeSummaryMatches =
                routeSummaries.Length > 0 &&
                routeSummaries.Length <= 8 &&
                ContainsRouteSummary(routeSummaries, expectedRoute, expectedDistance: 0.0f);
            if (routeSummaryMatches)
            {
                routeSummaries[0] = default;
                TerrainWorldRouteSummary[] secondRouteSummaries = planWorld.QueryRouteSummariesNear(routeQueryPoint, radius: 1.0f, maxResults: 8);
                routeSummaryMatches =
                    secondRouteSummaries.Length > 0 &&
                    secondRouteSummaries.Length <= 8 &&
                    ContainsRouteSummary(secondRouteSummaries, expectedRoute, expectedDistance: 0.0f);
            }

            routeSummaryQueryPassed = routeSummaryMatches;

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

            routePlacementQueryPassed = false;
            foreach (TerrainWorldRoute route in plan.Routes)
            {
                if (route.Waypoints.Length < 2)
                {
                    continue;
                }

                Vector2 midpoint = route.Waypoints[0].Lerp(route.Waypoints[1], 0.5f);
                Rect2 routeBounds = new(
                    midpoint - new Vector2(profile.ChunkSize * 0.25f, profile.ChunkSize * 0.25f),
                    new Vector2(profile.ChunkSize * 0.5f, profile.ChunkSize * 0.5f));
                TerrainPlacementCandidate[] routePlacements = planWorld.QueryPlacementCandidates(
                    routeBounds,
                    TerrainGameplayTag.Traversable,
                    TerrainGameplayTag.None,
                    maxCandidates: 8,
                    sampleSpacing: 16.0f,
                    minTraversability: 0.30f,
                    maxTraversalCost: 8.0f,
                    maxHazardPotential: 1.0f,
                    requireRouteInfluence: true,
                    minRouteInfluence: 0.05f);
                if (routePlacements.Length == 0)
                {
                    continue;
                }

                routePlacementQueryPassed =
                    PlacementCandidatesMatchFilters(
                        routePlacements,
                        TerrainGameplayTag.Traversable,
                        TerrainGameplayTag.None,
                        minTraversability: 0.30f,
                        maxTraversalCost: 8.0f,
                        maxHazardPotential: 1.0f,
                        requireRouteInfluence: true,
                        minRouteInfluence: 0.05f);
                if (routePlacementQueryPassed)
                {
                    TerrainPlacementCandidate originalPlacement = routePlacements[0];
                    routePlacements[0] = default;
                    TerrainPlacementCandidate[] secondPlacements = planWorld.QueryPlacementCandidates(
                        routeBounds,
                        TerrainGameplayTag.Traversable,
                        TerrainGameplayTag.None,
                        maxCandidates: 8,
                        sampleSpacing: 16.0f,
                        minTraversability: 0.30f,
                        maxTraversalCost: 8.0f,
                        maxHazardPotential: 1.0f,
                        requireRouteInfluence: true,
                        minRouteInfluence: 0.05f);
                    routePlacementQueryPassed = ContainsPlacementCandidate(secondPlacements, originalPlacement);
                }

                break;
            }

            routeGraphSnapshotPassed =
                planWorld.TryGetRouteGraphSnapshot(out TerrainRouteGraphSnapshot? routeGraphSnapshot) &&
                routeGraphSnapshot is not null &&
                RouteGraphMatchesPlan(routeGraphSnapshot, plan);
            if (routeGraphSnapshotPassed && routeGraphSnapshot is not null)
            {
                routeGraphSnapshotIsolated = RouteGraphSnapshotIsolated(planWorld, routeGraphSnapshot, plan);
            }
            else
            {
                routeGraphSnapshotIsolated = false;
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
            placementCandidatesQueryPassed &&
            noPlanRoutePlacementPassed &&
            navigationGridPassed &&
            noPlanRouteGraphPassed &&
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
            pointSummaryQueryPassed &&
            gameplayTagRegionQueryPassed &&
            routeQueryPassed &&
            routeSummaryQueryPassed &&
            routeCorridorQueryPassed &&
            routePlacementQueryPassed &&
            routeGraphSnapshotPassed &&
            routeGraphSnapshotIsolated &&
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
                placementCandidatesQueryPassed,
                noPlanRoutePlacementPassed,
                navigationGridPassed,
                noPlanRouteGraphPassed,
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
                pointSummaryQueryPassed,
                gameplayTagRegionQueryPassed,
                routeQueryPassed,
                routeSummaryQueryPassed,
                routeCorridorQueryPassed,
                routePlacementQueryPassed,
                routeGraphSnapshotPassed,
                routeGraphSnapshotIsolated,
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
            placementCandidatesQueryPassed,
            noPlanRoutePlacementPassed,
            navigationGridPassed,
            noPlanRouteGraphPassed,
            streamingSnapshotPassed,
            apiVersionPassed,
            determinismContractPassed,
            performanceContractPassed,
            integrationInterfacesPassed,
            signalContractsPassed,
            pointQueryPassed,
            pointSummaryQueryPassed,
            gameplayTagRegionQueryPassed,
            routeQueryPassed,
            routeSummaryQueryPassed,
            routeCorridorQueryPassed,
            routePlacementQueryPassed,
            routeGraphSnapshotPassed,
            routeGraphSnapshotIsolated,
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
            PlacementCandidatesQueryPassed: false,
            NoPlanRoutePlacementPassed: false,
            NavigationGridPassed: false,
            NoPlanRouteGraphPassed: false,
            StreamingSnapshotPassed: false,
            ApiVersionPassed: false,
            DeterminismContractPassed: false,
            PerformanceContractPassed: false,
            IntegrationInterfacesPassed: false,
            SignalContractsPassed: false,
            PointQueryPassed: false,
            PointSummaryQueryPassed: false,
            GameplayTagRegionQueryPassed: false,
            RouteQueryPassed: false,
            RouteSummaryQueryPassed: false,
            RouteCorridorQueryPassed: false,
            RoutePlacementQueryPassed: false,
            RouteGraphSnapshotPassed: false,
            RouteGraphSnapshotIsolated: false,
            PointSnapshotIsolated: false,
            RouteSnapshotIsolated: false,
            WorldPlanSnapshotIsolated: false,
            Reason: $"TerrainWorld runtime facade threw {ex.GetType().Name}: {ex.Message}");
    }
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

static bool ContainsPointOfInterestSummary(
    TerrainWorldPointOfInterestSummary[] points,
    TerrainWorldPointOfInterest expected,
    float expectedDistance)
{
    foreach (TerrainWorldPointOfInterestSummary point in points)
    {
        if (point.Id == expected.Id &&
            point.Kind == expected.Kind &&
            ExactPositionEquals(point.WorldPosition, expected.WorldPosition) &&
            ExactFloatEquals(point.Distance, expectedDistance) &&
            ExactFloatEquals(point.Score, expected.Score) &&
            ExactFloatEquals(point.ScenicPotential, expected.ScenicPotential) &&
            ExactFloatEquals(point.Traversability, expected.Traversability) &&
            point.SettlementTier == expected.SettlementTier &&
            point.BiomeKind == expected.BiomeKind &&
            point.LandscapeKind == expected.LandscapeKind)
        {
            return true;
        }
    }

    return false;
}

static bool ContainsGameplayTagRegionSummary(
    TerrainGameplayTagRegionSummary[] regions,
    TerrainWorldPlan plan,
    TerrainWorldRegion expected,
    TerrainGameplayTag expectedFlags)
{
    Rect2 expectedBounds = ComputeExpectedGameplayTagRegionBounds(plan, expected);
    foreach (TerrainGameplayTagRegionSummary region in regions)
    {
        if (region.GridX == expected.GridX &&
            region.GridY == expected.GridY &&
            ExactPositionEquals(region.WorldPosition, expected.WorldPosition) &&
            ExactRectEquals(region.WorldBounds, expectedBounds) &&
            region.Flags == expectedFlags &&
            region.BiomeKind == expected.BiomeKind &&
            region.LandscapeKind == expected.LandscapeKind &&
            region.RegionKind == expected.RegionKind &&
            ExactFloatEquals(region.Traversability, expected.Traversability) &&
            ExactFloatEquals(region.ScenicPotential, expected.ScenicPotential) &&
            ExactFloatEquals(region.ResourcePotential, expected.ResourcePotential) &&
            ExactFloatEquals(region.HazardPotential, expected.HazardPotential) &&
            ExactFloatEquals(region.EncounterPotential, expected.EncounterPotential))
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

static bool ContainsRouteSummary(
    TerrainWorldRouteSummary[] routes,
    TerrainWorldRoute expected,
    float expectedDistance)
{
    foreach (TerrainWorldRouteSummary route in routes)
    {
        if (route.FromPointId == expected.FromPointId &&
            route.ToPointId == expected.ToPointId &&
            route.Kind == expected.Kind &&
            ExactFloatEquals(route.Distance, expectedDistance) &&
            ExactFloatEquals(route.Cost, expected.Cost) &&
            ExactFloatEquals(route.AverageScenicPotential, expected.AverageScenicPotential) &&
            ExactFloatEquals(route.AverageTraversability, expected.AverageTraversability) &&
            route.WaypointCount == expected.Waypoints.Length)
        {
            return true;
        }
    }

    return false;
}

static bool PlacementCandidatesMatchFilters(
    TerrainPlacementCandidate[] candidates,
    TerrainGameplayTag requiredTags,
    TerrainGameplayTag excludedTags,
    float minTraversability,
    float maxTraversalCost,
    float maxHazardPotential,
    bool requireRouteInfluence,
    float minRouteInfluence)
{
    foreach (TerrainPlacementCandidate candidate in candidates)
    {
        TerrainGameplayTags tags = candidate.Tags;
        TerrainTraversalCost traversal = candidate.Traversal;
        TerrainRouteCorridorSample route = candidate.RouteCorridor;

        if (requiredTags != TerrainGameplayTag.None &&
            (tags.Flags & requiredTags) != requiredTags)
        {
            return false;
        }

        if (excludedTags != TerrainGameplayTag.None &&
            (tags.Flags & excludedTags) != TerrainGameplayTag.None)
        {
            return false;
        }

        if (traversal.IsBlocked ||
            traversal.Traversability + TerrainDeterminismContract.ExactFloatEpsilon < minTraversability)
        {
            return false;
        }

        if (!float.IsPositiveInfinity(traversal.Cost) &&
            traversal.Cost - TerrainDeterminismContract.ExactFloatEpsilon > maxTraversalCost)
        {
            return false;
        }

        if (candidate.Tags.HazardPotential - TerrainDeterminismContract.ExactFloatEpsilon > maxHazardPotential)
        {
            return false;
        }

        if (requireRouteInfluence &&
            (!route.HasInfluence ||
                route.Influence + TerrainDeterminismContract.ExactFloatEpsilon < minRouteInfluence))
        {
            return false;
        }
    }

    return true;
}

static bool ContainsPlacementCandidate(
    TerrainPlacementCandidate[] candidates,
    TerrainPlacementCandidate expected)
{
    foreach (TerrainPlacementCandidate candidate in candidates)
    {
        if (ExactPositionEquals(candidate.WorldPosition, expected.WorldPosition) &&
            ExactFloatEquals(candidate.Height, expected.Height) &&
            ExactFloatEquals(candidate.Score, expected.Score) &&
            TerrainGameplayTagsMatch(candidate.Tags, expected.Tags) &&
            TerrainTraversalCostsMatch(candidate.Traversal, expected.Traversal) &&
            TerrainWaterStatesMatch(candidate.Water, expected.Water) &&
            TerrainRouteCorridorSamplesMatch(candidate.RouteCorridor, expected.RouteCorridor))
        {
            return true;
        }
    }

    return false;
}

static bool RouteGraphMatchesPlan(
    TerrainRouteGraphSnapshot snapshot,
    TerrainWorldPlan plan)
{
    if (!ExactPositionEquals(snapshot.Center, plan.Center) ||
        !ExactFloatEquals(snapshot.WorldSize, plan.WorldSize) ||
        snapshot.Nodes.Length != plan.PointsOfInterest.Length ||
        snapshot.Edges.Length != plan.Routes.Length)
    {
        return false;
    }

    for (int i = 0; i < snapshot.Nodes.Length; i++)
    {
        TerrainRouteGraphNode node = snapshot.Nodes[i];
        TerrainWorldPointOfInterest point = plan.PointsOfInterest[i];
        if (node.PointId != point.Id ||
            !ExactPositionEquals(node.WorldPosition, point.WorldPosition) ||
            node.Kind != point.Kind ||
            node.SettlementTier != point.SettlementTier ||
            !ExactFloatEquals(node.Score, point.Score))
        {
            return false;
        }
    }

    for (int i = 0; i < snapshot.Edges.Length; i++)
    {
        TerrainRouteGraphEdge edge = snapshot.Edges[i];
        TerrainWorldRoute route = plan.Routes[i];
        if (edge.FromPointId != route.FromPointId ||
            edge.ToPointId != route.ToPointId ||
            edge.Kind != route.Kind ||
            !ExactFloatEquals(edge.Cost, route.Cost) ||
            !ExactFloatEquals(edge.AverageScenicPotential, route.AverageScenicPotential) ||
            !ExactFloatEquals(edge.AverageTraversability, route.AverageTraversability) ||
            edge.Waypoints.Length != route.Waypoints.Length)
        {
            return false;
        }

        for (int waypointIndex = 0; waypointIndex < edge.Waypoints.Length; waypointIndex++)
        {
            if (!ExactPositionEquals(edge.Waypoints[waypointIndex], route.Waypoints[waypointIndex]))
            {
                return false;
            }
        }
    }

    return true;
}

static bool RouteGraphSnapshotIsolated(
    TerrainWorld planWorld,
    TerrainRouteGraphSnapshot snapshot,
    TerrainWorldPlan plan)
{
    bool isolated = snapshot.Nodes.Length == plan.PointsOfInterest.Length &&
        snapshot.Edges.Length == plan.Routes.Length;
    if (!isolated)
    {
        return false;
    }

    if (snapshot.Nodes.Length > 0)
    {
        TerrainRouteGraphNode originalNode = snapshot.Nodes[0];
        snapshot.Nodes[0] = originalNode with { PointId = originalNode.PointId + 1_000_000 };
        TerrainRouteGraphSnapshot secondSnapshot = planWorld.GetRouteGraphSnapshot();
        isolated =
            secondSnapshot.Nodes.Length == plan.PointsOfInterest.Length &&
            secondSnapshot.Nodes[0].PointId == plan.PointsOfInterest[0].Id &&
            secondSnapshot.Nodes[0].Kind == plan.PointsOfInterest[0].Kind;
    }

    if (!isolated || snapshot.Edges.Length == 0)
    {
        return isolated;
    }

    TerrainRouteGraphEdge originalEdge = snapshot.Edges[0];
    snapshot.Edges[0] = originalEdge with { FromPointId = originalEdge.FromPointId + 1_000_000 };
    TerrainRouteGraphSnapshot edgeSnapshot = planWorld.GetRouteGraphSnapshot();
    isolated =
        edgeSnapshot.Edges.Length == plan.Routes.Length &&
        edgeSnapshot.Edges[0].FromPointId == plan.Routes[0].FromPointId &&
        edgeSnapshot.Edges[0].ToPointId == plan.Routes[0].ToPointId;

    if (!isolated || originalEdge.Waypoints.Length == 0)
    {
        return isolated;
    }

    Vector2 originalWaypoint = originalEdge.Waypoints[0];
    snapshot.Edges[0].Waypoints[0] = originalWaypoint + new Vector2(4444.0f, -4444.0f);
    TerrainRouteGraphSnapshot waypointSnapshot = planWorld.GetRouteGraphSnapshot();
    return waypointSnapshot.Edges.Length == plan.Routes.Length &&
        waypointSnapshot.Edges[0].Waypoints.Length == plan.Routes[0].Waypoints.Length &&
        ExactPositionEquals(waypointSnapshot.Edges[0].Waypoints[0], originalWaypoint);
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
    bool placementCandidatesQueryPassed,
    bool noPlanRoutePlacementPassed,
    bool navigationGridPassed,
    bool noPlanRouteGraphPassed,
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
    bool pointSummaryQueryPassed,
    bool gameplayTagRegionQueryPassed,
    bool routeQueryPassed,
    bool routeSummaryQueryPassed,
    bool routeCorridorQueryPassed,
    bool routePlacementQueryPassed,
    bool routeGraphSnapshotPassed,
    bool routeGraphSnapshotIsolated,
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

    if (!placementCandidatesQueryPassed)
    {
        return "QueryPlacementCandidates did not return candidates matching the requested tag and traversal filters";
    }

    if (!noPlanRoutePlacementPassed)
    {
        return "QueryPlacementCandidates returned route-influenced candidates without a world plan";
    }

    if (!navigationGridPassed)
    {
        return "CreateTraversalCostGrid did not match TerrainMapExporter traversal-cost handoff or isolate samples";
    }

    if (!noPlanRouteGraphPassed)
    {
        return "route graph facade did not expose an empty no-plan state";
    }

    if (!streamingSnapshotPassed)
    {
        return "GetStreamingSnapshot did not expose stable isolated streaming diagnostics";
    }

    if (!apiVersionPassed)
    {
        return "TerrainApiVersion constants did not match terrain-api-v1 version 1.3.0";
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

    if (!pointSummaryQueryPassed)
    {
        return "limited POI summary query did not return stable nearest results";
    }

    if (!gameplayTagRegionQueryPassed)
    {
        return "bounded gameplay-tag region query did not return stable isolated region summaries";
    }

    if (!routeQueryPassed)
    {
        return "route semantic query facade did not find nearby routes or isolate waypoint arrays";
    }

    if (!routeSummaryQueryPassed)
    {
        return "limited route summary query did not return stable nearby results";
    }

    if (!routeCorridorQueryPassed)
    {
        return "route corridor semantic facade did not match the planned corridor index";
    }

    if (!routePlacementQueryPassed)
    {
        return "route-influenced placement candidate facade did not respect route corridor filters";
    }

    if (!routeGraphSnapshotPassed)
    {
        return "route graph snapshot facade did not match the assigned open-world plan";
    }

    if (!routeGraphSnapshotIsolated)
    {
        return "route graph snapshot facade exposed mutable waypoint or graph array state";
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
        typeof(ITerrainStreamingDiagnostics).IsAssignableFrom(worldType) &&
        typeof(ITerrainPlacementService).IsAssignableFrom(worldType) &&
        typeof(ITerrainNavigationProvider).IsAssignableFrom(worldType);
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
    SetPrivateField(world, "_tileCache", CreateTileCacheState(coord, probeProfile, terrainFeatureKey));

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
        GetNestedPrivateCollectionCount(world, "_tileCache", "_tileCache") == 0 &&
        GetNestedPrivateCollectionCount(world, "_tileCache", "_tileCacheNodes") == 0 &&
        GetNestedPrivateCollectionCount(world, "_tileCache", "_tileCacheLru") == 0 &&
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

static float ColorDistance(Color a, Color b)
{
    float dr = a.R - b.R;
    float dg = a.G - b.G;
    float db = a.B - b.B;
    float da = a.A - b.A;
    return MathF.Sqrt((dr * dr) + (dg * dg) + (db * db) + (da * da));
}
