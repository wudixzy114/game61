using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Runtime;
using Dao.Terrain.Streaming;
using Godot;

TerrainGenerationProfile profile = CreateDemoProfile();
float worldSize = GetFloatArg(args, "--world-size", 12_288.0f);
int seed = GetIntArg(args, "--seed", profile.Seed);
int seedCount = Math.Max(1, GetIntArg(args, "--seed-count", 1));
int seedStep = Math.Max(1, GetIntArg(args, "--seed-step", 10_007));
bool verbose = HasFlag(args, "--verbose");
bool skipCorridorSmoke = HasFlag(args, "--skip-corridor-smoke");
bool skipRouteScatterSmoke = HasFlag(args, "--skip-route-scatter-smoke");
bool skipPoiTileSmoke = HasFlag(args, "--skip-poi-tile-smoke");
bool skipGameplayScatterSmoke = HasFlag(args, "--skip-gameplay-scatter-smoke");
bool skipBiomeScatterSmoke = HasFlag(args, "--skip-biome-scatter-smoke");
bool skipScenicLandmarkSmoke = HasFlag(args, "--skip-scenic-landmark-smoke");
bool skipArtifactSmoke = HasFlag(args, "--skip-artifact-smoke");
bool skipRuntimeWorldSmoke = HasFlag(args, "--skip-runtime-world-smoke");
bool smokeAllSeeds = HasFlag(args, "--smoke-all-seeds");
bool nativeSmoke = HasFlag(args, "--native-smoke");
bool benchmarkTiles = HasFlag(args, "--benchmark-tiles");
int benchmarkTileCount = Math.Max(1, GetIntArg(args, "--benchmark-tile-count", 48));
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
TerrainRuntimeWorldSmokeReport? runtimeWorldSmokeReport = null;
TerrainNativeSamplerSmokeReport? nativeSmokeReport = null;
TerrainTileBenchmarkReport? tileBenchmarkReport = null;
TerrainGenerationProfile benchmarkProfile = profile with { Seed = seed };
TerrainWorldPlan? benchmarkPlan = null;
const int TileBenchmarkMeasurementPasses = 5;

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

    if (runSeedSmokes && !skipRuntimeWorldSmoke)
    {
        runtimeWorldSmokeReport = ValidateRuntimeWorldPlanMaterialization(seedProfile, worldSize);
        PrintRuntimeWorldSmoke(runtimeWorldSmokeReport.Value);
        RecordAuxiliaryCheck(runtimeWorldSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
    }
}

if (nativeSmoke)
{
    nativeSmokeReport = ValidateNativeSamplerParity(profile);
    PrintNativeSamplerSmoke(nativeSmokeReport.Value);
    RecordAuxiliaryCheck(nativeSmokeReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
}

if (benchmarkTiles && benchmarkPlan is not null)
{
    tileBenchmarkReport = BenchmarkTerrainTiles(benchmarkProfile, benchmarkPlan, benchmarkTileCount);
    PrintTileBenchmark(tileBenchmarkReport.Value);
    RecordAuxiliaryCheck(tileBenchmarkReport.Value.Passed, ref totalFailures, ref auxiliaryCheckCount, ref auxiliaryFailureCount);
}

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
        return new TerrainRouteCorridorSmokeReport(false, profile.Seed, default, 0, 0.0f, 0.0f, 0, "no route with at least two waypoints");
    }

    Vector2 midpoint = route.Waypoints[route.Waypoints.Length / 2];
    TerrainTileCoord coord = TerrainTileCoord.FromWorldPosition(new Vector3(midpoint.X, 0.0f, midpoint.Y), profile.ChunkSize);
    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    TerrainRouteCorridorSegment[] segments = corridorIndex.GetSegments(coord);
    if (segments.Length == 0)
    {
        return new TerrainRouteCorridorSmokeReport(false, profile.Seed, coord, 0, 0.0f, 0.0f, 0, "selected route chunk had no indexed corridor segments");
    }

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
        (maxHeightDelta >= 0.05f || maxColorDelta >= 0.01f);
    string reason = passed
        ? "route corridor affected the generated tile"
        : "route corridor produced no measurable tile change";

    return new TerrainRouteCorridorSmokeReport(
        passed,
        profile.Seed,
        coord,
        segments.Length,
        maxHeightDelta,
        maxColorDelta,
        influencedVertices,
        reason);
}

static void PrintCorridorSmoke(TerrainRouteCorridorSmokeReport report)
{
    Console.WriteLine(
        $"Route corridor tile smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"seed {report.Seed}, tile {report.Coord}, segments {report.SegmentCount}, " +
        $"influenced vertices {report.InfluencedVertexCount}, max height delta {report.MaxHeightDelta:0.000}, " +
        $"max color delta {report.MaxColorDelta:0.000} ({report.Reason})");
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

    bool passed =
        materialized.Count == expected.Count &&
        distinctKinds >= 5 &&
        distinctScatterKinds >= 9 &&
        kindCounts[(int)TerrainLandmarkKind.Village] > 0 &&
        kindCounts[(int)TerrainLandmarkKind.Town] > 0 &&
        scatterKindCounts[(int)TerrainLandmarkKind.Village] >= kindCounts[(int)TerrainLandmarkKind.Village] &&
        scatterKindCounts[(int)TerrainLandmarkKind.Town] >= kindCounts[(int)TerrainLandmarkKind.Town] &&
        settlementInteriorScatterCount >= settlementLandmarkCount * 3 &&
        villageHouseScatterCount > 0 &&
        townBlockScatterCount > 0 &&
        settlementPlazaScatterCount > 0 &&
        villageWellScatterCount > 0 &&
        marketStallScatterCount > 0 &&
        watchTowerScatterCount > 0 &&
        settlementServiceScatterCount >= settlementLandmarkCount &&
        settlementGatewayScatterCount >= settlementLandmarkCount &&
        (kindCounts[(int)TerrainLandmarkKind.OasisHub] == 0 ||
            (oasisCanopyScatterCount > 0 && oasisPoolScatterCount > 0 && oasisGardenScatterCount > 0)) &&
        landmarkScatterCount >= expected.Count &&
        footprintReport.Passed;
    string reason = passed
        ? "planned POIs materialized as tile landmarks with settlement services and road-connected gateways"
        : "planned POIs missing from tile landmark data";

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
        reason);
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
        $"interior scatter {report.SettlementInteriorScatterCount}/{report.SettlementLandmarkCount}, " +
        $"interior kinds H/B/C/P/W {report.VillageHouseScatterCount}/{report.TownBlockScatterCount}/{report.OasisCanopyScatterCount}/{report.SettlementPlazaScatterCount}/{report.OasisPoolScatterCount}, " +
        $"services well/market/tower/garden {report.VillageWellScatterCount}/{report.MarketStallScatterCount}/{report.WatchTowerScatterCount}/{report.OasisGardenScatterCount}, " +
        $"gateways {report.SettlementGatewayScatterCount}, " +
        $"footprint vertices {report.FootprintInfluencedVertexCount}, max footprint delta {report.FootprintMaxHeightDelta:0.000}/{report.FootprintMaxColorDelta:0.000}, " +
        $"layout color vertices {report.LayoutColorVertexCount}, max layout color {report.LayoutMaxColorDelta:0.000}, " +
        $"landmark scatter {report.LandmarkScatterCount} ({report.Reason})");
}

static int SettlementLandmarkCount(Span<int> kindCounts)
{
    return kindCounts[(int)TerrainLandmarkKind.Village] +
        kindCounts[(int)TerrainLandmarkKind.Town] +
        kindCounts[(int)TerrainLandmarkKind.OasisHub];
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

    if (coords.Count == 0)
    {
        return new TerrainBiomeScatterSmokeReport(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "no biome scatter candidate tiles found");
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
    bool passed =
        grassTuftCount > 0 &&
        desertShrubCount > 0 &&
        cactusClusterCount > 0 &&
        reedClusterCount > 0 &&
        snowClumpCount > 0 &&
        alpinePineCount > 0 &&
        coastalPalmCount > 0 &&
        driftwoodCount > 0 &&
        mangroveRootCount > 0 &&
        lakeReedCount > 0 &&
        waterLilyCount > 0 &&
        biomeScatterCount >= 72;
    string reason = passed
        ? "biome surface scatter materialized across plains, desert, wetland, lake, snowfield, coast, island, and alpine terrain"
        : "one or more biome surface scatter kinds did not materialize";

    return new TerrainBiomeScatterSmokeReport(
        passed,
        coords.Count,
        sampledTiles,
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
        reason);
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

        if (snowScore > 0.0f)
        {
            snowCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, snowScore));
        }

        if (coastScore > 0.0f)
        {
            coastCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, coastScore));
        }

        float generalScore = Mathf.Max(Mathf.Max(Mathf.Max(grassScore, desertScore), Mathf.Max(wetlandScore, lakeScore)), Mathf.Max(snowScore, coastScore));
        if (generalScore > 0.0f)
        {
            generalCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, generalScore));
        }
    }

    int categoryQuota = Mathf.Max(8, maxCoords / 7);
    AddSortedCandidateCoords(grassCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(desertCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(wetlandCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(lakeCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(snowCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(coastCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(generalCandidates, profile, coords, maxCoords);
}

static void PrintBiomeScatterSmoke(TerrainBiomeScatterSmokeReport report)
{
    Console.WriteLine(
        $"Biome scatter smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, " +
        $"grass/desert/cactus/reeds/snow/alpine/palms/driftwood/mangrove/lake reeds/lilies " +
        $"{report.GrassTuftCount}/{report.DesertShrubCount}/{report.CactusClusterCount}/{report.ReedClusterCount}/{report.SnowClumpCount}/{report.AlpinePineCount}/{report.CoastalPalmCount}/{report.DriftwoodCount}/{report.MangroveRootCount}/{report.LakeReedCount}/{report.WaterLilyCount}, " +
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

    TerrainWorldPlanArtifactResult export = TerrainWorldPlanExporter.SaveOpenWorldArtifacts(
        plan,
        profile,
        imageSize,
        outputDirectory,
        TerrainMapLayer.Biome);

    string mapFilePath = FileSystemPath(export.MapPath);
    string reportFilePath = FileSystemPath(export.ReportPath);
    bool mapExists = System.IO.File.Exists(mapFilePath);
    bool reportExists = System.IO.File.Exists(reportFilePath);
    long mapBytes = mapExists ? new System.IO.FileInfo(mapFilePath).Length : 0L;
    long reportBytes = reportExists ? new System.IO.FileInfo(reportFilePath).Length : 0L;
    string reportText = reportExists ? System.IO.File.ReadAllText(reportFilePath) : string.Empty;
    bool reportContainsRequiredSections = ReportContainsRequiredArtifactSections(reportText);

    bool mapHasContent =
        distinctColorBuckets >= 24 &&
        nonDarkSampleCount >= 512 &&
        overlayChangedPixels >= Mathf.Max(256, imageSize) &&
        maxOverlayColorDelta >= 0.04f;
    bool filesLookValid =
        export.MapSaveError == Error.Ok &&
        export.ReportSaveError == Error.Ok &&
        mapExists &&
        reportExists &&
        mapBytes >= 4096 &&
        reportBytes >= 2048;
    bool passed =
        export.Passed &&
        filesLookValid &&
        mapHasContent &&
        reportContainsRequiredSections;
    string reason = passed
        ? "open world map and report artifacts exported with visible terrain, routes, and POI overlays"
        : ArtifactFailureReason(export, filesLookValid, mapHasContent, reportContainsRequiredSections);

    return new TerrainArtifactSmokeReport(
        passed,
        outputDirectory,
        export.MapPath,
        export.ReportPath,
        mapBytes,
        reportBytes,
        imageSize,
        distinctColorBuckets,
        nonDarkSampleCount,
        overlayChangedPixels,
        maxOverlayColorDelta,
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

static int QuantizedColorKey(Color color)
{
    int r = Mathf.Clamp(Mathf.RoundToInt(color.R * 15.0f), 0, 15);
    int g = Mathf.Clamp(Mathf.RoundToInt(color.G * 15.0f), 0, 15);
    int b = Mathf.Clamp(Mathf.RoundToInt(color.B * 15.0f), 0, 15);
    return (r << 8) | (g << 4) | b;
}

static bool ReportContainsRequiredArtifactSections(string reportText)
{
    return reportText.Contains("Open World Terrain Plan", StringComparison.Ordinal) &&
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
        $"image {report.ImageSize} px, map {report.MapBytes / 1024.0:0.0} KB, report {report.ReportBytes / 1024.0:0.0} KB, " +
        $"colors {report.DistinctColorBuckets}, overlay pixels {report.OverlayChangedPixels}, " +
        $"max overlay delta {report.MaxOverlayColorDelta:0.000}, sections {(report.ReportContainsRequiredSections ? "yes" : "no")} ({report.Reason})");
    Console.WriteLine($"Artifact paths: {report.MapPath}, {report.ReportPath}");
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
        reason);
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
            a.WorldPosition.DistanceSquaredTo(b.WorldPosition) > 0.01f ||
            Math.Abs(a.Score - b.Score) > 0.0001f)
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
        $"gates Q/P/E/A {(report.QualityGatePassed ? "pass" : "fail")}/{(report.PlanningGatePassed ? "pass" : "fail")}/{(report.ExperienceGatePassed ? "pass" : "fail")}/{(report.ArchetypeGatePassed ? "pass" : "fail")} " +
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

    bool gridPassed = compared == expectedCount && maxDelta <= 1.5f && averageDelta <= 0.25f;
    bool tilePassed =
        tileVertexCount == managedTile.Vertices.Length &&
        tileVertexCount == nativeTile.Vertices.Length &&
        tileMaxHeightDelta <= 1.5f &&
        tileMaxColorDelta <= 0.03f;
    int expectedComparedFieldValues = expectedCount * 15;
    bool fieldGridPassed =
        fieldGridAvailable &&
        fieldGridContainsDerivedData &&
        comparedFieldValues == expectedComparedFieldValues &&
        maxFieldDelta <= 0.015f &&
        averageFieldDelta <= 0.0025f &&
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

    if (coords.Length == 0)
    {
        return new TerrainTileBenchmarkReport(
            false,
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
    long totalVertices = 0;
    long totalIndices = 0;
    long totalScatter = 0;
    long totalLandmarks = 0;
    double heightChecksum = 0.0;

    foreach (TerrainTileCoord coord in coords)
    {
        TerrainTileData data = TerrainTileBuilder.Build(coord, lod: 0, profile, includeCollision: false, corridorIndex, poiIndex);
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
        Math.Max(0, allocatedAfter - allocatedBefore),
        heightChecksum);
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
        $"tiles {report.MeasuredTileCount}/{report.RequestedTileCount}, " +
        $"passes {report.MeasurementPassCount}, native speedup {report.NativeSpeedup:0.00}x, parity tiles {report.ParityTileCount}, " +
        $"max parity delta {report.MaxHeightDelta:0.000}/{report.MaxColorDelta:0.000} ({report.Reason})");
    Console.WriteLine(
        $"Benchmark thresholds: managed <= {report.Thresholds.MaxManagedMillisecondsPerTile:0.00} ms/tile, " +
        $"native <= {report.Thresholds.MaxNativeMillisecondsPerTile:0.00} ms/tile, " +
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
        $"alloc {pass.AllocatedMegabytes:0.00} MB ({pass.AllocatedKilobytesPerTile:0.0} KB/tile), " +
        $"vertices {pass.TotalVertices}, scatter {pass.TotalScatter}, landmarks {pass.TotalLandmarks}");
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
    string Reason);

/// <summary>Reports whether POI footprints produce height and color changes on a sampled tile.</summary>
internal readonly record struct TerrainPoiFootprintSmokeReport(
    bool Passed,
    int InfluencedVertexCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    int LayoutColorVertexCount,
    float LayoutMaxColorDelta);

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
    long MapBytes,
    long ReportBytes,
    int ImageSize,
    int DistinctColorBuckets,
    int NonDarkSampleCount,
    int OverlayChangedPixels,
    float MaxOverlayColorDelta,
    bool ReportContainsRequiredSections,
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
        MaxManagedMillisecondsPerTile: 24.0,
        MaxNativeMillisecondsPerTile: 8.0,
        MaxAllocatedKilobytesPerTile: 320.0,
        MinNativeSpeedup: 1.00,
        MinParityTileCount: 8,
        MinBenchmarkBiomeKinds: 7,
        MinBenchmarkLandscapeKinds: 6,
        MinBenchmarkPointOfInterestTiles: 8,
        MinBenchmarkRouteTiles: 8,
        MinBenchmarkGameplayRichTiles: 12,
        MaxParityHeightDelta: 0.05f,
        MaxParityColorDelta: 0.03f);
}

/// <summary>Measured performance data for a single tile build pass (managed or native).</summary>
internal readonly record struct TerrainTileBenchmarkPass(
    int TileCount,
    long TotalVertices,
    long TotalIndices,
    long TotalScatter,
    long TotalLandmarks,
    double ElapsedMilliseconds,
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
