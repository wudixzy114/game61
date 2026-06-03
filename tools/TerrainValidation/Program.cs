using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Runtime;
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
bool nativeSmoke = HasFlag(args, "--native-smoke");
bool benchmarkTiles = HasFlag(args, "--benchmark-tiles");
int benchmarkTileCount = Math.Max(1, GetIntArg(args, "--benchmark-tile-count", 48));

int failures = 0;
TerrainValidationAggregate aggregate = new();
TerrainRouteCorridorSmokeReport? corridorSmokeReport = null;
TerrainRouteScatterSmokeReport? routeScatterSmokeReport = null;
TerrainPoiTileSmokeReport? poiTileSmokeReport = null;
TerrainGameplayScatterSmokeReport? gameplayScatterSmokeReport = null;
TerrainBiomeScatterSmokeReport? biomeScatterSmokeReport = null;
TerrainScenicLandmarkSmokeReport? scenicLandmarkSmokeReport = null;
TerrainNativeSamplerSmokeReport? nativeSmokeReport = null;
TerrainTileBenchmarkReport? tileBenchmarkReport = null;
TerrainGenerationProfile benchmarkProfile = profile with { Seed = seed };
TerrainWorldPlan? benchmarkPlan = null;

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
        failures++;
    }

    PrintSeedResult(result, seedCount == 1 || verbose);

    if (i == 0 && !skipCorridorSmoke)
    {
        corridorSmokeReport = ValidateRouteCorridorTileEffect(seedProfile, result.Plan);
        PrintCorridorSmoke(corridorSmokeReport.Value);
        if (!corridorSmokeReport.Value.Passed)
        {
            failures++;
        }
    }

    if (i == 0 && !skipRouteScatterSmoke)
    {
        routeScatterSmokeReport = ValidateRouteScatterMaterialization(seedProfile, result.Plan);
        PrintRouteScatterSmoke(routeScatterSmokeReport.Value);
        if (!routeScatterSmokeReport.Value.Passed)
        {
            failures++;
        }
    }

    if (i == 0 && !skipPoiTileSmoke)
    {
        poiTileSmokeReport = ValidatePoiTileMaterialization(seedProfile, result.Plan);
        PrintPoiTileSmoke(poiTileSmokeReport.Value);
        if (!poiTileSmokeReport.Value.Passed)
        {
            failures++;
        }
    }

    if (i == 0 && !skipGameplayScatterSmoke)
    {
        gameplayScatterSmokeReport = ValidateGameplayScatterMaterialization(seedProfile, result.Plan);
        PrintGameplayScatterSmoke(gameplayScatterSmokeReport.Value);
        if (!gameplayScatterSmokeReport.Value.Passed)
        {
            failures++;
        }
    }

    if (i == 0 && !skipBiomeScatterSmoke)
    {
        biomeScatterSmokeReport = ValidateBiomeScatterMaterialization(seedProfile, result.Plan);
        PrintBiomeScatterSmoke(biomeScatterSmokeReport.Value);
        if (!biomeScatterSmokeReport.Value.Passed)
        {
            failures++;
        }
    }

    if (i == 0 && !skipScenicLandmarkSmoke)
    {
        scenicLandmarkSmokeReport = ValidateScenicLandmarkMaterialization(seedProfile, result.Plan);
        PrintScenicLandmarkSmoke(scenicLandmarkSmokeReport.Value);
        if (!scenicLandmarkSmokeReport.Value.Passed)
        {
            failures++;
        }
    }
}

if (nativeSmoke)
{
    nativeSmokeReport = ValidateNativeSamplerParity(profile);
    PrintNativeSamplerSmoke(nativeSmokeReport.Value);
    if (!nativeSmokeReport.Value.Passed)
    {
        failures++;
    }
}

if (benchmarkTiles && benchmarkPlan is not null)
{
    tileBenchmarkReport = BenchmarkTerrainTiles(benchmarkProfile, benchmarkPlan, benchmarkTileCount);
    PrintTileBenchmark(tileBenchmarkReport.Value);
    if (!tileBenchmarkReport.Value.Passed)
    {
        failures++;
    }
}

PrintAggregate(
    aggregate,
    seedCount,
    failures,
    corridorSmokeReport,
    routeScatterSmokeReport,
    poiTileSmokeReport,
    gameplayScatterSmokeReport,
    biomeScatterSmokeReport,
    scenicLandmarkSmokeReport,
    nativeSmokeReport,
    tileBenchmarkReport);
return failures == 0 ? 0 : 1;

static TerrainValidationResult ValidateSeed(TerrainGenerationProfile profile, float worldSize)
{
    TerrainWorldPlan plan = TerrainWorldPlanner.CreateOpenWorldPlan(profile, Vector2.Zero, worldSize);
    TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
    TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
    TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
    TerrainPointOfInterestArchetypeValidationReport archetypeGate = TerrainPointOfInterestArchetypeCatalog.ValidatePlanReadiness(plan);
    return new TerrainValidationResult(profile.Seed, plan, qualityGate, planningGate, experienceGate, archetypeGate);
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
        coords.Add(new TerrainTileCoord(
            Mathf.FloorToInt(point.WorldPosition.X / profile.ChunkSize),
            Mathf.FloorToInt(point.WorldPosition.Y / profile.ChunkSize)));
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

    bool passed =
        materialized.Count == expected.Count &&
        distinctKinds >= 5 &&
        distinctScatterKinds >= 5 &&
        kindCounts[(int)TerrainLandmarkKind.Village] > 0 &&
        kindCounts[(int)TerrainLandmarkKind.Town] > 0 &&
        scatterKindCounts[(int)TerrainLandmarkKind.Village] >= kindCounts[(int)TerrainLandmarkKind.Village] &&
        scatterKindCounts[(int)TerrainLandmarkKind.Town] >= kindCounts[(int)TerrainLandmarkKind.Town] &&
        settlementInteriorScatterCount >= settlementLandmarkCount * 3 &&
        villageHouseScatterCount > 0 &&
        townBlockScatterCount > 0 &&
        settlementPlazaScatterCount > 0 &&
        (kindCounts[(int)TerrainLandmarkKind.OasisHub] == 0 ||
            (oasisCanopyScatterCount > 0 && oasisPoolScatterCount > 0)) &&
        landmarkScatterCount >= expected.Count &&
        footprintReport.Passed;
    string reason = passed
        ? "planned POIs materialized as tile landmarks"
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
        scatterKindCounts[(int)TerrainLandmarkKind.OasisPool];
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
        return new TerrainBiomeScatterSmokeReport(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "no biome scatter candidate tiles found");
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
    int biomeScatterCount =
        grassTuftCount +
        desertShrubCount +
        cactusClusterCount +
        reedClusterCount +
        snowClumpCount +
        alpinePineCount +
        coastalPalmCount +
        driftwoodCount +
        mangroveRootCount;
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
        biomeScatterCount >= 54;
    string reason = passed
        ? "biome surface scatter materialized across plains, desert, wetland, snowfield, coast, island, and alpine terrain"
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

        if (snowScore > 0.0f)
        {
            snowCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, snowScore));
        }

        if (coastScore > 0.0f)
        {
            coastCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, coastScore));
        }

        float generalScore = Mathf.Max(Mathf.Max(Mathf.Max(grassScore, desertScore), Mathf.Max(wetlandScore, snowScore)), coastScore);
        if (generalScore > 0.0f)
        {
            generalCandidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, generalScore));
        }
    }

    int categoryQuota = Mathf.Max(8, maxCoords / 6);
    AddSortedCandidateCoords(grassCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(desertCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(wetlandCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(snowCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(coastCandidates, profile, coords, Mathf.Min(maxCoords, coords.Count + categoryQuota));
    AddSortedCandidateCoords(generalCandidates, profile, coords, maxCoords);
}

static void PrintBiomeScatterSmoke(TerrainBiomeScatterSmokeReport report)
{
    Console.WriteLine(
        $"Biome scatter smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"tiles {report.SampledTileCount}/{report.CandidateTileCount}, " +
        $"grass/desert/cactus/reeds/snow/alpine/palms/driftwood/mangrove " +
        $"{report.GrassTuftCount}/{report.DesertShrubCount}/{report.CactusClusterCount}/{report.ReedClusterCount}/{report.SnowClumpCount}/{report.AlpinePineCount}/{report.CoastalPalmCount}/{report.DriftwoodCount}/{report.MangroveRootCount}, " +
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
        naturalArchCount > 0 &&
        geothermalSpringCount > 0 &&
        glacialRidgeCount > 0 &&
        distinctGeneratedKinds >= 7 &&
        scenicLandmarkCount >= waterfallCount + biomeScenicLandmarkCount;
    string reason = passed
        ? "scenic natural landmarks materialized across water, desert, rock, geothermal, and snow terrain"
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
        return new TerrainNativeSamplerSmokeReport(false, false, coord, resolution, 0, 0.0f, 0.0f, 0, 0.0f, 0.0f, "native height grid unavailable");
    }

    Vector2 origin = coord.Origin(nativeProfile.ChunkSize);
    float step = nativeProfile.ChunkSize / resolution;
    float maxDelta = 0.0f;
    double deltaSum = 0.0;
    int compared = Math.Min(expectedCount, nativeHeights.Length);

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
            float managedHeight = TerrainWorldFieldSampler.Sample(world, nativeProfile).Height;
            float delta = Math.Abs(nativeHeights[index] - managedHeight);
            maxDelta = Math.Max(maxDelta, delta);
            deltaSum += delta;
        }
    }

    float averageDelta = compared == 0 ? 0.0f : (float)(deltaSum / compared);
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
    bool passed = gridPassed && tilePassed;
    string reason = passed
        ? "native height grid and tile output match managed path tolerance"
        : gridPassed ? "native tile output diverged from managed path" : "native height grid diverged from managed sampler";

    return new TerrainNativeSamplerSmokeReport(
        passed,
        true,
        coord,
        resolution,
        compared,
        maxDelta,
        averageDelta,
        tileVertexCount,
        tileMaxHeightDelta,
        tileMaxColorDelta,
        reason);
}

static void PrintNativeSamplerSmoke(TerrainNativeSamplerSmokeReport report)
{
    Console.WriteLine(
        $"Native sampler smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"available {report.Available}, tile {report.Coord}, resolution {report.Resolution}, " +
        $"samples {report.ComparedSampleCount}, max delta {report.MaxHeightDelta:0.000}, " +
        $"avg delta {report.AverageHeightDelta:0.000}, tile vertices {report.TileVertexCount}, " +
        $"tile delta {report.TileMaxHeightDelta:0.000}/{report.TileMaxColorDelta:0.000} ({report.Reason})");
}

static TerrainTileBenchmarkReport BenchmarkTerrainTiles(
    TerrainGenerationProfile profile,
    TerrainWorldPlan plan,
    int requestedTileCount)
{
    TerrainTileCoord[] coords = SelectBenchmarkTileCoords(profile, plan, requestedTileCount);
    TerrainRouteCorridorIndex corridorIndex = TerrainRouteCorridorIndex.FromPlan(plan, profile);
    TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);
    TerrainGenerationProfile managedProfile = profile with { UseNativeSamplerWhenAvailable = false };
    TerrainGenerationProfile nativeProfile = profile with { UseNativeSamplerWhenAvailable = true };
    bool nativeAvailable = NativeTerrainBridge.IsAvailable;
    TerrainTileBenchmarkThresholds thresholds = TerrainTileBenchmarkThresholds.Default;

    if (coords.Length == 0)
    {
        return new TerrainTileBenchmarkReport(
            false,
            nativeAvailable,
            requestedTileCount,
            0,
            default,
            default,
            0,
            0.0f,
            0.0f,
            0.0,
            thresholds,
            "no benchmark tile coordinates selected");
    }

    TerrainTileBuilder.Build(coords[0], lod: 0, managedProfile, includeCollision: false, corridorIndex, poiIndex);
    if (nativeAvailable)
    {
        TerrainTileBuilder.Build(coords[0], lod: 0, nativeProfile, includeCollision: false, corridorIndex, poiIndex);
    }

    TerrainTileBenchmarkPass managed = MeasureTileBuildPass(coords, managedProfile, corridorIndex, poiIndex);
    TerrainTileBenchmarkPass native = nativeAvailable
        ? MeasureTileBuildPass(coords, nativeProfile, corridorIndex, poiIndex)
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
        nativeAvailable,
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
        requestedTileCount,
        coords.Length,
        managed,
        native,
        parityTileCount,
        maxHeightDelta,
        maxColorDelta,
        speedup,
        thresholds,
        reason);
}

static bool EvaluateTileBenchmark(
    int measuredTileCount,
    bool nativeAvailable,
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

    foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
    {
        if (TryAddBenchmarkCoord(coords, seen, point.WorldPosition, profile, maxCoords))
        {
            return coords.ToArray();
        }
    }

    foreach (TerrainWorldRoute route in plan.Routes)
    {
        if (route.Waypoints.Length == 0)
        {
            continue;
        }

        if (TryAddBenchmarkCoord(coords, seen, route.Waypoints[route.Waypoints.Length / 2], profile, maxCoords))
        {
            return coords.ToArray();
        }
    }

    var candidates = new List<GameplayScatterRegionCandidate>(plan.Regions.Length);
    foreach (TerrainWorldRegion region in plan.Regions)
    {
        if (region.RegionKind == TerrainWorldRegionKind.Ocean)
        {
            continue;
        }

        float score =
            region.ScenicPotential * 0.30f +
            region.EncounterPotential * 0.25f +
            region.ResourcePotential * 0.20f +
            region.HazardPotential * 0.20f +
            region.Traversability * 0.05f;
        candidates.Add(new GameplayScatterRegionCandidate(region.WorldPosition, score));
    }

    candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
    foreach (GameplayScatterRegionCandidate candidate in candidates)
    {
        if (TryAddBenchmarkCoord(coords, seen, candidate.WorldPosition, profile, maxCoords))
        {
            return coords.ToArray();
        }
    }

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
        $"Tile generation benchmark: {(report.Passed ? "PASS" : "FAIL")} native available {report.NativeAvailable}, " +
        $"tiles {report.MeasuredTileCount}/{report.RequestedTileCount}, " +
        $"native speedup {report.NativeSpeedup:0.00}x, parity tiles {report.ParityTileCount}, " +
        $"max parity delta {report.MaxHeightDelta:0.000}/{report.MaxColorDelta:0.000} ({report.Reason})");
    Console.WriteLine(
        $"Benchmark thresholds: managed <= {report.Thresholds.MaxManagedMillisecondsPerTile:0.00} ms/tile, " +
        $"native <= {report.Thresholds.MaxNativeMillisecondsPerTile:0.00} ms/tile, " +
        $"alloc <= {report.Thresholds.MaxAllocatedKilobytesPerTile:0.0} KB/tile, " +
        $"speedup >= {report.Thresholds.MinNativeSpeedup:0.00}x");
    PrintTileBenchmarkPass("Managed", report.Managed);
    if (report.NativeAvailable)
    {
        PrintTileBenchmarkPass("Native", report.Native);
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
    int failures,
    TerrainRouteCorridorSmokeReport? corridorSmokeReport,
    TerrainRouteScatterSmokeReport? routeScatterSmokeReport,
    TerrainPoiTileSmokeReport? poiTileSmokeReport,
    TerrainGameplayScatterSmokeReport? gameplayScatterSmokeReport,
    TerrainBiomeScatterSmokeReport? biomeScatterSmokeReport,
    TerrainScenicLandmarkSmokeReport? scenicLandmarkSmokeReport,
    TerrainNativeSamplerSmokeReport? nativeSmokeReport,
    TerrainTileBenchmarkReport? tileBenchmarkReport)
{
    Console.WriteLine();
    Console.WriteLine($"Open world terrain validation: {(failures == 0 ? "PASS" : "FAIL")} ({seedCount - failures}/{seedCount} seeds passed)");
    Console.WriteLine($"Land ratio min/avg/max: {aggregate.MinLandRatio:0.000} / {aggregate.AverageLandRatio:0.000} / {aggregate.MaxLandRatio:0.000}");
    Console.WriteLine($"Scenic ratio min/avg/max: {aggregate.MinScenicRatio:0.000} / {aggregate.AverageScenicRatio:0.000} / {aggregate.MaxScenicRatio:0.000}");
    Console.WriteLine($"Traversable land min/avg/max: {aggregate.MinTraversableLandRatio:0.000} / {aggregate.AverageTraversableLandRatio:0.000} / {aggregate.MaxTraversableLandRatio:0.000}");
    Console.WriteLine($"POI count min/avg/max: {aggregate.MinPoiCount} / {aggregate.AveragePoiCount:0.0} / {aggregate.MaxPoiCount}");
    Console.WriteLine($"Route count min/avg/max: {aggregate.MinRouteCount} / {aggregate.AverageRouteCount:0.0} / {aggregate.MaxRouteCount}");
    Console.WriteLine($"Connected ratio min/avg/max: {aggregate.MinConnectedPointRatio:0.000} / {aggregate.AverageConnectedPointRatio:0.000} / {aggregate.MaxConnectedPointRatio:0.000}");
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
    float MaxHeightDelta,
    float AverageHeightDelta,
    int TileVertexCount,
    float TileMaxHeightDelta,
    float TileMaxColorDelta,
    string Reason);

/// <summary>Reports managed vs native tile generation benchmark results including timing, allocations, and parity.</summary>
internal readonly record struct TerrainTileBenchmarkReport(
    bool Passed,
    bool NativeAvailable,
    int RequestedTileCount,
    int MeasuredTileCount,
    TerrainTileBenchmarkPass Managed,
    TerrainTileBenchmarkPass Native,
    int ParityTileCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    double NativeSpeedup,
    TerrainTileBenchmarkThresholds Thresholds,
    string Reason);

/// <summary>Thresholds for tile generation benchmark pass/fail criteria.</summary>
internal readonly record struct TerrainTileBenchmarkThresholds(
    double MaxManagedMillisecondsPerTile,
    double MaxNativeMillisecondsPerTile,
    double MaxAllocatedKilobytesPerTile,
    double MinNativeSpeedup,
    int MinParityTileCount,
    float MaxParityHeightDelta,
    float MaxParityColorDelta)
{
    public static TerrainTileBenchmarkThresholds Default { get; } = new(
        MaxManagedMillisecondsPerTile: 55.0,
        MaxNativeMillisecondsPerTile: 18.0,
        MaxAllocatedKilobytesPerTile: 1800.0,
        MinNativeSpeedup: 2.50,
        MinParityTileCount: 8,
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
