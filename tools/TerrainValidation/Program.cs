using System;
using System.Collections.Generic;
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
bool skipPoiTileSmoke = HasFlag(args, "--skip-poi-tile-smoke");
bool nativeSmoke = HasFlag(args, "--native-smoke");

int failures = 0;
TerrainValidationAggregate aggregate = new();
TerrainRouteCorridorSmokeReport? corridorSmokeReport = null;
TerrainPoiTileSmokeReport? poiTileSmokeReport = null;
TerrainNativeSamplerSmokeReport? nativeSmokeReport = null;

for (int i = 0; i < seedCount; i++)
{
    TerrainGenerationProfile seedProfile = profile with { Seed = seed + i * seedStep };
    TerrainValidationResult result = ValidateSeed(seedProfile, worldSize);
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

    if (i == 0 && !skipPoiTileSmoke)
    {
        poiTileSmokeReport = ValidatePoiTileMaterialization(seedProfile, result.Plan);
        PrintPoiTileSmoke(poiTileSmokeReport.Value);
        if (!poiTileSmokeReport.Value.Passed)
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

PrintAggregate(aggregate, seedCount, failures, corridorSmokeReport, poiTileSmokeReport, nativeSmokeReport);
return failures == 0 ? 0 : 1;

static TerrainValidationResult ValidateSeed(TerrainGenerationProfile profile, float worldSize)
{
    TerrainWorldPlan plan = TerrainWorldPlanner.CreateOpenWorldPlan(profile, Vector2.Zero, worldSize);
    TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
    TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
    TerrainPointOfInterestArchetypeValidationReport archetypeGate = TerrainPointOfInterestArchetypeCatalog.ValidatePlanReadiness(plan);
    return new TerrainValidationResult(profile.Seed, plan, qualityGate, planningGate, archetypeGate);
}

static void PrintSeedResult(TerrainValidationResult result, bool detailed)
{
    TerrainQualityReport quality = result.QualityGate.Report;
    TerrainWorldPlanningReport planning = result.PlanningGate.Report;
    Console.WriteLine(
        $"Seed {result.Seed}: {(result.Passed ? "PASS" : "FAIL")} " +
        $"land {quality.LandRatio:0.000}, scenic {quality.ScenicRatio:0.000}, " +
        $"traversable {quality.TraversableLandRatio:0.000}, POIs {planning.PointOfInterestCount}, " +
        $"routes {planning.RouteCount}, connected {planning.ConnectedPointRatio:0.000}, " +
        $"coverage {planning.PointOfInterestWorldCoverage:0.000}/{planning.RouteWorldCoverage:0.000}, " +
        $"archetypes {(result.ArchetypeGate.Passed ? "PASS" : "FAIL")}");

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
    Console.WriteLine($"Connected point ratio: {planning.ConnectedPointRatio:0.000}");
    Console.WriteLine($"World coverage POIs/routes: {planning.PointOfInterestWorldCoverage:0.000} / {planning.RouteWorldCoverage:0.000}");
    Console.WriteLine($"Average route scenic/traversability: {planning.AverageRouteScenicPotential:0.000} / {planning.AverageRouteTraversability:0.000}");
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
    Span<int> kindCounts = stackalloc int[8];
    Span<int> scatterKindCounts = stackalloc int[8];
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

    bool passed =
        materialized.Count == expected.Count &&
        distinctKinds >= 5 &&
        distinctScatterKinds >= 5 &&
        landmarkScatterCount >= expected.Count;
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
        landmarkScatterCount,
        reason);
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
        $"landmark scatter {report.LandmarkScatterCount} ({report.Reason})");
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
        return new TerrainNativeSamplerSmokeReport(false, false, coord, resolution, 0, 0.0f, 0.0f, "native height grid unavailable");
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
    bool passed = compared == expectedCount && maxDelta <= 1.5f && averageDelta <= 0.25f;
    string reason = passed
        ? "native height grid matches managed sampler tolerance"
        : "native height grid diverged from managed sampler";

    return new TerrainNativeSamplerSmokeReport(passed, true, coord, resolution, compared, maxDelta, averageDelta, reason);
}

static void PrintNativeSamplerSmoke(TerrainNativeSamplerSmokeReport report)
{
    Console.WriteLine(
        $"Native sampler smoke: {(report.Passed ? "PASS" : "FAIL")} " +
        $"available {report.Available}, tile {report.Coord}, resolution {report.Resolution}, " +
        $"samples {report.ComparedSampleCount}, max delta {report.MaxHeightDelta:0.000}, " +
        $"avg delta {report.AverageHeightDelta:0.000} ({report.Reason})");
}

static void PrintAggregate(
    TerrainValidationAggregate aggregate,
    int seedCount,
    int failures,
    TerrainRouteCorridorSmokeReport? corridorSmokeReport,
    TerrainPoiTileSmokeReport? poiTileSmokeReport,
    TerrainNativeSamplerSmokeReport? nativeSmokeReport)
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
    Console.WriteLine($"Runtime archetype readiness: {(aggregate.ArchetypeFailureCount == 0 ? "PASS" : "FAIL")} ({seedCount - aggregate.ArchetypeFailureCount}/{seedCount} seeds covered)");
    if (corridorSmokeReport is not null)
    {
        Console.WriteLine($"Route corridor tile smoke: {(corridorSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
    }
    if (poiTileSmokeReport is not null)
    {
        Console.WriteLine($"POI tile landmark smoke: {(poiTileSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
    }
    if (nativeSmokeReport is not null)
    {
        Console.WriteLine($"Native sampler smoke: {(nativeSmokeReport.Value.Passed ? "PASS" : "FAIL")}");
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
        UseNativeSamplerWhenAvailable: false);
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

internal readonly record struct TerrainValidationResult(
    int Seed,
    TerrainWorldPlan Plan,
    TerrainQualityGateResult QualityGate,
    TerrainWorldPlanningGateResult PlanningGate,
    TerrainPointOfInterestArchetypeValidationReport ArchetypeGate)
{
    public bool Passed => QualityGate.Passed && PlanningGate.Passed && ArchetypeGate.Passed;
}

internal readonly record struct TerrainRouteCorridorSmokeReport(
    bool Passed,
    int Seed,
    TerrainTileCoord Coord,
    int SegmentCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    int InfluencedVertexCount,
    string Reason);

internal readonly record struct TerrainPoiTileSmokeReport(
    bool Passed,
    int ExpectedPointCount,
    int MaterializedPointCount,
    int TileCount,
    int DistinctLandmarkKinds,
    int DistinctScatterLandmarkKinds,
    int LandmarkScatterCount,
    string Reason);

internal readonly record struct TerrainNativeSamplerSmokeReport(
    bool Passed,
    bool Available,
    TerrainTileCoord Coord,
    int Resolution,
    int ComparedSampleCount,
    float MaxHeightDelta,
    float AverageHeightDelta,
    string Reason);

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

    public void Add(TerrainValidationResult result)
    {
        TerrainQualityReport quality = result.QualityGate.Report;
        TerrainWorldPlanningReport planning = result.PlanningGate.Report;
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
